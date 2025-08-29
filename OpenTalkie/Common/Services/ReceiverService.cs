using AutoMapper;
using OpenTalkie.Common.Dto;
using OpenTalkie.Common.Enums;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace OpenTalkie.Common.Services;

public class ReceiverService
{
    private readonly IMapper _mapper;
    private readonly IEndpointRepository _endpointRepository;
    private readonly IAudioOutputService _audioOutput;
    private AsyncReceiver? _receiver;

    public ObservableCollection<Endpoint> Endpoints { get; }
    public bool ListeningState { get; private set; }
    public Action<bool>? ListeningStateChanged;

    public ReceiverService(IEndpointRepository endpointRepository, IMapper mapper, IAudioOutputService audioOutput)
    {
        _endpointRepository = endpointRepository;
        _mapper = mapper;
        _audioOutput = audioOutput;

        Endpoints = mapper.Map<ObservableCollection<Endpoint>>(
            _endpointRepository.List().Where(e => e.Type == EndpointType.Receiver));
        Endpoints.CollectionChanged += EndpointsCollectionChanged;
        for (int i = 0; i < Endpoints.Count; i++)
            Endpoints[i].PropertyChanged += EndpointPropertyChanged;

        ListeningStateChanged += OnListeningStateChange;
    }

    public void Start()
    {
        if (ListeningState) return;
        _receiver ??= new AsyncReceiver(Endpoints);
        _receiver.FrameReceived += OnFrameReceived;
        _receiver.Start();
        ListeningStateChanged?.Invoke(true);
    }

    public void Stop()
    {
        if (!ListeningState) return;
        _receiver!.FrameReceived -= OnFrameReceived;
        _receiver.Stop();
        _receiver = null;
        _audioOutput.Stop();
        ListeningStateChanged?.Invoke(false);
    }

    public void Switch()
    {
        if (ListeningState) Stop(); else Start();
    }

    private void OnFrameReceived(Endpoint ep, byte[] payload, WaveFormat wf)
    {
        // Ensure output is running with compatible format (16-bit PCM, mono/stereo)
        int outChannels = wf.Channels <= 1 ? 1 : 2;
        EnsureOutput(wf.SampleRate, outChannels);

        // Convert to 16-bit PCM little endian and adjust channel count
        byte[] pcm16 = ConvertToPcm16Interleaved(payload, wf.BitsPerSample, wf.Channels, outChannels);
        _audioOutput.Write(pcm16, 0, pcm16.Length);
    }

    private void EnsureOutput(int sampleRate, int channels)
    {
        if (!_audioOutput.IsStarted)
        {
            _audioOutput.Start(sampleRate, channels);
            return;
        }
        // A minimal approach: restart if different format arrives
        // More advanced buffering/format negotiation could be added later.
        _audioOutput.Start(sampleRate, channels);
    }

    private static byte[] ConvertToPcm16Interleaved(byte[] input, int inBits, int inCh, int outCh)
    {
        // Fast path: 16-bit PCM already
        if (inBits == 16)
        {
            if (inCh == outCh)
                return input; // already correct

            int frames = input.Length / (inCh * 2);
            byte[] outBuf = new byte[frames * outCh * 2];

            if (outCh == 1)
            {
                // Downmix to mono: average first two channels (or duplicate mono)
                for (int f = 0; f < frames; f++)
                {
                    int inBase = f * inCh * 2;
                    short l = BitConverter.ToInt16(input, inBase);
                    short r = inCh >= 2 ? BitConverter.ToInt16(input, inBase + 2) : l;
                    short m = (short)((l + r) / 2);
                    Buffer.BlockCopy(BitConverter.GetBytes(m), 0, outBuf, f * 2, 2);
                }
            }
            else // outCh == 2
            {
                if (inCh == 1)
                {
                    // Upmix mono to stereo
                    for (int f = 0; f < frames; f++)
                    {
                        short s = BitConverter.ToInt16(input, f * 2);
                        int outBase = f * 4;
                        Buffer.BlockCopy(BitConverter.GetBytes(s), 0, outBuf, outBase, 2);
                        Buffer.BlockCopy(BitConverter.GetBytes(s), 0, outBuf, outBase + 2, 2);
                    }
                }
                else
                {
                    // Take first two channels from multichannel
                    for (int f = 0; f < frames; f++)
                    {
                        int inBase = f * inCh * 2;
                        int outBase = f * 4;
                        outBuf[outBase + 0] = input[inBase + 0];
                        outBuf[outBase + 1] = input[inBase + 1];
                        outBuf[outBase + 2] = input[inBase + 2];
                        outBuf[outBase + 3] = input[inBase + 3];
                    }
                }
            }
            return outBuf;
        }

        // Convert any integer format to 16-bit mono/stereo
        int inBps = inBits / 8;
        int framesCount = input.Length / (inCh * inBps);
        short[] tmpOut = new short[framesCount * outCh];

        for (int f = 0; f < framesCount; f++)
        {
            // Read first two channels (or one) as 32-bit intermediate
            int baseIdx = f * inCh * inBps;

            int s0 = ReadSampleAsInt(input, baseIdx + 0 * inBps, inBits);
            int s1 = inCh >= 2 ? ReadSampleAsInt(input, baseIdx + 1 * inBps, inBits) : s0;

            short l = ToInt16(s0, inBits);
            short r = ToInt16(s1, inBits);

            if (outCh == 1)
            {
                // mono: average
                int m = (l + r) / 2;
                tmpOut[f] = (short)m;
            }
            else
            {
                int outBase = f * 2;
                tmpOut[outBase] = l;
                tmpOut[outBase + 1] = r;
            }
        }

        // Serialize to bytes LE
        byte[] outBytes = new byte[tmpOut.Length * 2];
        Buffer.BlockCopy(tmpOut, 0, outBytes, 0, outBytes.Length);
        return outBytes;
    }

    private static int ReadSampleAsInt(byte[] buffer, int offset, int bits)
    {
        return bits switch
        {
            // 8-bit PCM: assume signed [-128..127]
            8 => (sbyte)buffer[offset],
            // 16-bit PCM LE
            16 => BitConverter.ToInt16(buffer, offset),
            // 24-bit PCM LE: sign-extend to 32-bit
            24 =>
            (
                (buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16))
                | ((buffer[offset + 2] & 0x80) != 0 ? unchecked((int)0xFF000000) : 0)
            ),
            // 32-bit PCM LE
            32 => BitConverter.ToInt32(buffer, offset),
            _ => 0
        };
    }

    private static short ToInt16(int sample, int inBits)
    {
        return inBits switch
        {
            8 => (short)(sample << 8), // expand to 16-bit
            16 => (short)sample,
            24 => (short)(sample >> 8),
            32 => (short)(sample >> 16),
            _ => 0
        };
    }

    private void OnListeningStateChange(bool active) => ListeningState = active;

    private void EndpointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            for (int i = 0; i < e.OldItems.Count; i++)
            {
                var endpoint = (Endpoint)e.OldItems[i]!;
                _ = _endpointRepository.RemoveAsync(endpoint.Id);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            for (int i = 0; i < e.NewItems.Count; i++)
            {
                var endpoint = (Endpoint)e.NewItems[i]!;
                endpoint.PropertyChanged += EndpointPropertyChanged;
                var dto = _mapper.Map<EndpointDto>(endpoint);
                _ = _endpointRepository.CreateAsync(dto);
            }
        }
    }

    private void EndpointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Endpoint endpoint) return;
        var dto = _mapper.Map<EndpointDto>(endpoint);
        _ = _endpointRepository.UpdateAsync(dto);
    }
}
