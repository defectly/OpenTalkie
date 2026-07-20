using Android.Content;
using Android.Media;
using OpenTalkie.Application.Abstractions.Repositories;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Microphone;

public static class MicrophoneAudioRecord
{
    private static readonly Lock _repoLock = new();
    private static IMicrophoneRepository? _microphoneRepository;
    private static IAudioManagerSettingsRepository? _audioManagerSettingsRepository;
    private static bool _volumeSubscriptionAttached;
    private static float _volume;
    private static AudioRecord? _audioRecord;
    private static int _microphoneSource;
    private static int _microphoneChannel;
    private static int _microphoneSampleRate;
    private static int _microphoneEncoding;
    private static WaveFormat? _waveFormat;
    [ThreadStatic]
    private static bool _realtimePriorityConfigured;

    public static int BufferSize { get; set; }

    public static void Configure(
        IMicrophoneRepository microphoneRepository,
        IAudioManagerSettingsRepository audioManagerSettingsRepository)
    {
        lock (_repoLock)
        {
            if (ReferenceEquals(_microphoneRepository, microphoneRepository)
                && ReferenceEquals(_audioManagerSettingsRepository, audioManagerSettingsRepository))
            {
                return;
            }

            if (_microphoneRepository != null && _volumeSubscriptionAttached)
                _microphoneRepository.VolumeChanged -= OnVolumeChange;

            _microphoneRepository = microphoneRepository;
            _audioManagerSettingsRepository = audioManagerSettingsRepository;
            _microphoneRepository.VolumeChanged += OnVolumeChange;
            _volumeSubscriptionAttached = true;
        }
    }

    public static void Start()
    {
        if (_audioRecord != null)
            return;

        var microphoneRepository = GetMicrophoneRepository();
        LoadPreferences();

        CreateAudioRecord();

        if (_audioRecord == null || _audioRecord.State == State.Uninitialized)
        {
            _audioRecord = null;
            throw new Exception("Can't initialize audio record.. Selected parameters may be not supported");
        }

        var preferredOutputAudioDevice = microphoneRepository.GetSettings().PreferredAudioInputDevice.Value;

        if (!string.IsNullOrWhiteSpace(preferredOutputAudioDevice))
            SetPreferredAudioDevice(preferredOutputAudioDevice);

        _audioRecord.StartRecording();
    }

    public static WaveFormat GetWaveFormat()
    {
        if (_audioRecord == null)
            throw new NullReferenceException("Audio record is null");

        var microphoneRepository = GetMicrophoneRepository();
        return _waveFormat ??=
            new WaveFormat(int.Parse(microphoneRepository.GetSettings().SelectedEncoding.Value), _audioRecord.ChannelCount, _audioRecord.SampleRate);
    }

    public static void Stop()
    {
        if (_audioRecord == null)
            return;

        try
        {
            if (_audioRecord.RecordingState != RecordState.Stopped)
                _audioRecord.Stop();
        }
        catch { }
        finally
        {
            try { _audioRecord.Release(); } catch { }
            _audioRecord.Dispose();
            _audioRecord = null;
            _waveFormat = null;
        }
    }

    public static Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (_audioRecord == null)
            throw new NullReferenceException("Audio record is null");

        ConfigureCurrentThreadPriority();
        int read = _audioRecord.Read(buffer, offset, count);

        if (read > 0)
            ChangeVolume(buffer, offset, read, _volume);

        return Task.FromResult(read);
    }

    public static unsafe void ChangeVolume(byte[] audioBytes, int offset, int length, float gain)
    {
        if (audioBytes == null || length % 2 != 0)
            throw new ArgumentException("Incorrect audio data format");

        if (Math.Abs(gain - 1f) < 0.0001f) return;

        fixed (byte* basePtr = audioBytes)
        {
            byte* ptr = basePtr + offset;
            short* samples = (short*)ptr;
            int sampleCount = length / 2;

            for (int i = 0; i < sampleCount; i++)
            {
                float amplified = samples[i] * gain;
                samples[i] = amplified > short.MaxValue ? short.MaxValue :
                             amplified < short.MinValue ? short.MinValue :
                             (short)amplified;
            }
        }
    }

    private static void LoadPreferences()
    {
        var settings = GetMicrophoneRepository().GetSettings();
        _microphoneSource = (int)Enum.Parse<AudioSource>(settings.SelectedSource.Value);
        _microphoneChannel = (int)Enum.Parse<ChannelIn>(settings.SelectedInputChannel.Value);
        _microphoneSampleRate = int.Parse(settings.SelectedSampleRate.Value);
        _microphoneEncoding = (int)MapToAndroidEncoding(int.Parse(settings.SelectedEncoding.Value));
        BufferSize = settings.SelectedBufferSize;
        _volume = settings.VolumeGain;
    }

    private static void CreateAudioRecord()
    {
        CreateAudioRecord(
            (AudioSource)_microphoneSource,
            _microphoneSampleRate,
            (ChannelIn)_microphoneChannel,
            (Encoding)_microphoneEncoding,
            BufferSize);
    }

    private static void CreateAudioRecord(AudioSource audioSource, int sampleRate, ChannelIn channel, Encoding encoding, int bufferSize)
    {
        _audioRecord = new(audioSource, sampleRate, channel, encoding, bufferSize);
    }

    private static void OnVolumeChange(float gain)
    {
        _volume = gain;
    }

    private static IMicrophoneRepository GetMicrophoneRepository()
    {
        return _microphoneRepository 
            ?? throw new InvalidOperationException("Microphone repository service is unavailable.");
    }

    public static void SetPreferredAudioDevice(string preferredDevice)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
            return;

        var context = Platform.AppContext;
        var audioManager = (AudioManager?)context.GetSystemService(Context.AudioService);
        if (audioManager is null)
            return;

        if (preferredDevice.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            _audioRecord?.SetPreferredDevice(null);

            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                audioManager.ClearCommunicationDevice();
            }
            else
            {
                audioManager.BluetoothScoOn = false;
                audioManager.StopBluetoothSco();
            }

            audioManager.Mode = GetConfiguredAudioManagerMode();
            return;
        }

        if (!Enum.TryParse<AudioDeviceType>(preferredDevice, ignoreCase: true, out var wantedType))
            return;

        audioManager.Mode = GetConfiguredAudioManagerMode();

        AudioDeviceInfo? target = null;

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var commDevices = audioManager.AvailableCommunicationDevices;
            target = commDevices?.FirstOrDefault(d => d.Type == wantedType);
            if (target is null)
                return;

            var ok = audioManager.SetCommunicationDevice(target);
            if (!ok)
                return;
        }
        else
        {
            var inputs = audioManager.GetDevices(GetDevicesTargets.Inputs);
            target = inputs?.FirstOrDefault(d => d.Type == wantedType);
            if (target is null)
                return;

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

        _audioRecord?.SetPreferredDevice(target);
    }

    private static Encoding MapToAndroidEncoding(int encoding)
    {
        return encoding switch
        {
            8 => Encoding.Pcm8bit,
            16 => Encoding.Pcm16bit,
            24 => OperatingSystem.IsAndroidVersionAtLeast(31) ? Encoding.Pcm24bitPacked : throw new NotSupportedException("Pcm24bitPacked supported on sdk 31 or higher"),
            32 => OperatingSystem.IsAndroidVersionAtLeast(31) ? Encoding.Pcm32bit : throw new NotSupportedException("Pcm32bit supported on sdk 31 or higher"),
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }

    private static Mode GetConfiguredAudioManagerMode()
    {
        var settings = _audioManagerSettingsRepository?.GetSettings()
            ?? throw new InvalidOperationException("Audio manager settings repository service is unavailable.");

        return Enum.Parse<Mode>(settings.SelectedMode.Value);
    }

    public static void ConfigureCurrentThreadPriority()
    {
        if (_realtimePriorityConfigured)
            return;

        try
        {
            global::Android.OS.Process.SetThreadPriority(global::Android.OS.ThreadPriority.UrgentAudio);
            _realtimePriorityConfigured = true;
        }
        catch (Java.Lang.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unable to set urgent audio thread priority: {ex.Message}");
        }
    }
}
