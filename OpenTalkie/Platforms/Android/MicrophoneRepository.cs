using Android.Media;
using OpenTalkie.VBAN;
using static Android.Graphics.ImageDecoder;

namespace OpenTalkie.Platforms.Android;

public class MicrophoneRepository : IMicrophoneRepository
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
    public List<string> GetEncodings()
    {
        return ["8", "16", "24", "32"];
    }
    public string GetSelectedSource()
    {
        int source = Preferences.Get("MicrophoneSource", (int)AudioSource.Default);

        return ((AudioSource)source).ToString();
    }
    public string GetSelectedInputChannel()
    {
        int channel = Preferences.Get("MicrophoneChannel", (int)ChannelIn.Default);

        return ((ChannelIn)channel).ToString();
    }
    public string GetSelectedSampleRate()
    {
        return Preferences.Get("MicrophoneSampleRate", 48000).ToString();
    }
    public string GetSelectedEncoding()
    {
        var encoding = Preferences.Get("MicrophoneSource", (int)Encoding.Default);

        var converted = MapFromAndroidEncoding((Encoding)encoding);

        return converted.ToString();
    }
    public void SetSelectedSource(string source)
    {
        var parsedSource = Enum.Parse<AudioSource>(source);
        Preferences.Set("MicrophoneSource", (int)parsedSource);
    }
    public void SetSelectedInputChannel(string inputChannel)
    {
        var parsedInputChannel = Enum.Parse<ChannelIn>(inputChannel);
        Preferences.Set("MicrophoneSource", (int)parsedInputChannel);
    }
    public void SetSelectedSampleRate(string sampleRate)
    {
        Preferences.Set("MicrophoneSource", int.Parse(sampleRate));
    }
    public void SetSelectedEncoding(string encoding)
    {
        Preferences.Set("MicrophoneSource", (int)MapToAndroidEncoding(int.Parse(encoding)));
    }
    private static int MapFromAndroidEncoding(Encoding encoding)
    {
        return encoding switch
        {
            Encoding.Default => 16,
            Encoding.Pcm8bit => 8,
            Encoding.Pcm16bit => 16,
            Encoding.Pcm24bitPacked => 24,
            Encoding.Pcm32bit => 32,
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }
    private static Encoding MapToAndroidEncoding(int encoding)
    {
        return encoding switch
        {
            8 => Encoding.Pcm8bit,
            16 => Encoding.Pcm16bit,
            24 => Encoding.Pcm24bitPacked,
            32 => Encoding.Pcm32bit,
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }
}
