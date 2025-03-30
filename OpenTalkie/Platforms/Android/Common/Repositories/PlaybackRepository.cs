using Android.Media;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.VBAN;

namespace OpenTalkie.Platforms.Android.Common.Repositories;

public class PlaybackRepository : IPlaybackRepository
{
    public List<string> GetSampleRates()
    {
        List<string> sampleRates = [];

        foreach (var item in VBANConsts.SAMPLERATES.Order())
            sampleRates.Add(item.ToString());

        return sampleRates;
    }
    public List<string> GetOutputChannels()
    {
        List<string> inputChannels = [];

        foreach (var source in Enum.GetNames<ChannelOut>())
            inputChannels.Add(source.ToString());

        return inputChannels;
    }
    public List<string> GetEncodings()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
            return ["8", "16", "24", "32"];
        else
            return ["8", "16"];
    }

    public string GetSelectedBufferSize()
    {
        var encoding = Preferences.Get("PlaybackBufferSize", 1920);

        return 1920.ToString();
    }
    public void SetSelectedBufferSize(string bufferSize)
    {
        Preferences.Set("PlaybackBufferSize", int.Parse(bufferSize));
    }

    public string GetSelectedSampleRate()
    {
        return Preferences.Get("PlaybackSampleRate", 48000).ToString();
    }
    public void SetSelectedSampleRate(string sampleRate)
    {
        Preferences.Set("PlaybackSampleRate", int.Parse(sampleRate));
    }
    public string GetSelectedChannelOut()
    {
        var encoding = Preferences.Get("PlaybackChannelOut", (int)ChannelOut.Stereo);

        return ((ChannelOut)encoding).ToString();
    }
    public void SetSelectedChannelOut(string encoding)
    {
        Preferences.Set("PlaybackChannelOut", (int)Enum.Parse<ChannelOut>(encoding));
    }
    public void SetSelectedEncoding(string encoding)
    {
        Preferences.Set("PlaybackEncoding", (int)MapToAndroidEncoding(int.Parse(encoding)));
    }
    public string GetSelectedEncoding()
    {
        var encoding = Preferences.Get("PlaybackEncoding", (int)Encoding.Default);

        var converted = MapFromAndroidEncoding((Encoding)encoding);

        return converted.ToString();
    }
    private int MapFromAndroidEncoding(Encoding encoding)
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
    private Encoding MapToAndroidEncoding(int encoding)
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
