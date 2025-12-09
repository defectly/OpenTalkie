using Android.Content;
using global::Android.Media;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;

namespace OpenTalkie.Platforms.Android.Common.Services.Output;

public class AudioOutputService(IReceiverRepository receiverRepository) : IAudioOutputService
{
    private AudioTrack? _track;
    private int _sampleRate;
    private int _channels;

    public bool IsStarted => _track != null && _track.PlayState == PlayState.Playing;

    public void Start(int sampleRate, int channels)
    {
        channels = channels <= 1 ? 1 : 2; // support mono/stereo only

        if (_track != null && _sampleRate == sampleRate && _channels == channels)
        {
            if (_track.PlayState != PlayState.Playing) _track.Play();
            return;
        }

        Stop();

        var channelOut = channels == 1 ? ChannelOut.Mono : ChannelOut.Stereo;
        var encoding = Encoding.Pcm16bit;

        int minBuf = AudioTrack.GetMinBufferSize(sampleRate, channelOut, encoding);
        if (minBuf <= 0) minBuf = sampleRate * channels * 2; // fallback

        var attrs = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)
            .SetContentType(AudioContentType.Music)
            .Build();

        var format = new AudioFormat.Builder()
            .SetSampleRate(sampleRate)
            .SetEncoding(encoding)
            .SetChannelMask(channelOut)
            .Build();

        var builder = new AudioTrack.Builder()
            .SetAudioAttributes(attrs)
            .SetAudioFormat(format)
            .SetBufferSizeInBytes(minBuf);
        try
        {
            // Hint ultra low-latency path when available
            builder = builder.SetPerformanceMode(AudioTrackPerformanceMode.LowLatency);
        }
        catch { }
        _track = builder.Build();

        _sampleRate = sampleRate;
        _channels = channels;

        var prefferedOutputAudioDevice = receiverRepository.GetPrefferedDevice();

        if (!string.IsNullOrWhiteSpace(prefferedOutputAudioDevice))
            SetPrefferedAudioDevice(receiverRepository.GetPrefferedDevice()!);

        _track.Play();
    }

    public void Stop()
    {
        if (_track == null) return;
        try { if (_track.PlayState == PlayState.Playing) _track.Stop(); } catch { }
        _track.Release();
        _track.Dispose();
        _track = null;
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        if (_track == null) return;
        try { _track.Write(buffer, offset, count, WriteMode.Blocking); } catch { }
    }

    public void SetPrefferedAudioDevice(string prefferedDevice)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
            return;

        if(string.IsNullOrWhiteSpace(prefferedDevice) || _track is null)
            return;

        if(prefferedDevice == "Default")
        {
            _track.SetPreferredDevice(null);
            return;
        }

        bool parsedSuccessfully = Enum.TryParse(prefferedDevice, out AudioDeviceType audioDeviceType);

        if (!parsedSuccessfully)
            return;

        if (_track is null)
            return;

        try
        {
            var context = Platform.AppContext;
            var audioManager = (AudioManager?)context.GetSystemService(Context.AudioService);
            if (audioManager == null)
                return;

            var devices = audioManager.GetDevices(GetDevicesTargets.Outputs);
            if (devices is null)
                return;

            var foundPrefferedDevice = devices.FirstOrDefault(d => d.Type == audioDeviceType);

            if (foundPrefferedDevice == null)
                return;

            _track.SetPreferredDevice(foundPrefferedDevice);
        }
        catch
        {
        }
    }
}
