using System.Runtime.InteropServices;
using OpenTalkie.Infrastructure.Audio;

static class AudioPipelineChecks
{
    public static void Run()
    {
        foreach (int width in new[] { 1, 2, 3, 4 })
        {
            int amplitude = width == 4 ? 1600000000 : (1 << (width * 8 - 2)) + 3;
            byte[] source = Encode([amplitude, -amplitude, 0], width);
            byte[] output = new byte[source.Length];
            PcmVolume.Apply(source, output, width, (double)2f * 0.5f);
            Check(output.SequenceEqual(source), $"{width * 8}-bit sender: boost and attenuation must cancel without clipping");
            byte[] first = new byte[source.Length];
            byte[] second = new byte[source.Length];
            PcmVolume.Apply(source, first, width, (double)2f * 0.25f);
            PcmVolume.Apply(source, second, width, (double)2f * 0.5f);
            Check(!first.SequenceEqual(second) && second.SequenceEqual(source), "Outgoing streams remain independent");
            Check(source.SequenceEqual(Encode([amplitude, -amplitude, 0], width)), "Scaling must not modify the captured source");
            PcmVolume.Apply(source, output, width, (double)float.MaxValue * float.MaxValue);
            Check(output.Skip(width * 2).All(x => x == 0), "Extreme combined gains preserve silence");
            PcmVolume.Apply(source, output, width, (double)0f * float.MaxValue);
            Check(output.All(x => x == 0), "Global mute with extreme stream gain");
        }

        // 83 samples exercises both vector blocks and the scalar remainder.
        short[] signal = Enumerable.Range(0, 83).Select(i => (short)(i % 2 == 0 ? 24000 : -24000)).ToArray();
        byte[] pcm = MemoryMarshal.AsBytes(signal.AsSpan()).ToArray();
        double[] mix = new double[signal.Length];
        byte[] result = new byte[pcm.Length];
        PcmMixer.Add16(pcm, mix, 2f);
        Check(mix[0] == 48000d && mix[1] == -48000d, "Boosted stream is retained above PCM range");
        PcmMixer.Write16(mix, result, 0.5f);
        Check(result.SequenceEqual(pcm), "Receiver boost and global attenuation cancel");

        Array.Clear(mix);
        PcmMixer.Add16(pcm, mix, 1f);
        PcmMixer.Add16(pcm, mix, 1f);
        PcmMixer.Write16(mix, result, 0.5f);
        Check(result.SequenceEqual(pcm), "Two loud streams are summed before global attenuation");
        byte[] opposite = MemoryMarshal.AsBytes(signal.Select(x => (short)-x).ToArray().AsSpan()).ToArray();
        PcmMixer.Add16(opposite, mix, 1f);
        PcmMixer.Write16(mix, result, 1f);
        Check(result.SequenceEqual(pcm), "Cancellation after a loud sum preserves the original signal");
        double[] reverseOrder = new double[mix.Length];
        PcmMixer.Add16(opposite, reverseOrder, 1f);
        PcmMixer.Add16(pcm, reverseOrder, 1f);
        PcmMixer.Add16(pcm, reverseOrder, 1f);
        Check(reverseOrder.SequenceEqual(mix), "Mixing these sources is independent of order");

        Array.Clear(mix);
        PcmMixer.Add16(pcm, mix, 0.5f);
        PcmMixer.Add16(opposite, mix, 0.25f);
        PcmMixer.Write16(mix, result, 1f);
        Check(MemoryMarshal.Cast<byte, short>(result)[0] == 6000, "Incoming streams retain separate gains");
        Array.Clear(mix);
        PcmMixer.Add16(pcm, mix, float.MaxValue);
        PcmMixer.Add16(opposite, mix, float.MaxValue);
        PcmMixer.Write16(mix, result, float.MaxValue);
        Check(result.All(x => x == 0), "Extreme opposing stream gains cancel without overflow or NaN");
        PcmMixer.Add16(pcm, mix, 2f);
        PcmMixer.Write16(mix, result, 1f);
        var saturated = MemoryMarshal.Cast<byte, short>(result);
        Check(saturated[0] == short.MaxValue && saturated[1] == short.MinValue, "Only the final PCM output is saturated");
        Check(pcm.SequenceEqual(MemoryMarshal.AsBytes(signal.AsSpan()).ToArray()), "Mixing does not alter source buffers");
        Console.WriteLine("Audio pipeline checks passed: gain compensation, independent streams, unclipped mixing, cancellation and final saturation.");
    }

    static byte[] Encode(int[] samples, int width)
    {
        byte[] output = new byte[samples.Length * width];
        for (int i = 0; i < samples.Length; i++)
            for (int b = 0; b < width; b++) output[i * width + b] = (byte)(samples[i] >> (b * 8));
        return output;
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
