namespace ESRecorder.Core;

public sealed record CombustionAcousticTuning(
    string Id,
    double MasterGainMultiplier,
    double EventGainMultiplier,
    double DecayMultiplier,
    double ResonanceMultiplier,
    double NoiseMix,
    double ThrottleResponse,
    double ExhaustGainMultiplier,
    string Notes);

public static class CombustionAcousticProfileCatalog
{
    public const string ContractId = "nextcar-combustion-acoustic-profile-v1";

    private static readonly IReadOnlyDictionary<string, CombustionAcousticTuning> Profiles =
        new Dictionary<string, CombustionAcousticTuning>(StringComparer.OrdinalIgnoreCase)
        {
            ["conventional-spark"] = new(
                "conventional-spark", 1.00, 1.00, 1.00, 1.00, 0.27, 0.72, 1.00,
                "Neutral spark-ignition authoring profile."),
            ["lean-burn-spark"] = new(
                "lean-burn-spark", 0.96, 0.94, 0.90, 1.03, 0.24, 0.68, 0.94,
                "Leaner, slightly shorter combustion-pulse authoring texture."),
            ["stratified-di-spark"] = new(
                "stratified-di-spark", 0.99, 0.98, 0.92, 1.04, 0.29, 0.72, 1.00,
                "Stratified/direct-injection acoustic authoring texture."),
            ["atkinson-spark"] = new(
                "atkinson-spark", 0.92, 0.91, 1.10, 0.94, 0.23, 0.62, 0.92,
                "Softer long-expansion authoring texture."),
            ["miller-spark"] = new(
                "miller-spark", 0.96, 0.95, 1.02, 0.98, 0.25, 0.67, 0.96,
                "Moderated charge-control authoring texture."),
            ["prechamber-spark"] = new(
                "prechamber-spark", 1.01, 1.04, 0.84, 1.08, 0.22, 0.75, 1.04,
                "Sharper pre-chamber jet ignition authoring texture."),
            ["hcci"] = new(
                "hcci", 0.93, 0.91, 1.14, 0.95, 0.19, 0.60, 0.90,
                "Smoother distributed autoignition authoring texture."),
            ["spcci"] = new(
                "spcci", 0.97, 0.96, 0.96, 1.00, 0.22, 0.68, 0.96,
                "Spark-controlled compression-ignition authoring texture."),
            ["turbulent-jet-ignition"] = new(
                "turbulent-jet-ignition", 1.03, 1.06, 0.80, 1.10, 0.20, 0.77, 1.07,
                "Fast, sharp turbulent-jet authoring texture."),
            ["methanol-spark"] = new(
                "methanol-spark", 1.02, 1.04, 0.84, 1.08, 0.24, 0.76, 1.04,
                "High-energy methanol spark-ignition authoring texture."),
            ["diesel-ci"] = new(
                "diesel-ci", 1.02, 1.05, 0.88, 0.93, 0.40, 0.68, 1.06,
                "Compression-ignition diesel authoring texture."),
            ["rcci"] = new(
                "rcci", 0.97, 0.94, 1.02, 0.92, 0.31, 0.64, 0.96,
                "Reactivity-controlled compression-ignition authoring texture."),
            ["hydrogen-lean-spark"] = new(
                "hydrogen-lean-spark", 0.94, 0.91, 0.78, 1.12, 0.20, 0.78, 0.94,
                "Ultra-lean hydrogen spark-ignition authoring texture."),
            ["cng-prechamber"] = new(
                "cng-prechamber", 0.97, 0.96, 0.92, 1.01, 0.24, 0.70, 0.98,
                "CNG pre-chamber authoring texture.")
        };

    public static IReadOnlyCollection<string> KnownProfileIds => Profiles.Keys.ToArray();

    public static CombustionAcousticTuning Resolve(string profileId, string combustionClass)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);

        var resolvedId = profileId.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? DefaultProfileFor(combustionClass)
            : profileId;

        if (!Profiles.TryGetValue(resolvedId, out var tuning))
        {
            throw new ArgumentException(
                $"Unknown combustion acoustic profile '{profileId}'. " +
                $"Known profiles: {string.Join(", ", KnownProfileIds.OrderBy(static item => item, StringComparer.Ordinal))}.",
                nameof(profileId));
        }

        return tuning;
    }

    private static string DefaultProfileFor(string combustionClass)
    {
        if (combustionClass.Contains("diesel", StringComparison.OrdinalIgnoreCase))
            return "diesel-ci";
        if (combustionClass.Contains("methanol", StringComparison.OrdinalIgnoreCase))
            return "methanol-spark";
        if (combustionClass.Contains("hydrogen", StringComparison.OrdinalIgnoreCase))
            return "hydrogen-lean-spark";
        if (combustionClass.Contains("cng", StringComparison.OrdinalIgnoreCase))
            return "cng-prechamber";
        return "conventional-spark";
    }
}
