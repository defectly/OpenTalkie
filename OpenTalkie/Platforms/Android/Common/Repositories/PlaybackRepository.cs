using Android.Media;
using OpenTalkie.Common.Repositories.Interfaces;

namespace OpenTalkie.Platforms.Android;

public class PlaybackRepository : IPlaybackRepository
{
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
        var encoding = Preferences.Get("PlaybackChannelOut", (int)ChannelOut.Default);

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
