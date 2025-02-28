using Android.Media;
using OpenTalkie.VBAN;

namespace OpenTalkie.Platforms.Android;

public class ParameterRepository : IParameterRepository
{
    public List<string> GetSampleRates()
    {
        List<string> sampleRates = [];

        foreach (var item in VBANConsts.SAMPLERATES.Order())
            sampleRates.Add(item.ToString());

        return sampleRates;
    }

    public List<string> GetInputChannels()
    {
        List<string> inputChannels = [];

        foreach (var source in Enum.GetNames<ChannelIn>())
            inputChannels.Add(source.ToString());

        return inputChannels;
    }

    public List<string> GetAudioSources()
    {
        List<string> audioSources = [];

        foreach (var source in Enum.GetNames<AudioSource>())
            audioSources.Add(source.ToString());

        return audioSources;
    }

    public static int ConvertFromAndroidEncoding(Encoding encoding)
    {
        return encoding switch
        {
            Encoding.Pcm8bit => 8,
            Encoding.Pcm16bit => 16,
            Encoding.Pcm24bitPacked => 24,
            Encoding.Pcm32bit => 32,
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }
}
