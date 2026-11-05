namespace OpenTalkie.Application.Abstractions.Services;

public interface IInputStream
{
    // Keep capture gain separate so each outgoing stream is scaled only once.
    float VolumeGain { get; }
    Task<int> ReadAsync(byte[] buffer, int offset, int count);
    WaveFormat GetWaveFormat();
}
