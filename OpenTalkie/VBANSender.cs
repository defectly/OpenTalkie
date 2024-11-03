using NAudio.Wave;
using System.Net.Sockets;

namespace OpenTalkie;

public class VBANSender : IDisposable, ISampleProvider
{
    private readonly string DestionationHost;
    private readonly ISampleProvider _source;
    private readonly string _streamName;
    private readonly UdpClient _udpClient;
    private readonly AtomicUInt _framecount;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public VBANSender(ISampleProvider source, string destHost, int destPort, string streamName)
    {
        DestionationHost = destHost;
        _source = source;
        _streamName = streamName.Length > 16 ? streamName.Substring(0, 16) : streamName;
        _udpClient = new UdpClient(DestionationHost, destPort);

        _framecount = new();
    }

    public void Dispose()
    {
        _udpClient.Close();
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
            _ = SendUdpAsync(samples.Slice(i * _count, chunkSize));
        }
    }

    private async Task SendUdpAsync(ReadOnlyMemory<float> samples)
    {
        byte[] packet = CreatePacket(samples.Length);

        AddDataToPacket(samples, packet);

        try
        {
            await _udpClient.SendAsync(packet.AsMemory());
        }
        catch (SocketException)
        //when (exception.Message == "Connection refused")
        {

        }
    }

    private static void AddDataToPacket(ReadOnlyMemory<float> samples, byte[] packet)
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

        packet[4] = (byte)((int)VBanProtocol.VBAN_PROTOCOL_AUDIO << 5 | Array.IndexOf(VBANConsts.SAMPLERATES, WaveFormat.SampleRate)); // Samplerate + sub protocol
        packet[5] = (byte)(samplesLength / WaveFormat.Channels - 1); // Number of samples 
        packet[6] = (byte)(WaveFormat.Channels - 1); // Number of channels
        packet[7] = (int)VBanCodec.VBAN_CODEC_PCM << 5 | 0 << 4 | (byte)VBanBitResolution.VBAN_BITFMT_16_INT; // DataFormat + 1bit + CODEC

        for (int i = 0; i < _streamName.Length; i++)
            packet[i + 8] = (byte)_streamName[i];

        _framecount.Increment();
        var convertedCounter = BitConverter.GetBytes(_framecount.GetValue());

        for (int i = 24; i < 28; i++)
            packet[i] = convertedCounter[i - 24];

        return packet;
    }

    private static void ReverseIfNeeded(byte[] bytes, Endian endian)
    {
        if (BitConverter.IsLittleEndian ^ endian == Endian.Little)
            Array.Reverse(bytes);
    }
}

internal enum Endian
{
    Little, Big
}

public static class VBANConsts
{
    public static readonly int[] SAMPLERATES =
    {
        6000, 12000, 24000, 48000,
        96000, 192000, 384000, 8000,
        16000, 32000, 64000, 128000,
        256000, 512000, 11025, 22050,
        44100, 88200, 176400, 352800,
        705600
    };
}

public enum VBanBitResolution
{
    VBAN_BITFMT_8_INT = 0,
    VBAN_BITFMT_16_INT,
    VBAN_BITFMT_24_INT,
    VBAN_BITFMT_32_INT,
    VBAN_BITFMT_32_FLOAT,
    VBAN_BITFMT_64_FLOAT,
    VBAN_BITFMT_12_INT,
    VBAN_BITFMT_10_INT,
}
public enum VBanCodec
{
    VBAN_CODEC_PCM = 0x00,
    VBAN_CODEC_VBCA = 0x10,
    VBAN_CODEC_VBCV = 0x20,
    VBAN_CODEC_UNDEFINED_3 = 0x30,
    VBAN_CODEC_UNDEFINED_4 = 0x40,
    VBAN_CODEC_UNDEFINED_5 = 0x50,
    VBAN_CODEC_UNDEFINED_6 = 0x60,
    VBAN_CODEC_UNDEFINED_7 = 0x70,
    VBAN_CODEC_UNDEFINED_8 = 0x80,
    VBAN_CODEC_UNDEFINED_9 = 0x90,
    VBAN_CODEC_UNDEFINED_10 = 0xA0,
    VBAN_CODEC_UNDEFINED_11 = 0xB0,
    VBAN_CODEC_UNDEFINED_12 = 0xC0,
    VBAN_CODEC_UNDEFINED_13 = 0xD0,
    VBAN_CODEC_UNDEFINED_14 = 0xE0,
    VBAN_CODEC_USER = 0xF0
}
public enum VBanProtocol
{
    VBAN_PROTOCOL_AUDIO = 0x00,
    VBAN_PROTOCOL_SERIAL = 0x20,
    VBAN_PROTOCOL_TXT = 0x40,
    VBAN_PROTOCOL_UNDEFINED_1 = 0x80,
    VBAN_PROTOCOL_UNDEFINED_2 = 0xA0,
    VBAN_PROTOCOL_UNDEFINED_3 = 0xC0,
    VBAN_PROTOCOL_UNDEFINED_4 = 0xE0
}

public enum VBanQuality
{
    VBAN_QUALITY_OPTIMAL = 512,
    VBAN_QUALITY_FAST = 1024,
    VBAN_QUALITY_MEDIUM = 2048,
    VBAN_QUALITY_SLOW = 4096,
    VBAN_QUALITY_VERYSLOW = 8192,
}

