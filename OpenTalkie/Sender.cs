using NAudio.Wave;
using OpenTalkie.VBAN;
using System.Net.Sockets;

namespace OpenTalkie;

public class Sender : IDisposable
{
    private readonly ISampleProvider _source;
    private readonly List<Endpoint> _endpoints;

    public Sender(ISampleProvider source, List<Endpoint> endpoints)
    {
        _source = source;
        _endpoints = endpoints;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var readed = _source.Read(buffer, offset, count);

        if (readed > 0)
            SplitAndSend(buffer, offset, readed);

        return readed;
    }

    private void SplitAndSend(ReadOnlyMemory<float> buffer, int offset, int readed)
    {
        var samples = buffer.Slice(offset, readed);
        const int _count = 256;
        int totalChunks = (samples.Length + _count - 1) / _count;

        for (int i = 0; i < totalChunks; i++)
        {
            int chunkSize = Math.Min(_count, samples.Length - i * _count);
            _ = SendAsync(samples.Slice(i * _count, chunkSize))
                .ConfigureAwait(false);
        }
    }

    private async Task SendAsync(ReadOnlyMemory<float> samples)
    {
        byte[] packet = CreatePacket(samples.Length);

        FillPacketData(samples, packet);

        await Parallel.ForEachAsync(_endpoints, async (endpoint, ct) =>
        {
            if (!endpoint.StreamState)
                return;

            byte[] packetCopy = new byte[540];
            Array.Copy(packet, packetCopy, packet.Length);

            endpoint.CorrectPacket(packetCopy);

            try
            {
                _ = endpoint.UdpClient.SendAsync(packetCopy, ct)
                .ConfigureAwait(false);
            }
            catch (SocketException)
            {
            }
        });
    }

    private static void FillPacketData(ReadOnlyMemory<float> samples, byte[] packet)
    {
        int last = 27;

        for (int j = 0; j < samples.Length; j++) // 16bit int out
        {
            var f = samples.Span[j];
            f = f * 32768;

            if (f > 32767)
                f = 32767;
            else if (f < -32768)
                f = -32768;

            var i = (short)f;

            var bita = BitConverter.GetBytes(i);
            ReverseIfNeeded(bita, Endian.Little);

            last += 1;

            for (int k = 0; k < bita.Length; k++)
            {
                last += k;
                packet[last] = bita[k];
            }
        }
    }

    private byte[] CreatePacket(int samplesLength)
    {
        byte[] packet = new byte[540];

        packet[0] = (byte)'V';
        packet[1] = (byte)'B';
        packet[2] = (byte)'A';
        packet[3] = (byte)'N';

        packet[4] = (byte)((int)VBanProtocol.VBAN_PROTOCOL_AUDIO << 5 | Array.IndexOf(VBANConsts.SAMPLERATES, _source.WaveFormat.SampleRate)); // Samplerate + sub protocol
        packet[5] = (byte)(samplesLength / _source.WaveFormat.Channels - 1); // Number of samples 
        packet[6] = (byte)(_source.WaveFormat.Channels - 1); // Number of channels
        packet[7] = (int)VBanCodec.VBAN_CODEC_PCM << 5 | 0 << 4 | (byte)VBanBitResolution.VBAN_BITFMT_16_INT; // DataFormat + 1bit + CODEC

        return packet;
    }

    private static void ReverseIfNeeded(byte[] bytes, Endian endian)
    {
        if (BitConverter.IsLittleEndian ^ endian == Endian.Little)
            Array.Reverse(bytes);
    }
}
