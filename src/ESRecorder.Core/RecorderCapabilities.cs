namespace ESRecorder.Core;

public sealed record RecorderBackendCapability(
    string Id,
    string Status,
    string[] SourceFamilies,
    string[] Features,
    string Notes);

public sealed record RecorderCapabilitiesReport(
    int SchemaVersion,
    string Product,
    string CapabilityContract,
    RecorderBackendCapability[] Backends,
    string[] Guarantees,
    string[] PlannedSourceFamilies);

public static class RecorderCapabilityCatalog
{
    public static RecorderCapabilitiesReport Create() => new(
        SchemaVersion: 1,
        Product: "ESRecorder-nextcar",
        CapabilityContract: "nextcar-recorder-capabilities-v1",
        Backends:
        [
            new RecorderBackendCapability(
                Id: "legacy-engine-sim-0.1.11a",
                Status: "supported-compatibility-backend",
                SourceFamilies: ["classic-piston-engine"],
                Features:
                [
                    "engine-sim-script-compile",
                    "rpm-hold",
                    "throttle-control",
                    "wav-recording"
                ],
                Notes:
                    "Preserves the historical esrecord-lib.dll compatibility path. " +
                    "It must not be used to claim support for topologies absent from engine-sim 0.1.11a."),
            new RecorderBackendCapability(
                Id: "event-source-v1",
                Status: "supported-native-fork-backend",
                SourceFamilies:
                [
                    "generic-periodic-event-source",
                    "wankel",
                    "multi-crank",
                    "two-stroke-piston",
                    "opposed-piston"
                ],
                Features:
                [
                    "arbitrary-event-phases",
                    "multiple-shaft-ratios",
                    "multi-event-train-composition",
                    "periodic-combustion-pulses",
                    "two-stroke-event-scheduling",
                    "opposed-piston-chamber-model",
                    "harmonic-mechanical-layers",
                    "deterministic-noise",
                    "cross-platform-pcm16-wav-render"
                ],
                Notes:
                    "Native ESRecorder-nextcar event renderer. Wankel, multi-crank, two-stroke piston " +
                    "and opposed-piston sources are represented by their explicit event/shaft topology " +
                    "rather than by fallback piston-engine substitutions.")
        ],
        Guarantees:
        [
            "machine-readable-capabilities",
            "deterministic-render-for-identical-input",
            "no-silent-topology-fallback",
            "offline-authoring-only"
        ],
        PlannedSourceFamilies:
        [
            "radial-cam-ring",
            "axial-piston",
            "free-piston",
            "electric-machine",
            "multi-source-composite"
        ]);
}
