namespace OpenTalkie.Common.Services.Interfaces;

public interface IInputStream
{
    Task<int> ReadAsync(byte[] buffer, int offset, int count);
    WaveFormat GetWaveFormat();
}
