namespace OpenTalkie;

public class WaveFormat
{
    public int BitsPerSample { get; set; }
    public int Channels { get; set; }
    public int SampleRate { get; set; }

    public WaveFormat(int bitsPerSample, int channels, int sampleRate)
    {
        BitsPerSample = bitsPerSample;
        Channels = channels;
        SampleRate = sampleRate;
    }
}
