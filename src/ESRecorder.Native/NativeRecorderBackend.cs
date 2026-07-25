using System.Runtime.InteropServices;
using ESRecorder.Core;

namespace ESRecorder.Native;

public sealed class NativeRecorderBackend : IRecorderBackend
{
    public int NativeLibraryVersion => NativeMethods.ESRecord_GetVersion();

    public void Initialise(int instanceId)
    {
        EnsureWindows();
        if (!NativeMethods.ESRecord_Initialise(instanceId))
            throw new InvalidOperationException($"Failed to initialise recorder instance {instanceId}.");
    }

    public void Compile(int instanceId, string engineScriptPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineScriptPath);
        if (!File.Exists(engineScriptPath))
            throw new FileNotFoundException("Engine script was not found.", engineScriptPath);

        if (!NativeMethods.ESRecord_Compile(instanceId, engineScriptPath))
            throw new InvalidOperationException(
                $"Native engine compilation failed for instance {instanceId}. " +
                $"Inspect es/error_log{instanceId}.log.");
    }

    public EngineMetadata GetEngineMetadata(int instanceId) => new(
        NativeMethods.ESRecord_Engine_GetName(instanceId) ?? string.Empty,
        NativeMethods.ESRecord_Engine_GetRedline(instanceId),
        NativeMethods.ESRecord_Engine_GetDisplacement(instanceId),
        NativeLibraryVersion);

    public SampleMeasurement Record(int instanceId, RecordingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.OutputPath.Length >= 256)
            throw new ArgumentException("Native recorder output paths must be shorter than 256 characters.", nameof(sample));

        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.GetFullPath(sample.OutputPath))
            ?? throw new InvalidOperationException("Sample output directory is invalid."));

        var nativeConfig = new NativeSampleConfig
        {
            OverrideRevLimit = sample.OverrideRevLimit,
            WarmupCount = sample.WarmupCount,
            Rpm = sample.Rpm,
            Throttle = sample.Throttle,
            Frequency = sample.Frequency,
            Length = sample.Length,
            OutputPath = sample.OutputPath
        };

        var result = NativeMethods.ESRecord_Record(instanceId, nativeConfig);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Native recording failed for instance {instanceId}, " +
                $"RPM {sample.Rpm}, throttle {sample.Throttle}.");
        }

        return new SampleMeasurement(
            sample,
            result.Power,
            result.Torque,
            result.RealtimeRatio,
            result.ElapsedMilliseconds);
    }

    public RecorderStatus GetStatus(int instanceId)
    {
        var state = NativeMethods.ESRecord_GetState(instanceId, out var progress);
        return new RecorderStatus(
            (RecorderState)state,
            progress,
            NativeMethods.ESRecord_GetSimState(instanceId));
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The native ESRecorder backend requires Windows x64.");
    }

    private enum NativeRecorderState
    {
        Idle,
        Compiling,
        Preparing,
        Warmup,
        Recording
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct NativeSampleConfig
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool OverrideRevLimit;
        public int WarmupCount;
        public int Rpm;
        public int Throttle;
        public int Frequency;
        public int Length;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string OutputPath;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSampleResult
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool Success;
        public float Power;
        public float Torque;
        public float RealtimeRatio;
        public long ElapsedMilliseconds;
    }

    private static class NativeMethods
    {
        private const string Library = "es/esrecord-lib.dll";

        [DllImport(Library, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ESRecord_Compile(int instanceId, string path);

        [DllImport(Library)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ESRecord_Initialise(int instanceId);

        [DllImport(Library)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ESRecord_GetSimState(int instanceId);

        [DllImport(Library)]
        public static extern NativeRecorderState ESRecord_GetState(int instanceId, out int progress);

        [DllImport(Library, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string? ESRecord_Engine_GetName(int instanceId);

        [DllImport(Library)]
        public static extern float ESRecord_Engine_GetRedline(int instanceId);

        [DllImport(Library)]
        public static extern float ESRecord_Engine_GetDisplacement(int instanceId);

        [DllImport(Library)]
        public static extern NativeSampleResult ESRecord_Record(
            int instanceId,
            NativeSampleConfig config);

        [DllImport(Library)]
        public static extern int ESRecord_GetVersion();
    }
}
