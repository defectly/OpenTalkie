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

        return encoding.ToString();
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
        return Preferences.Get("PlaybackVolume", 1f);
    }

    public void SetSelectedVolume(float gain)
    {
        Preferences.Set("PlaybackVolume", gain);
    }
}
