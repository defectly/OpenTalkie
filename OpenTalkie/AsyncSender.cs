using OpenTalkie.Common.Services.Interfaces;
using OpenTalkie.RNNoise;
using OpenTalkie.VBAN;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

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
    private byte[]? _packetBuffer;
    private int _packetCapacity;
    private readonly AdaptCtx _adapt = new();

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
    }

    public void Dispose()
    {
        _denoiser.Dispose();
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
                EnsureCapacity(ref _denoisePending, _denoiseCount + bytesRead);
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
                    EnsureCapacity(ref _denoisePending, _denoiseCount + mono48Bytes.Length);
                    Buffer.BlockCopy(mono48Bytes, 0, _denoisePending!, _denoiseCount, mono48Bytes.Length);
                    _denoiseCount += mono48Bytes.Length;
                }
            }

            // Denoise full frames (480 samples = 960 bytes)
            int frameBytes = 480 * 2;
            int framesReady = _denoiseCount / frameBytes;
            if (framesReady > 0)
            {
                int outLen48 = framesReady * frameBytes; // mono 16-bit 48k
                EnsureCapacity(ref _denoiseBuffer, outLen48);
                for (int i = 0; i < framesReady; i++)
                {
                    int off = i * frameBytes;
                    _denoiser.Denoise(_denoisePending!, off, frameBytes, false);
                    Buffer.BlockCopy(_denoisePending!, off, _denoiseBuffer!, off, frameBytes);
                }
                int remain = _denoiseCount - outLen48;
                if (remain > 0)
                    Buffer.BlockCopy(_denoisePending!, outLen48, _denoisePending!, 0, remain);
                _denoiseCount = remain;

                // After denoise, DO NOT adapt back. Send as-is: 48k / 16-bit / mono
                denoisedOut = _denoiseBuffer.AsMemory(0, outLen48);
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
                    EnsureCapacity(ref _replicateBuffer, stereoLen);
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
                    EnsureCapacity(ref _volumeBuffer, length);
                    ApplyVolume16(sendMemory.Span, _volumeBuffer!.AsSpan(0, length), endpoint.Volume);
                    sendMemory = _volumeBuffer.AsMemory(0, length);
                }
                await SplitAndSendAsync(sendMemory, endpoint, outChannels, 2, 48000, VBanBitResolution.VBAN_BITFMT_16_INT, 256);
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
                EnsureCapacity(ref _volumeBuffer, length);
                ApplyVolume(sendMemory.Span, _volumeBuffer!.AsSpan(0, length), endpoint.Volume);
                sendMemory = _volumeBuffer.AsMemory(0, length);
            }

            await SplitAndSendAsync(sendMemory, endpoint);
        }
        return bytesRead;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task SplitAndSendAsync(ReadOnlyMemory<byte> buffer, Endpoint endpoint)
    {
        const int samplesPerChunk = 256;
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
        var packetBuffer = new byte[packetLength];

        InitializePacketHeader(packetBuffer);
        FillPacketData(samples, sampleCount, packetBuffer);

        string name = endpoint.Name;
        for (int j = 0; j < name.Length && j < 16; j++)
            packetBuffer[j + 8] = (byte)name[j];

        BitConverter.TryWriteBytes(packetBuffer.AsSpan(24, 4), endpoint.FrameCount);
        var sendBuffer = packetBuffer; // already dedicated per send

        try
        {
            var udpClient = endpoint.UdpClient;
            if (udpClient != null)
                await udpClient.SendAsync(sendBuffer, sendBuffer.Length);
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
        var packetBuffer = new byte[packetLength];

        InitializePacketHeader(packetBuffer, sampleRate, channels, bitRes);
        FillPacketData(samples, sampleCount, packetBuffer);

        string name = endpoint.Name;
        for (int j = 0; j < name.Length && j < 16; j++)
            packetBuffer[j + 8] = (byte)name[j];

        BitConverter.TryWriteBytes(packetBuffer.AsSpan(24, 4), endpoint.FrameCount);
        var sendBuffer = packetBuffer; // already dedicated per send

        try
        {
            var udpClient = endpoint.UdpClient;
            if (udpClient != null)
                await udpClient.SendAsync(sendBuffer, sendBuffer.Length);
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
            int samples = src.Length / 2;
            for (int i = 0; i < samples; i++)
            {
                short s = (short)(src[i * 2] | (src[i * 2 + 1] << 8));
                float amplified = s * gain;
                short clamped = amplified > short.MaxValue ? short.MaxValue : amplified < short.MinValue ? short.MinValue : (short)amplified;
                dst[i * 2] = (byte)(clamped & 0xFF);
                dst[i * 2 + 1] = (byte)((clamped >> 8) & 0xFF);
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
            int samples = src.Length / 3;
            for (int i = 0; i < samples; i++)
            {
                int off = i * 3;
                int val = src[off] | (src[off + 1] << 8) | (src[off + 2] << 16);
                if ((src[off + 2] & 0x80) != 0) val |= unchecked((int)0xFF000000);
                float amplified = val * gain;
                int clamped = amplified > 0x7FFFFF ? 0x7FFFFF : amplified < unchecked((int)0xFF800000) ? unchecked((int)0xFF800000) : (int)amplified;
                dst[off] = (byte)(clamped & 0xFF);
                dst[off + 1] = (byte)((clamped >> 8) & 0xFF);
                dst[off + 2] = (byte)((clamped >> 16) & 0xFF);
            }
            return;
        }
        if (bps == 4)
        {
            int samples = src.Length / 4;
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
        // Fallback copy
        src.CopyTo(dst);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyVolume16(ReadOnlySpan<byte> src, Span<byte> dst, float gain)
    {
        int samples = src.Length / 2;
        for (int i = 0; i < samples; i++)
        {
            short s = (short)(src[i * 2] | (src[i * 2 + 1] << 8));
            float amplified = s * gain;
            short clamped = amplified > short.MaxValue ? short.MaxValue : amplified < short.MinValue ? short.MinValue : (short)amplified;
            dst[i * 2] = (byte)(clamped & 0xFF);
            dst[i * 2 + 1] = (byte)((clamped >> 8) & 0xFF);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillPacketData(ReadOnlyMemory<byte> samples, int sampleCount, byte[] packetBuffer)
    {
        packetBuffer[5] = (byte)(sampleCount - 1);
        samples.Span.CopyTo(packetBuffer.AsSpan(28));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializePacketHeader(byte[] packetBuffer)
    {
        packetBuffer[0] = (byte)'V';
        packetBuffer[1] = (byte)'B';
        packetBuffer[2] = (byte)'A';
        packetBuffer[3] = (byte)'N';
        packetBuffer[4] = (byte)((int)VBanProtocol.VBAN_PROTOCOL_AUDIO << 5 | Array.IndexOf(VBANConsts.SAMPLERATES, _waveFormat.SampleRate));
        packetBuffer[6] = (byte)(_waveFormat.Channels - 1);
        packetBuffer[7] = (byte)((int)VBanCodec.VBAN_CODEC_PCM << 5 | (byte)_bitResolution);
    }
    private void InitializePacketHeader(byte[] packetBuffer, int sampleRate, int channels, VBanBitResolution bitRes)
    {
        packetBuffer[0] = (byte)'V';
        packetBuffer[1] = (byte)'B';
        packetBuffer[2] = (byte)'A';
        packetBuffer[3] = (byte)'N';
        packetBuffer[4] = (byte)((int)VBanProtocol.VBAN_PROTOCOL_AUDIO << 5 | Array.IndexOf(VBANConsts.SAMPLERATES, sampleRate));
        packetBuffer[6] = (byte)(channels - 1);
        packetBuffer[7] = (byte)((int)VBanCodec.VBAN_CODEC_PCM << 5 | (byte)bitRes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureCapacity(ref byte[]? buffer, int size)
    {
        if (buffer == null || buffer.Length < size)
            buffer = new byte[size];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsurePacketCapacity(int size)
    {
        if (_packetBuffer == null || _packetCapacity < size)
        {
            _packetCapacity = Math.Max(size, _packetCapacity * 2);
            if (_packetCapacity == 0) _packetCapacity = size;
            _packetBuffer = new byte[_packetCapacity];
        }
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
        var outList = new List<short>(context.ResInCount);
        while (context.ResInPos + 1.0 < context.ResInCount)
        {
            int i0 = (int)context.ResInPos;
            double frac = context.ResInPos - i0;
            short s0 = context.ResIn[i0];
            short s1 = context.ResIn[i0 + 1];
            int interp = s0 + (int)((s1 - s0) * frac);
            outList.Add((short)interp);
            context.ResInPos += step;
        }
        int consumed = Math.Max(0, (int)context.ResInPos);
        if (consumed > 0)
        {
            int remaining = context.ResInCount - consumed;
            if (remaining > 0) Array.Copy(context.ResIn, consumed, context.ResIn, 0, remaining);
            context.ResInCount = remaining;
            context.ResInPos -= consumed;
        }
        if (outList.Count == 0) return Array.Empty<byte>();
        var outBytes = new byte[outList.Count * 2];
        Buffer.BlockCopy(outList.ToArray(), 0, outBytes, 0, outBytes.Length);
        return outBytes;
    }

    private static short[] ResampleFrom48k(byte[] mono48kBytes, int outRate, AdaptCtx context)
    {
        if (mono48kBytes.Length == 0 || outRate <= 0) return Array.Empty<short>();
        if (context.LastOutRate != outRate)
        {
            context.LastOutRate = outRate;
            context.ResOutInCount = 0; context.ResOutPos = 0;
        }
        int frames = mono48kBytes.Length / 2;
        int required = context.ResOutInCount + frames;
        if (context.ResOutIn.Length < required)
        {
            var newBuffer = new short[Math.Max(required, context.ResOutIn.Length == 0 ? frames * 2 : context.ResOutIn.Length * 2)];
            if (context.ResOutInCount > 0) Array.Copy(context.ResOutIn, 0, newBuffer, 0, context.ResOutInCount);
            context.ResOutIn = newBuffer;
        }
        Buffer.BlockCopy(mono48kBytes, 0, context.ResOutIn, context.ResOutInCount * 2, mono48kBytes.Length);
        context.ResOutInCount += frames;

        double step = 48000.0 / (double)outRate;
        var outList = new List<short>(context.ResOutInCount);
        while (context.ResOutPos + 1.0 < context.ResOutInCount)
        {
            int i0 = (int)context.ResOutPos;
            double frac = context.ResOutPos - i0;
            short s0 = context.ResOutIn[i0];
            short s1 = context.ResOutIn[i0 + 1];
            int interp = s0 + (int)((s1 - s0) * frac);
            outList.Add((short)interp);
            context.ResOutPos += step;
        }
        int consumed = Math.Max(0, (int)context.ResOutPos);
        if (consumed > 0)
        {
            int remaining = context.ResOutInCount - consumed;
            if (remaining > 0) Array.Copy(context.ResOutIn, consumed, context.ResOutIn, 0, remaining);
            context.ResOutInCount = remaining;
            context.ResOutPos -= consumed;
        }
        return outList.ToArray();
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
    private static void DownmixToMono16(ReadOnlySpan<byte> src, Span<byte> dstMono, int channels)
    {
        int frames = src.Length / (channels * 2);
        for (int i = 0; i < frames; i++)
        {
            int inBase = i * channels * 2;
            int sum = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                short s = (short)(src[inBase + ch * 2] | (src[inBase + ch * 2 + 1] << 8));
                sum += s;
            }
            short m = (short)(sum / channels);
            int outOff = i * 2;
            dstMono[outOff] = (byte)(m & 0xFF);
            dstMono[outOff + 1] = (byte)((m >> 8) & 0xFF);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReplicateMonoToChannels16(ReadOnlySpan<byte> mono, Span<byte> dst, int channels)
    {
        int frames = mono.Length / 2;
        for (int i = 0; i < frames; i++)
        {
            byte lo = mono[i * 2];
            byte hi = mono[i * 2 + 1];
            int baseOff = i * channels * 2;
            for (int ch = 0; ch < channels; ch++)
            {
                int off = baseOff + ch * 2;
                dst[off] = lo;
                dst[off + 1] = hi;
            }
        }
    }
}

