using NAudio.Wave;
using OpenTalkie.RNNoise;

namespace OpenTalkie;

internal class DenoisedSampleProvider : ISampleProvider
{
    private Denoiser _denoiser;
    private ISampleProvider _sample;

    public WaveFormat WaveFormat => _sample.WaveFormat;

    public DenoisedSampleProvider(ISampleProvider sample) =>
        (_sample, _denoiser) = (sample, new());

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _sample.Read(buffer, 0, count);

        int denoised = _denoiser.Denoise(buffer);

        return denoised;
    }
}
