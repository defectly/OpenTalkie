namespace OpenTalkie;

public interface IParameterRepository
{
   List<string> GetAudioSources();
   List<string> GetInputChannels();
   List<string> GetSampleRates();
}