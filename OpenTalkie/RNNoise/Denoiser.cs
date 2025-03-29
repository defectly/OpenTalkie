using System.Buffers;
using System.Runtime.CompilerServices;

namespace OpenTalkie.RNNoise;

public class Denoiser : IDisposable
{
    private readonly nint state;
    private readonly float[] processingBuffer;
    private readonly float[] processedData;
    private int processingBufferDataStart;
    private int processedDataRemaining;

    public Denoiser()
    {
        state = Native.rnnoise_create(nint.Zero);
        processingBuffer = new float[Native.FRAME_SIZE];
        processedData = new float[Native.FRAME_SIZE];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe int Denoise(byte[] buffer, int offset, int byteCount, bool finish = true)
    {
        if (buffer == null || offset < 0 || offset > buffer.Length ||
            byteCount < 0 || offset + byteCount > buffer.Length ||
            byteCount % 2 != 0)
            throw new ArgumentException();

        int sampleCount = byteCount >> 1;
        float[] floatBuffer = ArrayPool<float>.Shared.Rent(sampleCount);
        try
        {
            fixed (byte* bytePtr = buffer)
            {
                short* shortPtr = (short*)(bytePtr + offset);
                for (int i = 0; i < sampleCount; i++)
                    floatBuffer[i] = shortPtr[i] * (1.0f / 32768.0f);

                int processedSamples = Denoise(floatBuffer.AsSpan(0, sampleCount), finish);

                for (int i = 0; i < processedSamples; i++)
                {
                    float sample = floatBuffer[i] * 32768.0f;
                    shortPtr[i] = (short)Math.Clamp(sample, -32768.0f, 32767.0f);
                }

                return processedSamples << 1;
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(floatBuffer);
        }
    }

    public unsafe int Denoise(Span<float> buffer, bool finish = true)
    {
        int count = 0;
        int bufferLength = buffer.Length;

        fixed (float* bufferPtr = buffer)
        fixed (float* procBufferPtr = processingBuffer)
        fixed (float* procDataPtr = processedData)
        {
            while (bufferLength > 0 || processingBufferDataStart == Native.FRAME_SIZE)
            {
                if (processingBufferDataStart == 0 && bufferLength >= Native.FRAME_SIZE)
                {
                    int fullFrames = bufferLength / Native.FRAME_SIZE;
                    ProcessFullFrames(bufferPtr + count, fullFrames);
                    int processedLength = fullFrames * Native.FRAME_SIZE;
                    count += processedLength;
                    bufferLength -= processedLength;
                    continue;
                }

                if (processedDataRemaining > 0)
                {
                    int copyLength = Math.Min(processedDataRemaining, bufferLength);
                    Buffer.MemoryCopy(
                        procDataPtr + Native.FRAME_SIZE - processedDataRemaining,
                        bufferPtr + count,
                        copyLength * sizeof(float),
                        copyLength * sizeof(float));

                    processingBufferDataStart += copyLength;
                    processedDataRemaining -= copyLength;
                    count += copyLength;
                    bufferLength -= copyLength;
                }

                if (processingBufferDataStart > 0 || bufferLength < Native.FRAME_SIZE)
                {
                    int remainingSpace = Native.FRAME_SIZE - processingBufferDataStart;
                    int copyLength = Math.Min(remainingSpace, bufferLength);

                    Buffer.MemoryCopy(
                        bufferPtr + count,
                        procBufferPtr + processingBufferDataStart,
                        copyLength * sizeof(float),
                        copyLength * sizeof(float));

                    processingBufferDataStart += copyLength;

                    if (processingBufferDataStart == Native.FRAME_SIZE || finish)
                    {
                        if (processingBufferDataStart < Native.FRAME_SIZE)
                            Array.Clear(processingBuffer, processingBufferDataStart,
                                Native.FRAME_SIZE - processingBufferDataStart);

                        ScaleAndProcessFrame(procBufferPtr, procDataPtr);
                        processedDataRemaining = Native.FRAME_SIZE;

                        copyLength = Math.Min(Native.FRAME_SIZE, bufferLength);
                        Buffer.MemoryCopy(procDataPtr, bufferPtr + count,
                            copyLength * sizeof(float), copyLength * sizeof(float));

                        count += copyLength;
                        bufferLength -= copyLength;
                        processedDataRemaining = finish ? 0 : Native.FRAME_SIZE - copyLength;
                        processingBufferDataStart = 0;
                    }
                }
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ProcessFullFrames(float* bufferPtr, int frameCount)
    {
        const float scale = Native.SIGNAL_SCALE;
        const float scaleInv = Native.SIGNAL_SCALE_INV;

        for (int i = 0; i < frameCount; i++)
        {
            float* framePtr = bufferPtr + (i * Native.FRAME_SIZE);
            for (int j = 0; j < Native.FRAME_SIZE; j++)
                framePtr[j] *= scale;

            Native.rnnoise_process_frame(state, framePtr, framePtr);

            for (int j = 0; j < Native.FRAME_SIZE; j++)
                framePtr[j] *= scaleInv;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void ScaleAndProcessFrame(float* inPtr, float* outPtr)
    {
        for (int i = 0; i < Native.FRAME_SIZE; i++)
            inPtr[i] *= Native.SIGNAL_SCALE;

        Native.rnnoise_process_frame(state, outPtr, inPtr);

        for (int i = 0; i < Native.FRAME_SIZE; i++)
            outPtr[i] *= Native.SIGNAL_SCALE_INV;
    }

    public void Dispose()
    {
        if (state != nint.Zero)
            Native.rnnoise_destroy(state);
    }
}