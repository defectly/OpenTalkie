using Android.Media;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.VBAN;

namespace OpenTalkie.Platforms.Android.Common.Repositories;

public class MicrophoneRepository : IMicrophoneRepository
{
    public Action<float> VolumeChanged { get; set; }

    public List<string> GetSampleRates()
    {
        List<string> sampleRates = [];

        var rates = VBANConsts.SAMPLERATES.Order().ToArray();
        for (int i = 0; i < rates.Length; i++)
            sampleRates.Add(rates[i].ToString());

        return sampleRates;
    }
    public List<string> GetInputChannels()
    {
        List<string> inputChannels = [];

        var ins = Enum.GetNames<ChannelIn>();
        for (int i = 0; i < ins.Length; i++)
            inputChannels.Add(ins[i].ToString());

        return inputChannels;
    }
    public List<string> GetAudioSources()
    {
        List<string> audioSources = [];

        var srcs = Enum.GetNames<AudioSource>();
        for (int i = 0; i < srcs.Length; i++)
            audioSources.Add(srcs[i].ToString());

        return audioSources;
    }
    public List<string> GetEncodings()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
            return ["8", "16", "24", "32"];
        else
            return ["8", "16"];
    }
    public string GetSelectedSource()
    {
        int source = Preferences.Get("MicrophoneSource", (int)AudioSource.Default);

        return ((AudioSource)source).ToString();
    }
    public string GetSelectedInputChannel()
    {
        int channel = Preferences.Get("MicrophoneInputChannel", (int)ChannelIn.Default);

        return ((ChannelIn)channel).ToString();
    }
    public string GetSelectedSampleRate()
    {
        return Preferences.Get("MicrophoneSampleRate", 48000).ToString();
    }
    public string GetSelectedEncoding()
    {
        var encoding = Preferences.Get("MicrophoneEncoding", (int)Encoding.Default);

        var converted = MapFromAndroidEncoding((Encoding)encoding);

        return converted.ToString();
    }
    public string GetSelectedBufferSize()
    {
        var bufferSize = Preferences.Get("MicrophoneBufferSize", 960);

        return bufferSize.ToString();
    }
    public void SetSelectedBufferSize(string bufferSize)
    {
        Preferences.Set("MicrophoneBufferSize", int.Parse(bufferSize));
    }
    public void SetSelectedSource(string source)
    {
        var parsedSource = Enum.Parse<AudioSource>(source);
        Preferences.Set("MicrophoneSource", (int)parsedSource);
    }
    public void SetSelectedInputChannel(string inputChannel)
    {
        var parsedInputChannel = Enum.Parse<ChannelIn>(inputChannel);
        Preferences.Set("MicrophoneInputChannel", (int)parsedInputChannel);
    }
    public void SetSelectedSampleRate(string sampleRate)
    {
        Preferences.Set("MicrophoneSampleRate", int.Parse(sampleRate));
    }
    public void SetSelectedEncoding(string encoding)
    {
        Preferences.Set("MicrophoneEncoding", (int)MapToAndroidEncoding(int.Parse(encoding)));
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
            Encoding.Invalid => 16,
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }
    private static Encoding MapToAndroidEncoding(int encoding)
    {
        return encoding switch
        {
            8 => Encoding.Pcm8bit,
            16 => Encoding.Pcm16bit,
            24 => OperatingSystem.IsAndroidVersionAtLeast(31) ? Encoding.Pcm24bitPacked : throw new NotSupportedException($"Pcm24bitPacked supported on sdk 31 or higher"),
            32 => OperatingSystem.IsAndroidVersionAtLeast(31) ? Encoding.Pcm32bit : throw new NotSupportedException($"Pcm32bit supported on sdk 31 or higher"),
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }

    public float GetSelectedVolume()
    {
        return Preferences.Get("MicrophoneVolume", 1f);
    }

    public void SetSelectedVolume(float gain)
    {
        Preferences.Set("MicrophoneVolume", gain);
        VolumeChanged?.Invoke(gain);
    }
}
