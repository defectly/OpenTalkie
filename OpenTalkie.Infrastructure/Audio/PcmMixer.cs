using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenTalkie.Infrastructure.Audio;

public static class PcmMixer
{
    public static void Add16(ReadOnlySpan<byte> source, Span<double> mix, float gain)
    {
        if (!float.IsFinite(gain) || gain < 0f)
            throw new ArgumentOutOfRangeException(nameof(gain));
        if (source.Length % 2 != 0 || mix.Length < source.Length / 2)
            throw new ArgumentException("Incorrect PCM or mix buffer size.");

        var samples = MemoryMarshal.Cast<byte, short>(source);
        int i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var gainVector = new Vector<double>(gain);
            int width = Vector<double>.Count;
            for (; i <= samples.Length - Vector<short>.Count; i += Vector<short>.Count)
            {
                Vector.Widen(new Vector<short>(samples.Slice(i)), out Vector<int> lo, out Vector<int> hi);
                Vector.Widen(Vector.ConvertToSingle(lo), out Vector<double> a, out Vector<double> b);
                Vector.Widen(Vector.ConvertToSingle(hi), out Vector<double> c, out Vector<double> d);
                (new Vector<double>(mix.Slice(i)) + a * gainVector).CopyTo(mix.Slice(i));
                (new Vector<double>(mix.Slice(i + width)) + b * gainVector).CopyTo(mix.Slice(i + width));
                (new Vector<double>(mix.Slice(i + width * 2)) + c * gainVector).CopyTo(mix.Slice(i + width * 2));
                (new Vector<double>(mix.Slice(i + width * 3)) + d * gainVector).CopyTo(mix.Slice(i + width * 3));
            }
        }
        for (; i < samples.Length; i++)
            mix[i] += (double)samples[i] * gain;
    }

    public static void Write16(ReadOnlySpan<double> mix, Span<byte> destination, float globalGain)
    {
        if (!float.IsFinite(globalGain) || globalGain < 0f)
            throw new ArgumentOutOfRangeException(nameof(globalGain));
        if (destination.Length < mix.Length * 2)
            throw new ArgumentException("Destination is too small.", nameof(destination));

        var samples = MemoryMarshal.Cast<byte, short>(destination);
        for (int i = 0; i < mix.Length; i++)
            samples[i] = (short)Math.Floor(Math.Clamp(mix[i] * globalGain + 0.5d, short.MinValue, short.MaxValue));
    }
}
