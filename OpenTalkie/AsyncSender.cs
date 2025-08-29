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
    private byte[]? _denoisePending;
    private int _denoiseCount;
    private byte[]? _volumeBuffer;
    private byte[]? _packetBuffer;
    private int _packetCapacity;

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
        int readed = await _source.ReadAsync(buffer, offset, count);

        if (readed <= 0)
            return readed;

        // Optional denoise (streaming, only if source is RNNoise-compatible)
        byte[] processedBuffer = buffer;
        ReadOnlyMemory<byte> denoisedOut = ReadOnlyMemory<byte>.Empty;
        bool anyDenoise = false;
        for (int ei = 0; ei < _endpoints.Count; ei++)
        {
            var ept = _endpoints[ei];
            if (ept.IsEnabled && ept.IsDenoiseEnabled) { anyDenoise = true; break; }
        }
        if (anyDenoise)
        {
            // RNNoise supports only 48kHz, mono, 16-bit.
            if (_waveFormat.SampleRate == 48000 && _waveFormat.BitsPerSample == 16 && _waveFormat.Channels == 1)
            {
                // Append to pending
                EnsureCapacity(ref _denoisePending, _denoiseCount + readed);
                Buffer.BlockCopy(buffer, offset, _denoisePending!, _denoiseCount, readed);
                _denoiseCount += readed;

                int frameBytes = 480 * 2;
                int framesReady = _denoiseCount / frameBytes;
                if (framesReady > 0)
                {
                    int outLen = framesReady * frameBytes;
                    EnsureCapacity(ref _denoiseBuffer, outLen);
                    // Process in place in pending, output to _denoiseBuffer
                    for (int i = 0; i < framesReady; i++)
                    {
                        int off = i * frameBytes;
                        _denoiser.Denoise(_denoisePending!, off, frameBytes, false);
                        Buffer.BlockCopy(_denoisePending!, off, _denoiseBuffer!, off, frameBytes);
                    }
                    // shift remainder
                    int remain = _denoiseCount - outLen;
                    if (remain > 0)
                        Buffer.BlockCopy(_denoisePending!, outLen, _denoisePending!, 0, remain);
                    _denoiseCount = remain;
                    denoisedOut = _denoiseBuffer.AsMemory(0, outLen);
                }
                else
                {
                    denoisedOut = ReadOnlyMemory<byte>.Empty; // no output yet
                }
            }
            // else: not compatible; skip adaptation as requested to avoid lag
        }

        for (int i = 0; i < _endpoints.Count; i++)
        {
            Endpoint? endpoint = _endpoints[i];
            if (!endpoint.IsEnabled)
                continue;

            ReadOnlyMemory<byte> sendMemory;
            if (endpoint.IsDenoiseEnabled && denoisedOut.Length > 0)
            {
                sendMemory = denoisedOut;
            }
            else
            {
                byte[] endpointBuffer = processedBuffer;
                sendMemory = endpointBuffer == buffer
                    ? endpointBuffer.AsMemory(offset, readed)
                    : endpointBuffer.AsMemory(0, readed);
            }

            if (endpoint.Volume != 1f)
            {
                EnsureCapacity(ref _volumeBuffer, readed);
                ApplyVolume(sendMemory.Span, _volumeBuffer!.AsSpan(0, readed), endpoint.Volume);
                sendMemory = _volumeBuffer.AsMemory(0, readed);
            }

            await SplitAndSendAsync(sendMemory, endpoint);
        }
        return readed;
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

    private async Task SendAsync(ReadOnlyMemory<byte> samples, Endpoint endpoint)
    {
        int sampleCount = samples.Length / (_waveFormat.Channels * _bytesPerSample);
        int dataLength = samples.Length;

        EnsurePacketCapacity(28 + dataLength);
        var packetBuffer = _packetBuffer!;

        InitializePacketHeader(packetBuffer);
        FillPacketData(samples, sampleCount, packetBuffer);

        string name = endpoint.Name;
        for (int j = 0; j < name.Length && j < 16; j++)
            packetBuffer[j + 8] = (byte)name[j];

        BitConverter.TryWriteBytes(packetBuffer.AsSpan(24, 4), endpoint.FrameCount);

        try
        {
            await endpoint.UdpClient.SendAsync(packetBuffer, 28 + dataLength);
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
}
