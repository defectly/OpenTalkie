using NAudio.Wave;
using OpenTalkie.Common.Services.Interfaces;
using OpenTalkie.RNNoise;
using OpenTalkie.VBAN;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace OpenTalkie;

public class AsyncSender : IDisposable
{
    private readonly IInputStream _source;
    private readonly ObservableCollection<Endpoint> _endpoints;
    private readonly WaveFormat _waveFormat;
    private readonly Denoiser _denoiser = new();

    public AsyncSender(IInputStream source, ObservableCollection<Endpoint> endpoints)
    {
        _source = source;
        _endpoints = endpoints;
        _waveFormat = _source.GetWaveFormat();
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

        byte[] processedBuffer = new byte[readed];

        if (_endpoints.Any(e => e.IsDenoiseEnabled && e.IsEnabled))
        {
            Array.Copy(buffer, processedBuffer, readed);
            _denoiser.Denoise(processedBuffer, offset, readed);
        }
        else
            processedBuffer = buffer;

        if (readed > 0)
        {
            for (int i = 0; i < _endpoints.Count; i++)
            {
                Endpoint? endpoint = _endpoints[i];

                if (!endpoint.IsEnabled)
                    continue;

                byte[] endpointBuffer;

                if (endpoint.IsDenoiseEnabled)
                    endpointBuffer = processedBuffer;
                else
                    endpointBuffer = buffer;

                await SplitAndSendAsync(endpointBuffer.AsMemory(offset, readed), endpoint);
            }
        }
        return readed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task SplitAndSendAsync(ReadOnlyMemory<byte> buffer, Endpoint endpoint)
    {
        const int samplesPerChunk = 256;
        int bytesPerSample = 2;
        int chunkSize = samplesPerChunk * bytesPerSample * _waveFormat.Channels;
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
        int sampleCount = samples.Length / (_waveFormat.Channels * 2);
        int dataLength = samples.Length;

        byte[] packetBuffer = new byte[28 + dataLength];

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
        catch (SocketException)
        {
        }

        endpoint.FrameCount++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FillPacketData(ReadOnlyMemory<byte> samples, int sampleCount, byte[] packetBuffer)
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
        packetBuffer[7] = (byte)((int)VBanCodec.VBAN_CODEC_PCM << 5 | (byte)VBanBitResolution.VBAN_BITFMT_16_INT);
    }
}