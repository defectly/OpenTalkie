using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenTalkie.Infrastructure.Audio;

public static class PcmVolume
{
    public static void Apply(ReadOnlySpan<byte> source, Span<byte> destination, int bytesPerSample, double gain)
    {
        if (!double.IsFinite(gain) || gain < 0f)
            throw new ArgumentOutOfRangeException(nameof(gain));
        if (bytesPerSample is < 1 or > 4 || source.Length % bytesPerSample != 0)
            throw new ArgumentException("Incorrect PCM sample format.", nameof(source));
        if (destination.Length < source.Length)
            throw new ArgumentException("Destination is too small.", nameof(destination));
        if (gain == 1f)
        {
            source.CopyTo(destination);
            return;
        }

        switch (bytesPerSample)
        {
            case 1:
                for (int i = 0; i < source.Length; i++)
                {
                    sbyte sample = unchecked((sbyte)source[i]);
                    sbyte scaled = (sbyte)Math.Clamp((double)sample * gain, sbyte.MinValue, sbyte.MaxValue);
                    destination[i] = unchecked((byte)scaled);
                }
                break;
            case 2:
                Apply16(MemoryMarshal.Cast<byte, short>(source), MemoryMarshal.Cast<byte, short>(destination), gain);
                break;
            case 3:
                for (int i = 0; i < source.Length; i += 3)
                {
                    int sample = (source[i] | source[i + 1] << 8 | source[i + 2] << 16) << 8 >> 8;
                    int scaled = (int)Math.Clamp((double)sample * gain, -8388608d, 8388607d);
                    destination[i] = (byte)scaled;
                    destination[i + 1] = (byte)(scaled >> 8);
                    destination[i + 2] = (byte)(scaled >> 16);
                }
                break;
            case 4:
                for (int i = 0; i < source.Length; i += 4)
                {
                    int sample = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(i, 4));
                    int scaled = (int)Math.Clamp((double)sample * gain, int.MinValue, int.MaxValue);
                    BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(i, 4), scaled);
                }
                break;
        }
    }

    private static void Apply16(ReadOnlySpan<short> source, Span<short> destination, double gain)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && gain <= float.MaxValue)
        {
            var gainVector = new Vector<float>((float)gain);
            var half = new Vector<float>(0.5f);
            var minimum = new Vector<float>(short.MinValue);
            var maximum = new Vector<float>(short.MaxValue);
            for (; i <= source.Length - Vector<short>.Count; i += Vector<short>.Count)
            {
                Vector.Widen(new Vector<short>(source.Slice(i)), out Vector<int> lo, out Vector<int> hi);
                // Clamp before converting back to integers so amplification cannot wrap or change sign.
                var loScaled = Vector.Clamp(Vector.ConvertToSingle(lo) * gainVector + half, minimum, maximum);
                var hiScaled = Vector.Clamp(Vector.ConvertToSingle(hi) * gainVector + half, minimum, maximum);
                Vector.Narrow(Vector.ConvertToInt32(Vector.Floor(loScaled)), Vector.ConvertToInt32(Vector.Floor(hiScaled)))
                    .CopyTo(destination.Slice(i));
            }
        }
        for (; i < source.Length; i++)
            destination[i] = (short)Math.Floor(Math.Clamp(source[i] * gain + 0.5d, short.MinValue, short.MaxValue));
    }
}
