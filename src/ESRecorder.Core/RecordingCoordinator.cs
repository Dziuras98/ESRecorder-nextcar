using System.Collections.Concurrent;

namespace ESRecorder.Core;

public sealed class RecordingCoordinator
{
    private readonly IRecorderBackend backend;

    public RecordingCoordinator(IRecorderBackend backend)
    {
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public async Task<RecordingSession> RecordAsync(
        RecordingRequest request,
        IProgress<RecordingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var plan = RecordingPlanBuilder.Build(request);
        Directory.CreateDirectory(Path.GetFullPath(request.OutputDirectory));

        var startedAt = DateTimeOffset.UtcNow;
        var totalSamples = plan.Samples.Count;
        var completedSamples = 0;
        var results = new ConcurrentBag<SampleMeasurement>();

        await Task.WhenAll(plan.WorkerAssignments.Select((_, instanceId) => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            backend.Initialise(instanceId);
            backend.Compile(instanceId, request.EngineScriptPath);
            progress?.Report(new RecordingProgress(
                instanceId,
                null,
                backend.GetStatus(instanceId),
                Volatile.Read(ref completedSamples),
                totalSamples));
        }, cancellationToken))).ConfigureAwait(false);

        var metadata = backend.GetEngineMetadata(0) with
        {
            NativeLibraryVersion = backend.NativeLibraryVersion
        };

        await Task.WhenAll(plan.WorkerAssignments.Select((assignment, instanceId) => Task.Run(() =>
        {
            foreach (var sample in assignment)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new RecordingProgress(
                    instanceId,
                    sample,
                    backend.GetStatus(instanceId),
                    Volatile.Read(ref completedSamples),
                    totalSamples));

                var measurement = backend.Record(instanceId, sample);
                results.Add(measurement);
                var completed = Interlocked.Increment(ref completedSamples);

                progress?.Report(new RecordingProgress(
                    instanceId,
                    sample,
                    backend.GetStatus(instanceId),
                    completed,
                    totalSamples));
            }
        }, cancellationToken))).ConfigureAwait(false);

        var orderedResults = results
            .OrderBy(static measurement => measurement.Sample.Rpm)
            .ThenBy(static measurement => measurement.Sample.Throttle)
            .ToArray();

        if (orderedResults.Length != totalSamples)
        {
            throw new InvalidOperationException(
                $"Recorder returned {orderedResults.Length} samples, expected {totalSamples}.");
        }

        return new RecordingSession(
            metadata,
            orderedResults,
            startedAt,
            DateTimeOffset.UtcNow);
    }
}
