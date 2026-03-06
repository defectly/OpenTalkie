namespace OpenTalkie.Application.Abstractions.Services;

public interface IInputStream
{
    Task<int> ReadAsync(byte[] buffer, int offset, int count);
    WaveFormat GetWaveFormat();
}

