using OpenTalkie.Common.Services.Interfaces;
using OpenTalkie.RNNoise;
using OpenTalkie.VBAN;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace OpenTalkie;

public class AsyncSender : IDisposable
{
    private readonly IInputStream _source;
    private readonly ObservableCollection<Endpoint> _endpoints;
    private readonly WaveFormat _waveFormat;
    private readonly Denoiser _denoiser = new();
    private readonly VBanBitResolution _bitResolution;
    private readonly int _bytesPerSample;
    private byte[]? _denoiseBuffer;
    private byte[]? _denoisePending; // mono 16-bit 48k pending bytes
    private int _denoiseCount;
    private byte[]? _replicateBuffer;  // temp replicate back to original channels
    private byte[]? _volumeBuffer;
    private readonly AdaptCtx _adapt = new();
    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;
    private byte[]? _packetScratch;
    private int _packetScratchCapacity;
    private readonly int _srIndex;
    private readonly int _srIndex48;
    private const int VBanMaxSamplesPerPacket = 256; // PCM max samples per packet per VBAN spec

    public AsyncSender(IInputStream source, ObservableCollection<Endpoint> endpoints)
    {
        _source = source;
        _endpoints = endpoints;
        _waveFormat = _source.GetWaveFormat();

        _bitResolution = _waveFormat.BitsPerSample switch
        {
            8 => VBanBitResolution.VBAN_BITFMT_8_INT,
            16 => VBanBitResolution.VBAN_BITFMT_16_INT,
            24 => VBanBitResolution.VBAN_BITFMT_24_INT,
            32 => VBanBitResolution.VBAN_BITFMT_32_INT,
            _ => throw new NotSupportedException($"Unsupported encoding: {_waveFormat.BitsPerSample}")
        };

        _bytesPerSample = _waveFormat.BitsPerSample / 8;
        _srIndex = Array.IndexOf(VBANConsts.SAMPLERATES, _waveFormat.SampleRate);
        _srIndex48 = Array.IndexOf(VBANConsts.SAMPLERATES, 48000);
    }

    public void Dispose()
    {
        _denoiser.Dispose();
        ReturnPooled(ref _denoiseBuffer);
        ReturnPooled(ref _denoisePending);
        ReturnPooled(ref _replicateBuffer);
        ReturnPooled(ref _volumeBuffer);
        ReturnPooled(ref _packetScratch);
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        int bytesRead = await _source.ReadAsync(buffer, offset, count);

        if (bytesRead <= 0)
            return bytesRead;

        // Optional denoise (streaming, only if source is RNNoise-compatible)
        byte[] processedBuffer = buffer;
        ReadOnlyMemory<byte> denoisedOut = ReadOnlyMemory<byte>.Empty;
        int denoisedOutLen = 0;
        bool anyDenoise = false;
        for (int endpointIndex = 0; endpointIndex < _endpoints.Count; endpointIndex++)
        {
            var endpointItem = _endpoints[endpointIndex];
            if (endpointItem.IsEnabled && endpointItem.IsDenoiseEnabled) { anyDenoise = true; break; }
        }
        if (anyDenoise)
        {
            // Prepare mono 16-bit stream for RNNoise at 48k
            if (_waveFormat.BitsPerSample == 16 && _waveFormat.SampleRate == 48000 && _waveFormat.Channels == 1)
            {
                // Fast path: already mono/48k/16
                EnsurePooledCapacity(ref _denoisePending, _denoiseCount + bytesRead);
                Buffer.BlockCopy(buffer, offset, _denoisePending!, _denoiseCount, bytesRead);
                _denoiseCount += bytesRead;
            }
            else
            {
                // Convert arbitrary input to mono 16-bit at input rate
                short[] monoShorts = Array.Empty<short>();
                ConvertToMono16(buffer, offset, bytesRead, _waveFormat.BitsPerSample, _waveFormat.Channels, ref monoShorts, out int inFrames);
                // Resample to 48k if needed
                byte[] mono48Bytes;
                if (_waveFormat.SampleRate == 48000)
                {
                    mono48Bytes = new byte[inFrames * 2];
                    Buffer.BlockCopy(monoShorts, 0, mono48Bytes, 0, mono48Bytes.Length);
                }
                else
                {
                    mono48Bytes = ResampleTo48k(monoShorts, inFrames, _waveFormat.SampleRate, _adapt);
                }
                if (mono48Bytes.Length > 0)
                {
                    EnsurePooledCapacity(ref _denoisePending, _denoiseCount + mono48Bytes.Length);
                    Buffer.BlockCopy(mono48Bytes, 0, _denoisePending!, _denoiseCount, mono48Bytes.Length);
                    _denoiseCount += mono48Bytes.Length;
                }
            }

            // Denoise full frames (480 samples = 960 bytes)
            int frameBytes = 480 * 2;
            int framesReady = _denoiseCount / frameBytes;
            if (framesReady > 0)
            {
                denoisedOutLen = framesReady * frameBytes; // mono 16-bit 48k
                for (int i = 0; i < framesReady; i++)
                {
                    int off = i * frameBytes;
                    _denoiser.Denoise(_denoisePending!, off, frameBytes, false);
                }
                // After denoise, DO NOT adapt back. Send as-is: 48k / 16-bit / mono (read from pending buffer)
                denoisedOut = _denoisePending.AsMemory(0, denoisedOutLen);
            }
            else
            {
                denoisedOut = ReadOnlyMemory<byte>.Empty;
            }
        }

        for (int i = 0; i < _endpoints.Count; i++)
        {
            Endpoint? endpoint = _endpoints[i];
            if (!endpoint.IsEnabled)
                continue;

            ReadOnlyMemory<byte> sendMemory;
            if (endpoint.IsDenoiseEnabled && denoisedOut.Length > 0)
            {
                // RNNoise output is mono 48k/16-bit; replicate to 2 channels if original had >1 channel
                int outChannels = _waveFormat.Channels > 1 ? 2 : 1;
                if (outChannels == 2)
                {
                    int stereoLen = denoisedOut.Length * 2;
                    EnsurePooledCapacity(ref _replicateBuffer, stereoLen);
                    ReplicateMonoToChannels16(denoisedOut.Span, _replicateBuffer.AsSpan(0, stereoLen), 2);
                    sendMemory = _replicateBuffer.AsMemory(0, stereoLen);
                }
                else
                {
                    sendMemory = denoisedOut;
                }
                if (endpoint.Volume != 1f)
                {
                    int length = sendMemory.Length;
                    EnsurePooledCapacity(ref _volumeBuffer, length);
                    ApplyVolume16_Fixed(sendMemory.Span, _volumeBuffer!.AsSpan(0, length), endpoint.Volume);
                    sendMemory = _volumeBuffer.AsMemory(0, length);
                }
                int denoisedSamplesPerChunk = ComputeSamplesPerChunk(endpoint.Quality, 2, outChannels);
                await SplitAndSendAsync(sendMemory, endpoint, outChannels, 2, 48000, VBanBitResolution.VBAN_BITFMT_16_INT, denoisedSamplesPerChunk);
                continue;
            }
            else
            {
                byte[] endpointBuffer = processedBuffer;
                sendMemory = endpointBuffer == buffer
                    ? endpointBuffer.AsMemory(offset, bytesRead)
                    : endpointBuffer.AsMemory(0, bytesRead);
            }

            if (endpoint.Volume != 1f)
            {
                int length = sendMemory.Length;
                EnsurePooledCapacity(ref _volumeBuffer, length);
                ApplyVolume(sendMemory.Span, _volumeBuffer!.AsSpan(0, length), endpoint.Volume);
                sendMemory = _volumeBuffer.AsMemory(0, length);
            }

            await SplitAndSendAsync(sendMemory, endpoint);
        }
        // After sends, shift RNNoise pending remainder if any
        if (denoisedOutLen > 0)
        {
            int remain = _denoiseCount - denoisedOutLen;
            if (remain > 0)
                Buffer.BlockCopy(_denoisePending!, denoisedOutLen, _denoisePending!, 0, remain);
            _denoiseCount = remain;
        }
        return bytesRead;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task SplitAndSendAsync(ReadOnlyMemory<byte> buffer, Endpoint endpoint)
    {
        int samplesPerChunk = ComputeSamplesPerChunk(endpoint.Quality, _bytesPerSample, _waveFormat.Channels);
        int chunkSize = samplesPerChunk * _bytesPerSample * _waveFormat.Channels;
        int totalChunks = (buffer.Length + chunkSize - 1) / chunkSize;

        for (int i = 0; i < totalChunks; i++)
        {
            int start = i * chunkSize;
            int length = Math.Min(chunkSize, buffer.Length - start);
            await SendAsync(buffer.Slice(start, length), endpoint);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeSamplesPerChunk(VBanQuality quality, int bytesPerSample, int channels)
    {
        // Treat VBanQuality value as a reference bytes-per-packet at 16-bit stereo, then adapt.
        // Clamp to [1..256] samples per VBAN PCM constraints.
        int referenceBytesPerPacket = (int)quality; // 512, 1024, 2048, 4096, 8192
        int bytesPerFrame = Math.Max(1, bytesPerSample * Math.Max(1, channels));
        int desiredSamples = referenceBytesPerPacket / bytesPerFrame;
        if (desiredSamples <= 0) desiredSamples = 128; // reasonable fallback
        if (desiredSamples > VBanMaxSamplesPerPacket) desiredSamples = VBanMaxSamplesPerPacket;
        // Align to a multiple to favor SIMD/vectorized loops (e.g., 32 samples)
        const int align = 32;
        int rem = desiredSamples % align;
        if (rem != 0)
        {
            int aligned = desiredSamples + (align - rem);
            desiredSamples = Math.Min(aligned, VBanMaxSamplesPerPacket);
        }
        return desiredSamples;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task SplitAndSendAsync(ReadOnlyMemory<byte> buffer, Endpoint endpoint, int channels, int bytesPerSample, int sampleRate, VBanBitResolution bitRes, int samplesPerChunk)
    {
        int chunkSize = samplesPerChunk * bytesPerSample * channels;
        if (chunkSize <= 0) return;
        int totalChunks = (buffer.Length + chunkSize - 1) / chunkSize;

        for (int i = 0; i < totalChunks; i++)
        {
            int start = i * chunkSize;
            int length = Math.Min(chunkSize, buffer.Length - start);
            await SendAsync(buffer.Slice(start, length), endpoint, channels, bytesPerSample, sampleRate, bitRes);
        }
    }

    private async Task SendAsync(ReadOnlyMemory<byte> samples, Endpoint endpoint)
    {
        int sampleCount = samples.Length / (_waveFormat.Channels * _bytesPerSample);
        int dataLength = samples.Length;

        int packetLength = 28 + dataLength;
        byte[] sendBuffer = RentPacket(packetLength);
        InitializePacketHeader(sendBuffer.AsSpan(0, 28), _srIndex, _waveFormat.Channels, _bitResolution);
        FillPacketData(samples, sampleCount, sendBuffer);

        Buffer.BlockCopy(endpoint.NameBytes16, 0, sendBuffer, 8, 16);

        BitConverter.TryWriteBytes(sendBuffer.AsSpan(24, 4), endpoint.FrameCount);

        try
        {
            var udpClient = endpoint.UdpClient;
            if (udpClient != null)
                await udpClient.SendAsync(sendBuffer, packetLength);
        }
        catch
        {
        }

        endpoint.FrameCount++;
    }

    private async Task SendAsync(ReadOnlyMemory<byte> samples, Endpoint endpoint, int channels, int bytesPerSample, int sampleRate, VBanBitResolution bitRes)
    {
        int sampleCount = samples.Length / (channels * bytesPerSample);
        int dataLength = samples.Length;

        int packetLength = 28 + dataLength;
        byte[] sendBuffer = RentPacket(packetLength);
        int srIdx = sampleRate == 48000 ? _srIndex48 : Array.IndexOf(VBANConsts.SAMPLERATES, sampleRate);
        InitializePacketHeader(sendBuffer.AsSpan(0, 28), srIdx, channels, bitRes);
        FillPacketData(samples, sampleCount, sendBuffer);

        Buffer.BlockCopy(endpoint.NameBytes16, 0, sendBuffer, 8, 16);

        BitConverter.TryWriteBytes(sendBuffer.AsSpan(24, 4), endpoint.FrameCount);

        try
        {
            var udpClient = endpoint.UdpClient;
            if (udpClient != null)
                await udpClient.SendAsync(sendBuffer, packetLength);
        }
        catch
        {
        }

        endpoint.FrameCount++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyVolume(ReadOnlySpan<byte> src, Span<byte> dst, float gain)
    {
        int bps = _bytesPerSample;
        if (bps == 2)
        {
            // 16-bit LE
            var s16 = MemoryMarshal.Cast<byte, short>(src);
            var d16 = MemoryMarshal.Cast<byte, short>(dst);
            int q15 = (int)Math.Round(gain * 32768.0f);
            int bias = (q15 >= 0 ? 16384 : -16384);
            if (Vector.IsHardwareAccelerated && s16.Length >= Vector<short>.Count)
            {
                int vszShort = Vector<short>.Count;
                var qVec = new Vector<int>(q15);
                var biasVec = new Vector<int>(bias);
                var minI = new Vector<int>(short.MinValue);
                var maxI = new Vector<int>(short.MaxValue);
                int v = (s16.Length / vszShort) * vszShort;
                for (int i = 0; i < v; i += vszShort)
                {
                    var vS = new Vector<short>(s16.Slice(i));
                    Vector.Widen(vS, out Vector<int> lo, out Vector<int> hi);
                    lo = ((lo * qVec) + biasVec) >> 15;
                    hi = ((hi * qVec) + biasVec) >> 15;
                    lo = Vector.Min(Vector.Max(lo, minI), maxI);
                    hi = Vector.Min(Vector.Max(hi, minI), maxI);
                    var packed = Vector.Narrow(lo, hi);
                    packed.CopyTo(d16.Slice(i));
                }
                for (int i = v; i < s16.Length; i++)
                {
                    int scaled = (s16[i] * q15 + bias) >> 15;
                    if (scaled > short.MaxValue) scaled = short.MaxValue; else if (scaled < short.MinValue) scaled = short.MinValue;
                    d16[i] = (short)scaled;
                }
            }
            else
            {
                int samples = s16.Length;
                for (int i = 0; i < samples; i++)
                {
                    int scaled = (s16[i] * q15 + bias) >> 15;
                    if (scaled > short.MaxValue) scaled = short.MaxValue; else if (scaled < short.MinValue) scaled = short.MinValue;
                    d16[i] = (short)scaled;
                }
            }
            return;
        }
        if (bps == 1)
        {
            int samples = src.Length;
            for (int i = 0; i < samples; i++)
            {
                sbyte s = unchecked((sbyte)src[i]);
                float amplified = s * gain;
                sbyte clamped = amplified > sbyte.MaxValue ? sbyte.MaxValue : amplified < sbyte.MinValue ? sbyte.MinValue : (sbyte)amplified;
                dst[i] = unchecked((byte)clamped);
            }
            return;
        }
        if (bps == 3)
        {
            int q15 = (int)Math.Round(gain * 32768.0f);
            int bias = (q15 >= 0 ? 16384 : -16384);
            int samples = src.Length / 3;
            for (int i = 0; i < samples; i++)
            {
                int off = i * 3;
                int val = src[off] | (src[off + 1] << 8) | (src[off + 2] << 16);
                if ((src[off + 2] & 0x80) != 0) val |= unchecked((int)0xFF000000);
                int scaled = (val * q15 + bias) >> 15;
                if (scaled > 0x7FFFFF) scaled = 0x7FFFFF; else if (scaled < unchecked((int)0xFF800000)) scaled = unchecked((int)0xFF800000);
                dst[off] = (byte)(scaled & 0xFF);
                dst[off + 1] = (byte)((scaled >> 8) & 0xFF);
                dst[off + 2] = (byte)((scaled >> 16) & 0xFF);
            }
            return;
        }
        if (bps == 4)
        {
            int samples = src.Length / 4;
            if (Sse2.IsSupported && samples >= 4)
            {
                var gainVec = Vector128.Create(gain);
                int v = (samples / 4) * 4;
                for (int i = 0; i < v; i += 4)
                {
                    int off = i * 4;
                    int a0 = src[off] | (src[off + 1] << 8) | (src[off + 2] << 16) | (src[off + 3] << 24);
                    int a1 = src[off + 4] | (src[off + 5] << 8) | (src[off + 6] << 16) | (src[off + 7] << 24);
                    int a2 = src[off + 8] | (src[off + 9] << 8) | (src[off + 10] << 16) | (src[off + 11] << 24);
                    int a3 = src[off + 12] | (src[off + 13] << 8) | (src[off + 14] << 16) | (src[off + 15] << 24);
                    var vi = Vector128.Create(a0, a1, a2, a3);
                    var vf = Sse2.ConvertToVector128Single(vi);
                    vf = Sse.Multiply(vf, gainVec);
                    var vout = Sse2.ConvertToVector128Int32(vf); // truncates toward zero
                    int r0 = vout.GetElement(0);
                    int r1 = vout.GetElement(1);
                    int r2 = vout.GetElement(2);
                    int r3 = vout.GetElement(3);
                    dst[off] = (byte)(r0 & 0xFF);
                    dst[off + 1] = (byte)((r0 >> 8) & 0xFF);
                    dst[off + 2] = (byte)((r0 >> 16) & 0xFF);
                    dst[off + 3] = (byte)((r0 >> 24) & 0xFF);
                    dst[off + 4] = (byte)(r1 & 0xFF);
                    dst[off + 5] = (byte)((r1 >> 8) & 0xFF);
                    dst[off + 6] = (byte)((r1 >> 16) & 0xFF);
                    dst[off + 7] = (byte)((r1 >> 24) & 0xFF);
                    dst[off + 8] = (byte)(r2 & 0xFF);
                    dst[off + 9] = (byte)((r2 >> 8) & 0xFF);
                    dst[off + 10] = (byte)((r2 >> 16) & 0xFF);
                    dst[off + 11] = (byte)((r2 >> 24) & 0xFF);
                    dst[off + 12] = (byte)(r3 & 0xFF);
                    dst[off + 13] = (byte)((r3 >> 8) & 0xFF);
                    dst[off + 14] = (byte)((r3 >> 16) & 0xFF);
                    dst[off + 15] = (byte)((r3 >> 24) & 0xFF);
                }
                for (int i = v; i < samples; i++)
                {
                    int off = i * 4;
                    int val = src[off] | (src[off + 1] << 8) | (src[off + 2] << 16) | (src[off + 3] << 24);
                    float amplified = val * gain;
                    int clamped = amplified > int.MaxValue ? int.MaxValue : amplified < int.MinValue ? int.MinValue : (int)amplified;
                    dst[off] = (byte)(clamped & 0xFF);
                    dst[off + 1] = (byte)((clamped >> 8) & 0xFF);
                    dst[off + 2] = (byte)((clamped >> 16) & 0xFF);
                    dst[off + 3] = (byte)((clamped >> 24) & 0xFF);
                }
                return;
            }
            else
            {
                for (int i = 0; i < samples; i++)
                {
                    int off = i * 4;
                    int val = src[off] | (src[off + 1] << 8) | (src[off + 2] << 16) | (src[off + 3] << 24);
                    float amplified = val * gain;
                    int clamped = amplified > int.MaxValue ? int.MaxValue : amplified < int.MinValue ? int.MinValue : (int)amplified;
                    dst[off] = (byte)(clamped & 0xFF);
                    dst[off + 1] = (byte)((clamped >> 8) & 0xFF);
                    dst[off + 2] = (byte)((clamped >> 16) & 0xFF);
                    dst[off + 3] = (byte)((clamped >> 24) & 0xFF);
                }
                return;
            }
        }
        // Fallback copy
        src.CopyTo(dst);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyVolume16(ReadOnlySpan<byte> src, Span<byte> dst, float gain)
    {
        int q15 = (int)Math.Round(gain * 32768.0f);
        int bias = (q15 >= 0 ? 16384 : -16384);
        var s16 = MemoryMarshal.Cast<byte, short>(src);
        var d16 = MemoryMarshal.Cast<byte, short>(dst);
        if (Vector.IsHardwareAccelerated && s16.Length >= Vector<short>.Count)
        {
            int vszShort = Vector<short>.Count;
            var qVec = new Vector<int>(q15);
            var biasVec = new Vector<int>(bias);
            var minI = new Vector<int>(short.MinValue);
            var maxI = new Vector<int>(short.MaxValue);
            int v = (s16.Length / vszShort) * vszShort;
            for (int i = 0; i < v; i += vszShort)
            {
                var vS = new Vector<short>(s16.Slice(i));
                Vector.Widen(vS, out Vector<int> lo, out Vector<int> hi);
                lo = ((lo * qVec) + biasVec) >> 15;
                hi = ((hi * qVec) + biasVec) >> 15;
                lo = Vector.Min(Vector.Max(lo, minI), maxI);
                hi = Vector.Min(Vector.Max(hi, minI), maxI);
                var packed = Vector.Narrow(lo, hi);
                packed.CopyTo(d16.Slice(i));
            }
            for (int i = v; i < s16.Length; i++)
            {
                int scaled = (s16[i] * q15 + bias) >> 15;
                if (scaled > short.MaxValue) scaled = short.MaxValue; else if (scaled < short.MinValue) scaled = short.MinValue;
                d16[i] = (short)scaled;
            }
        }
        else
        {
            for (int i = 0; i < s16.Length; i++)
            {
                int scaled = (s16[i] * q15 + bias) >> 15;
                if (scaled > short.MaxValue) scaled = short.MaxValue; else if (scaled < short.MinValue) scaled = short.MinValue;
                d16[i] = (short)scaled;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyVolume16_Fixed(ReadOnlySpan<byte> src, Span<byte> dst, float gain)
        => ApplyVolume16(src, dst, gain);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillPacketData(ReadOnlyMemory<byte> samples, int sampleCount, byte[] packetBuffer)
    {
        packetBuffer[5] = (byte)(sampleCount - 1);
        samples.Span.CopyTo(packetBuffer.AsSpan(28));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializePacketHeader(Span<byte> header, int srIndex, int channels, VBanBitResolution bitRes)
    {
        header[0] = (byte)'V';
        header[1] = (byte)'B';
        header[2] = (byte)'A';
        header[3] = (byte)'N';
        header[4] = (byte)(((int)VBanProtocol.VBAN_PROTOCOL_AUDIO & 0xE0) | (srIndex & 0x1F));
        header[6] = (byte)(Math.Max(1, channels) - 1);
        header[7] = (byte)(((int)VBanCodec.VBAN_CODEC_PCM & 0xE0) | ((int)bitRes & 0x1F));
    }
    private void InitializePacketHeader(byte[] packetBuffer, int sampleRate, int channels, VBanBitResolution bitRes)
        => InitializePacketHeader(packetBuffer.AsSpan(0, 28), Array.IndexOf(VBANConsts.SAMPLERATES, sampleRate), channels, bitRes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsurePooledCapacity(ref byte[]? buffer, int size)
    {
        if (buffer == null)
        {
            buffer = _pool.Rent(size);
            return;
        }
        if (buffer.Length < size)
        {
            var old = buffer;
            buffer = _pool.Rent(size);
            _pool.Return(old);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnPooled(ref byte[]? buffer)
    {
        if (buffer != null)
        {
            _pool.Return(buffer);
            buffer = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] RentPacket(int size)
    {
        if (_packetScratch == null || _packetScratchCapacity < size)
        {
            if (_packetScratch != null) _pool.Return(_packetScratch);
            _packetScratch = _pool.Rent(size);
            _packetScratchCapacity = _packetScratch.Length;
        }
        return _packetScratch;
    }

    private sealed class AdaptCtx
    {
        public short[] ResIn = Array.Empty<short>();
        public int ResInCount;
        public double ResInPos;
        public int LastInRate = -1;
        public short[] ResOutIn = Array.Empty<short>();
        public int ResOutInCount;
        public double ResOutPos;
        public int LastOutRate = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadSampleAsInt(byte[] buffer, int offset, int bits)
    {
        return bits switch
        {
            // 8-bit PCM is typically unsigned; convert to signed centered at 0
            8 => (int)buffer[offset] - 128,
            16 => BitConverter.ToInt16(buffer, offset),
            24 => ((buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16)) | ((buffer[offset + 2] & 0x80) != 0 ? unchecked((int)0xFF000000) : 0)),
            32 => BitConverter.ToInt32(buffer, offset),
            _ => 0
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ToInt16(int sample, int inBits)
    {
        return inBits switch
        {
            8 => (short)(sample << 8),
            16 => (short)sample,
            24 => (short)(sample >> 8),
            32 => (short)(sample >> 16),
            _ => 0
        };
    }

    private static void ConvertToMono16(byte[] src, int offset, int length, int inBits, int channels, ref short[] dst, out int frames)
    {
        int bps = inBits / 8;
        frames = length / (bps * channels);
        if (frames <= 0) { dst = Array.Empty<short>(); return; }
        if (dst == null || dst.Length < frames) dst = new short[frames];
        for (int f = 0; f < frames; f++)
        {
            int baseIdx = offset + f * bps * channels;
            int acc = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                int s = ReadSampleAsInt(src, baseIdx + ch * bps, inBits);
                acc += ToInt16(s, inBits);
            }
            dst[f] = (short)(acc / channels);
        }
    }

    private static byte[] ResampleTo48k(short[] mono, int frames, int inRate, AdaptCtx context)
    {
        if (frames <= 0 || inRate <= 0) return Array.Empty<byte>();
        if (context.LastInRate != inRate)
        {
            context.LastInRate = inRate;
            context.ResInCount = 0; context.ResInPos = 0;
        }
        int required = context.ResInCount + frames;
        if (context.ResIn.Length < required)
        {
            var newBuffer = new short[Math.Max(required, context.ResIn.Length == 0 ? frames * 2 : context.ResIn.Length * 2)];
            if (context.ResInCount > 0) Array.Copy(context.ResIn, 0, newBuffer, 0, context.ResInCount);
            context.ResIn = newBuffer;
        }
        Array.Copy(mono, 0, context.ResIn, context.ResInCount, frames);
        context.ResInCount += frames;

        double step = (double)inRate / 48000.0;
        if (step <= 0) step = 1.0;

        // calculate maximum number of output samples we can produce
        int maxOut = 0;
        if (context.ResInCount > 1 && context.ResInPos < context.ResInCount - 1)
        {
            maxOut = (int)Math.Floor(((context.ResInCount - 1) - context.ResInPos) / step);
            if (maxOut < 0) maxOut = 0;
        }
        if (maxOut == 0) return Array.Empty<byte>();

        var outShorts = new short[maxOut];
        double pos = context.ResInPos;
        for (int k = 0; k < maxOut; k++)
        {
            int i0 = (int)pos;
            double frac = pos - i0;
            short s0 = context.ResIn[i0];
            short s1 = context.ResIn[i0 + 1];
            int interp = s0 + (int)((s1 - s0) * frac);
            outShorts[k] = (short)interp;
            pos += step;
        }
        context.ResInPos = pos;

        int consumed = Math.Max(0, (int)context.ResInPos);
        if (consumed > 0)
        {
            int remaining = context.ResInCount - consumed;
            if (remaining > 0) Array.Copy(context.ResIn, consumed, context.ResIn, 0, remaining);
            context.ResInCount = remaining;
            context.ResInPos -= consumed;
        }
        var outBytes = new byte[outShorts.Length * 2];
        Buffer.BlockCopy(outShorts, 0, outBytes, 0, outBytes.Length);
        return outBytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PackMonoToFormat(ReadOnlySpan<short> mono, Span<byte> dst, int channels, int bits)
    {
        int frames = mono.Length;
        if (bits == 16)
        {
            for (int f = 0; f < frames; f++)
            {
                short s = mono[f];
                int baseOff = f * channels * 2;
                for (int ch = 0; ch < channels; ch++)
                {
                    int off = baseOff + ch * 2;
                    dst[off] = (byte)(s & 0xFF);
                    dst[off + 1] = (byte)((s >> 8) & 0xFF);
                }
            }
            return;
        }
        if (bits == 8)
        {
            for (int f = 0; f < frames; f++)
            {
                sbyte s8 = (sbyte)(mono[f] >> 8);
                int baseOff = f * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    dst[baseOff + ch] = unchecked((byte)s8);
                }
            }
            return;
        }
        if (bits == 24)
        {
            for (int f = 0; f < frames; f++)
            {
                int v = mono[f] << 8;
                int baseOff = f * channels * 3;
                for (int ch = 0; ch < channels; ch++)
                {
                    int off = baseOff + ch * 3;
                    dst[off] = (byte)(v & 0xFF);
                    dst[off + 1] = (byte)((v >> 8) & 0xFF);
                    dst[off + 2] = (byte)((v >> 16) & 0xFF);
                }
            }
            return;
        }
        if (bits == 32)
        {
            for (int f = 0; f < frames; f++)
            {
                int v = mono[f] << 16;
                int baseOff = f * channels * 4;
                for (int ch = 0; ch < channels; ch++)
                {
                    int off = baseOff + ch * 4;
                    dst[off] = (byte)(v & 0xFF);
                    dst[off + 1] = (byte)((v >> 8) & 0xFF);
                    dst[off + 2] = (byte)((v >> 16) & 0xFF);
                    dst[off + 3] = (byte)((v >> 24) & 0xFF);
                }
            }
            return;
        }
        PackMonoToFormat(mono, dst, channels, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReplicateMonoToChannels16(ReadOnlySpan<byte> mono, Span<byte> dst, int channels)
    {
        int frames = mono.Length / 2;
        var src = MemoryMarshal.Cast<byte, short>(mono);
        var outShorts = MemoryMarshal.Cast<byte, short>(dst);
        if (channels == 2)
        {
            int i = 0;
            if (Sse2.IsSupported)
            {
                unsafe
                {
                    fixed (short* sPtr = src)
                    fixed (short* dPtr = outShorts)
                    {
                        int v = (frames / 8) * 8; // 8 shorts per 128-bit lane
                        for (; i < v; i += 8)
                        {
                            var vIn = Sse2.LoadVector128(sPtr + i);
                            var lo = Sse2.UnpackLow(vIn, vIn);  // s0 s0 s1 s1 s2 s2 s3 s3
                            var hi = Sse2.UnpackHigh(vIn, vIn); // s4 s4 s5 s5 s6 s6 s7 s7
                            int outBase = i * 2;
                            Sse2.Store(dPtr + outBase, lo);
                            Sse2.Store(dPtr + outBase + 8, hi);
                        }
                    }
                }
            }
            // tail or non-SIMD
            for (; i < frames; i++)
            {
                short s = src[i];
                int outBase = i * 2;
                outShorts[outBase] = s;
                outShorts[outBase + 1] = s;
            }
            return;
        }
        // generic N-channel replicate
        for (int i = 0; i < frames; i++)
        {
            short s = src[i];
            int baseOff = i * channels;
            for (int ch = 0; ch < channels; ch++)
                outShorts[baseOff + ch] = s;
        }
    }
}

