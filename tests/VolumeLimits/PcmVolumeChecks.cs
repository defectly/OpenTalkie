using System.Runtime.InteropServices;
using OpenTalkie.Infrastructure.Audio;

static class PcmVolumeChecks
{
    public static void Run()
    {
        foreach (int bytesPerSample in new[] { 1, 2, 3, 4 })
        {
            int minimum = bytesPerSample == 4 ? int.MinValue : -(1 << (bytesPerSample * 8 - 1));
            int maximum = bytesPerSample == 4 ? int.MaxValue : (1 << (bytesPerSample * 8 - 1)) - 1;
            int[] samples = [minimum, minimum + 1, minimum / 2, -101, -3, -1, 0, 1, 3, 101, maximum / 2, maximum - 1, maximum];
            // Repeat enough samples to exercise SIMD as well as its tail.
            samples = Enumerable.Range(0, 83).Select(i => samples[i % samples.Length]).ToArray();
            byte[] source = Encode(samples, bytesPerSample);
            foreach (float gain in new[] { 0f, 0.001f, 0.5f, 1f, 1.25f, 2f, 4f, 8f, 1000f, float.MaxValue })
            {
                byte[] destination = Enumerable.Repeat((byte)0xCD, source.Length + 2).ToArray();
                PcmVolume.Apply(source, destination.AsSpan(1, source.Length), bytesPerSample, gain);
                Check(destination[0] == 0xCD && destination[^1] == 0xCD, "PCM write exceeded destination slice");
                int[] result = Decode(destination.AsSpan(1, source.Length), bytesPerSample);
                for (int i = 0; i < samples.Length; i++)
                {
                    double expected = (double)samples[i] * gain;
                    if (bytesPerSample == 2 && gain != 1f)
                        expected = Math.Floor(expected + 0.5d);
                    expected = Math.Clamp(expected, minimum, maximum);
                    long difference = Math.Abs((long)result[i] - (int)expected);
                    Check(difference <= (bytesPerSample == 2 ? 1 : 0),
                        $"{bytesPerSample * 8}-bit PCM: {samples[i]} × {gain}: expected {(int)expected}, got {result[i]}");
                }
                byte[] inPlace = (byte[])source.Clone();
                PcmVolume.Apply(inPlace, inPlace, bytesPerSample, gain);
                Check(inPlace.AsSpan().SequenceEqual(destination.AsSpan(1, source.Length)), "In-place PCM differs");
            }
        }

        short[] fullRange = Enumerable.Range(short.MinValue, 65536).Select(x => (short)x).ToArray();
        foreach (float gain in new[] { 0f, 0.5f, 0.99999f, 1f, 1.25f, 2f, 4f, 8f, float.MaxValue })
        {
            byte[] output = new byte[fullRange.Length * 2];
            PcmVolume.Apply(MemoryMarshal.AsBytes(fullRange.AsSpan()), output, 2, gain);
            var result = MemoryMarshal.Cast<byte, short>(output);
            for (int i = 0; i < fullRange.Length; i++)
            {
                int expected = (int)Math.Clamp(Math.Floor(fullRange[i] * (double)gain + 0.5d), short.MinValue, short.MaxValue);
                Check(Math.Abs(result[i] - expected) <= 1, $"16-bit full range mismatch at {fullRange[i]} × {gain}");
            }
        }
        foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            bool rejected = false;
            try { PcmVolume.Apply(new byte[2], new byte[2], 2, invalid); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid PCM gain accepted");
        }
        Console.WriteLine("PCM checks passed: 8/16/24/32-bit saturation, full 16-bit range, SIMD/tails, in-place and extreme gains.");
    }

    static byte[] Encode(int[] samples, int width)
    {
        byte[] bytes = new byte[samples.Length * width];
        for (int i = 0; i < samples.Length; i++)
            for (int b = 0; b < width; b++)
                bytes[i * width + b] = (byte)(samples[i] >> (b * 8));
        return bytes;
    }

    static int[] Decode(ReadOnlySpan<byte> bytes, int width)
    {
        int[] samples = new int[bytes.Length / width];
        for (int i = 0; i < samples.Length; i++)
        {
            int sample = 0;
            for (int b = 0; b < width; b++) sample |= bytes[i * width + b] << (b * 8);
            samples[i] = sample << (32 - width * 8) >> (32 - width * 8);
        }
        return samples;
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
