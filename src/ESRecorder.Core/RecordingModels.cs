namespace ESRecorder.Core;

public sealed record RpmPoint(int Rpm, int Frequency);

public sealed record RecordingRequest(
    string EngineScriptPath,
    string OutputDirectory,
    string OutputStem,
    IReadOnlyList<RpmPoint> RpmPoints,
    IReadOnlyList<int> ThrottlePoints,
    int SampleLength,
    int WarmupCount,
    int MaxInstances,
    bool OverrideRevLimit = true);

public sealed record RecordingSample(
    int Rpm,
    int Throttle,
    int Frequency,
    int Length,
    int WarmupCount,
    bool OverrideRevLimit,
    string OutputPath);

public sealed record RecordingPlan(
    IReadOnlyList<IReadOnlyList<RecordingSample>> WorkerAssignments)
{
    public IReadOnlyList<RecordingSample> Samples => WorkerAssignments
        .SelectMany(static assignment => assignment)
        .OrderBy(static sample => sample.Rpm)
        .ThenBy(static sample => sample.Throttle)
        .ToArray();
}

public sealed record EngineMetadata(
    string Name,
    float RedlineRpm,
    float DisplacementLitres,
    int NativeLibraryVersion);

public sealed record SampleMeasurement(
    RecordingSample Sample,
    float PowerHorsepower,
    float TorqueNewtonMetres,
    float RealtimeRatio,
    long ElapsedMilliseconds);

public sealed record RecordingSession(
    EngineMetadata Engine,
    IReadOnlyList<SampleMeasurement> Measurements,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public TimeSpan Duration => CompletedAt - StartedAt;
}

public enum RecorderState
{
    Idle,
    Compiling,
    Preparing,
    Warmup,
    Recording
}

public sealed record RecorderStatus(
    RecorderState State,
    int ProgressPercent,
    bool SimulatorReady);

public sealed record RecordingProgress(
    int InstanceId,
    RecordingSample? Sample,
    RecorderStatus Status,
    int CompletedSamples,
    int TotalSamples);
