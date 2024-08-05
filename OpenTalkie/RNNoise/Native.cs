using System.Runtime.InteropServices;

namespace OpenTalkie.RNNoise;

public static partial class Native
{
    public const string LIBRARY_NAME = "librnnoise.so";
    public const int FRAME_SIZE = 480;

    public const float SIGNAL_SCALE = short.MaxValue;
    public const float SIGNAL_SCALE_INV = 1f / short.MaxValue;

    [LibraryImport(LIBRARY_NAME)]
    internal static partial int rnnoise_get_size();

    [LibraryImport(LIBRARY_NAME)]
    internal static partial int rnnoise_init(IntPtr state, IntPtr model);

    [LibraryImport(LIBRARY_NAME)]
    internal static partial IntPtr rnnoise_create(IntPtr model);

    [LibraryImport(LIBRARY_NAME)]
    internal static partial void rnnoise_destroy(IntPtr state);

    [LibraryImport(LIBRARY_NAME)]
    internal static unsafe partial float rnnoise_process_frame(IntPtr state, float* dataOut, float* dataIn);
}
