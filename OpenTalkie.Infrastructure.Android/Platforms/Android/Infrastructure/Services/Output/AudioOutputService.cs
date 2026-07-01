using Android.Content;
using Android.Media;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Output;

public class AudioOutputService(IReceiverRepository receiverRepository, ILogger<AudioOutputService> logger) : IAudioOutputService
{
    private AudioTrack? _track;
    private int _sampleRate;
    private int _channels;

    public bool IsStarted => _track != null && _track.PlayState == PlayState.Playing;

    public void Start(int sampleRate, int channels)
    {
        channels = channels <= 1 ? 1 : 2;

        if (_track != null && _sampleRate == sampleRate && _channels == channels)
        {
            if (_track.PlayState != PlayState.Playing)
            {
                _track.Play();
                logger.LogInformation("Receiver AudioTrack resumed.");
            }
            return;
        }

        Stop();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Starting receiver AudioTrack at {SampleRate} Hz with {Channels} channel(s).", sampleRate, channels);
        }

        var channelOut = channels == 1 ? ChannelOut.Mono : ChannelOut.Stereo;
        var encoding = Encoding.Pcm16bit;

        int minBuf = AudioTrack.GetMinBufferSize(sampleRate, channelOut, encoding);
        if (minBuf <= 0) minBuf = sampleRate * channels * 2;

        var attrsBuilder = new AudioAttributes.Builder();
        attrsBuilder = attrsBuilder.SetUsage(AudioUsageKind.Media) ?? throw new InvalidOperationException("Could not configure audio usage.");
        attrsBuilder = attrsBuilder.SetContentType(AudioContentType.Music) ?? throw new InvalidOperationException("Could not configure audio content type.");
        var attrs = attrsBuilder.Build() ?? throw new InvalidOperationException("Could not build audio attributes.");

        var formatBuilder = new AudioFormat.Builder();
        formatBuilder = formatBuilder.SetSampleRate(sampleRate) ?? throw new InvalidOperationException("Could not configure sample rate.");
        formatBuilder = formatBuilder.SetEncoding(encoding) ?? throw new InvalidOperationException("Could not configure audio encoding.");
        formatBuilder = formatBuilder.SetChannelMask(channelOut) ?? throw new InvalidOperationException("Could not configure output channels.");
        var format = formatBuilder.Build() ?? throw new InvalidOperationException("Could not build audio format.");

        var builder = new AudioTrack.Builder()
            .SetAudioAttributes(attrs) ?? throw new InvalidOperationException("Could not configure audio attributes.");
        builder = builder.SetAudioFormat(format) ?? throw new InvalidOperationException("Could not configure audio format.");
        builder = builder.SetBufferSizeInBytes(minBuf) ?? throw new InvalidOperationException("Could not configure audio buffer size.");
        try
        {
            builder = builder.SetPerformanceMode(AudioTrackPerformanceMode.LowLatency) ?? builder;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "AudioTrack low-latency performance mode was not accepted.");
        }
        _track = builder.Build() ?? throw new InvalidOperationException("Could not build audio track.");

        _sampleRate = sampleRate;
        _channels = channels;

        var preferredOutputAudioDevice = receiverRepository.GetSettings().PreferredAudioOutputDevice.Value;
        if (!string.IsNullOrWhiteSpace(preferredOutputAudioDevice))
            SetPreferredAudioDevice(preferredOutputAudioDevice);

        _track.Play();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Receiver AudioTrack started. MinBufferBytes={BufferSize}.", minBuf);
    }

    public void Stop()
    {
        if (_track == null) return;
        try { if (_track.PlayState == PlayState.Playing) _track.Stop(); } catch (Exception ex) { logger.LogWarning(ex, "Receiver AudioTrack stop failed."); }
        _track.Release();
        _track.Dispose();
        _track = null;
        logger.LogInformation("Receiver AudioTrack stopped.");
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        if (_track == null) return;
        try { _track.Write(buffer, offset, count, WriteMode.Blocking); } catch (Exception ex) { logger.LogWarning(ex, "Receiver AudioTrack write failed."); }
    }

    public void SetPrefferedAudioDevice(string prefferedDevice) => SetPreferredAudioDevice(prefferedDevice);

    private bool SetPreferredAudioDevice(string preferredDevice)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            logger.LogWarning("Preferred receiver output device ignored because Android version is below 23.");
            return false;
        }

        var context = Platform.AppContext;
        var audioManager = (AudioManager?)context.GetSystemService(Context.AudioService);
        if (audioManager is null)
        {
            logger.LogWarning("Preferred receiver output device ignored because AudioManager is unavailable.");
            return false;
        }

        if (preferredDevice.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            _track?.SetPreferredDevice(null);

            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                audioManager.ClearCommunicationDevice();
            }
            else
            {
                audioManager.BluetoothScoOn = false;
                audioManager.StopBluetoothSco();
            }

            audioManager.Mode = Mode.Normal;
            logger.LogInformation("Receiver preferred output device reset to default.");
            return true;
        }

        if (!Enum.TryParse<AudioDeviceType>(preferredDevice, ignoreCase: true, out var wantedType))
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("Preferred receiver output device '{PreferredDevice}' is not recognized.", preferredDevice);
            }

            return false;
        }

        audioManager.Mode = Mode.InCommunication;

        AudioDeviceInfo? target = null;

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var commDevices = audioManager.AvailableCommunicationDevices;
            target = commDevices?.FirstOrDefault(d => d.Type == wantedType);
            if (target is null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Preferred receiver communication output device '{PreferredDevice}' was not found.", preferredDevice);
                }

                return false;
            }

            var ok = audioManager.SetCommunicationDevice(target);
            if (!ok)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Preferred receiver communication output device '{PreferredDevice}' was rejected by Android.", preferredDevice);

                return false;
            }
        }
        else
        {
            var inputs = audioManager.GetDevices(GetDevicesTargets.Outputs);
            target = inputs?.FirstOrDefault(d => d.Type == wantedType);
            if (target is null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Preferred receiver output device '{PreferredDevice}' was not found.", preferredDevice);

                return false;
            }

            if (wantedType == AudioDeviceType.BluetoothSco)
            {
                audioManager.StartBluetoothSco();
                audioManager.BluetoothScoOn = true;
            }
            else
            {
                audioManager.BluetoothScoOn = false;
                audioManager.StopBluetoothSco();
            }
        }

        _track?.SetPreferredDevice(target);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Receiver preferred output device set to {PreferredDevice}.", preferredDevice);

        return true;
    }
}
