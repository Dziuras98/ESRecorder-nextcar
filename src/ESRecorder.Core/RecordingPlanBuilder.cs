namespace ESRecorder.Core;

public static class RecordingPlanBuilder
{
    public static RecordingPlan Build(RecordingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.EngineScriptPath))
            throw new ArgumentException("Engine script path is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.OutputStem))
            throw new ArgumentException("Output stem is required.", nameof(request));
        if (request.RpmPoints.Count == 0)
            throw new ArgumentException("At least one RPM point is required.", nameof(request));
        if (request.ThrottlePoints.Count == 0)
            throw new ArgumentException("At least one throttle point is required.", nameof(request));
        if (request.SampleLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Sample length must be positive.");
        if (request.WarmupCount < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Warmup count cannot be negative.");
        if (request.MaxInstances is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(request), "Instance count must be between 1 and 8.");

        var duplicateRpm = request.RpmPoints
            .GroupBy(static point => point.Rpm)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateRpm is not null)
            throw new ArgumentException($"Duplicate RPM point: {duplicateRpm.Key}.", nameof(request));

        var duplicateThrottle = request.ThrottlePoints
            .GroupBy(static throttle => throttle)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateThrottle is not null)
            throw new ArgumentException($"Duplicate throttle point: {duplicateThrottle.Key}.", nameof(request));

        foreach (var point in request.RpmPoints)
        {
            if (point.Rpm <= 0)
                throw new ArgumentOutOfRangeException(nameof(request), "RPM values must be positive.");
            if (point.Frequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(request), "Sample frequencies must be positive.");
        }

        foreach (var throttle in request.ThrottlePoints)
        {
            if (throttle is < 0 or > 100)
                throw new ArgumentOutOfRangeException(nameof(request), "Throttle values must be between 0 and 100.");
        }

        var orderedRpm = request.RpmPoints.OrderBy(static point => point.Rpm).ToArray();
        var orderedThrottle = request.ThrottlePoints.OrderBy(static throttle => throttle).ToArray();
        var sampleCount = orderedRpm.Length * orderedThrottle.Length;
        var workerCount = Math.Min(request.MaxInstances, sampleCount);
        var workers = Enumerable.Range(0, workerCount)
            .Select(static _ => new List<RecordingSample>())
            .ToArray();

        var outputDirectory = Path.GetFullPath(request.OutputDirectory);
        var outputStem = SanitizeFileName(request.OutputStem);
        var sampleIndex = 0;

        foreach (var rpm in orderedRpm)
        {
            foreach (var throttle in orderedThrottle)
            {
                var outputPath = Path.Combine(
                    outputDirectory,
                    $"{outputStem}_{rpm.Rpm}_{throttle}.wav");

                workers[sampleIndex % workerCount].Add(new RecordingSample(
                    rpm.Rpm,
                    throttle,
                    rpm.Frequency,
                    request.SampleLength,
                    request.WarmupCount,
                    request.OverrideRevLimit,
                    outputPath));
                sampleIndex++;
            }
        }

        return new RecordingPlan(workers
            .Select(static worker => (IReadOnlyList<RecordingSample>)worker.ToArray())
            .ToArray());
    }

    public static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(sanitized) ? "engine" : sanitized;
    }
}
