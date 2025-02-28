using NAudio.Wave;

namespace OpenTalkie;

public interface IMicrophoneService : IDisposable
{
    int BufferSize { get; set; }

    int Read(byte[] buffer, int offset, int count);
    Task<int> ReadAsync(byte[] buffer, int offset, int count);
    ISampleProvider ToSampleProvider();
    IWaveProvider ToWaveProvider();
    List<(string Name, string Parameter)> GetPreferencesAsString();
}