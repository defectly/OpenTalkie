#if ANDROID
using Android.Media;
using NAudio.Wave;

namespace vOpenTalkie;

public class WaveAudioRecord : IWaveProvider
{
    private AudioRecord _audioRecord;
    private WaveFormat _waveFormat;
    public WaveFormat WaveFormat => _waveFormat;
    public int BufferSize => _audioRecord.BufferSizeInFrames;

    DoggyDenoiser _denoiser;

    public WaveAudioRecord(WaveFormat waveFormat, AudioRecord audioRecord)
    {
        _waveFormat = waveFormat;
        _audioRecord = audioRecord;

        _denoiser = new DoggyDenoiser();
    }

    public int Read(byte[] buffer, int offset, int count) =>
        MainPage.UseRNNoise == true ? ReadRNNoise(buffer, offset, count) : JustRead(buffer, offset, count);

    private int JustRead(byte[] buffer, int offset, int count) =>
        _audioRecord.Read(buffer, offset, count);

    private int ReadRNNoise(byte[] buffer, int offset, int count)
    {
        int length = _audioRecord.Read(buffer, offset, count);

        float[] rnBuffer = Convert16BitToFloat(buffer.Take(length).ToArray());

        int denoisedLength = _denoiser.Denoise(rnBuffer, false);

        buffer = ConvertFloatTo16Bit(rnBuffer.Take(denoisedLength).ToArray());

        return buffer.Length;
    }

    public static float[] Convert16BitToFloat(byte[] input)
    {
        // 16 bit input, so 2 bytes per sample
        int inputSamples = input.Length / 2;
        float[] output = new float[inputSamples];
        int outputIndex = 0;
        for (int n = 0; n < inputSamples; n++)
        {
            short sample = BitConverter.ToInt16(input, n * 2);
            output[outputIndex++] = sample / 32768f;
        }
        return output;
    }

    public static byte[] ConvertFloatTo16Bit(float[] samples)
    {
        int samplesCount = samples.Length;
        var pcm = new byte[samplesCount * 2];
        int sampleIndex = 0, pcmIndex = 0;

        while (sampleIndex < samplesCount)
        {
            var outsample = (short)(samples[sampleIndex] * short.MaxValue);
            pcm[pcmIndex] = (byte)(outsample & 0xff);
            pcm[pcmIndex + 1] = (byte)((outsample >> 8) & 0xff);

            sampleIndex++;
            pcmIndex += 2;
        }

        return pcm;
    }

    public void Stop() => _audioRecord.Stop();
    public void Start() => _audioRecord.StartRecording();
}
#endif