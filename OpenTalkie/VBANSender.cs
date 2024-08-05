using NAudio.Wave;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Reflection;

using System.Text;

namespace OpenTalkie;

public class VBANSender : IDisposable, ISampleProvider
{
    private string DestHost { get; }
    private readonly ISampleProvider _source;
    private readonly string _streamName;
    private readonly UdpClient _udpClient;
    private uint _framecount;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public VBANSender(ISampleProvider source, string destHost, int destPort, string streamName)
    {
        DestHost = destHost;
        _source = source;
        _streamName = streamName.Length > 16 ? streamName.Substring(0, 16) : streamName;
        _udpClient = new UdpClient(DestHost, destPort);
    }

    public void Dispose() => _udpClient.Close();

    public int Read(float[] buffer, int offset, int count)
    {
        var readed = _source.Read(buffer, offset, count);

        if (readed > 0)
            Sent(buffer, offset, readed);

        return readed;
    }

    private void Sent(ReadOnlyMemory<float> buffer, int offset, int readed)
    {
        var samples = buffer.Slice(offset, readed);
        const int _count = 256;
        int totalChunks = (samples.Length + _count - 1) / _count;

        for (int i = 0; i < totalChunks; i++)
        {
            int chunkSize = Math.Min(_count, samples.Length - i * _count);
            SentUdp(samples.Slice(i * _count, chunkSize).Span);
        }
    }

    private void SentUdp(ReadOnlySpan<float> samples)
    {
        var sendBytes = new List<byte>(1500)
        {
            (byte)'V',//F
            (byte)'B',//O
            (byte)'A',//U
            (byte)'N',//R
            (byte)((int)VBanProtocol.VBAN_PROTOCOL_AUDIO << 5 | Array.IndexOf(VBANConsts.VBAN_SRList, WaveFormat.SampleRate)),//SR+Protocol
            (byte)(samples.Length / WaveFormat.Channels - 1),//Number of samples 
            (byte)(WaveFormat.Channels - 1),//Number of channels
            (int)VBanCodec.VBAN_CODEC_PCM << 5 | 0 << 4 | (byte)VBanBitResolution.VBAN_BITFMT_16_INT//DataFormat+1bit pad+CODEC
        };

        sendBytes.AddRange(Encoding.ASCII.GetBytes(_streamName));
        sendBytes.AddRange(new byte[16 - _streamName.Length]);

        sendBytes.AddRange(BitConverter.GetBytes(_framecount++));

        /* // for 32bit float out
        var byteArray = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, byteArray, 0, byteArray.Length);
        foreach (var b in byteArray)
            sendBytes.Add(b);
        */

        for (int j = 0; j < samples.Length; j++)// 16bit int out
        {
            var f = samples[j];
            f = f * 32768;
            if (f > 32767) f = 32767;
            if (f < -32768) f = -32768;
            var i = (short)f;

            var bita = BitConverter.GetBytes(i);
            sendBytes.AddRange(Reverse(bita, Endian.Little));
        }

        try
        {
            _udpClient.Send(sendBytes.GetInternalArray(), sendBytes.Count);
        }
        catch (SocketException)
        //when (exception.Message == "Connection refused")
        {

        }
    }

    private static byte[] Reverse(byte[] bytes, Endian endian)
    {
        if (BitConverter.IsLittleEndian ^ endian == Endian.Little)
            Array.Reverse(bytes);

        return bytes;
    }
}

public static class ListExtensions
{
    static class ArrayAccessor<T>
    {
        public static Func<List<T>, T[]> Getter;

        static ArrayAccessor()
        {
            var dm = new DynamicMethod("get", MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(T[]), [typeof(List<T>)], typeof(ArrayAccessor<T>), true);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // Load List<T> argument
            il.Emit(OpCodes.Ldfld, typeof(List<T>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)); // Replace argument by field
            il.Emit(OpCodes.Ret); // Return field
            Getter = (Func<List<T>, T[]>)dm.CreateDelegate(typeof(Func<List<T>, T[]>));
        }
    }

    public static T[] GetInternalArray<T>(this List<T> list)
    {
        return ArrayAccessor<T>.Getter(list);
    }
}


internal enum Endian
{
    Little, Big
}
public static class VBANConsts
{
    public static readonly int[] VBAN_SRList =
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

