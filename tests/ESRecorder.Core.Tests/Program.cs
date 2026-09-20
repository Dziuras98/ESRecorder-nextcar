using System.Collections.Concurrent;
using ESRecorder.BeamNG;
using ESRecorder.Core;

namespace ESRecorder.Core.Tests;

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            TestDeterministicPlan();
            TestDuplicateThrottleRejected();
            TestCapabilityContract();
            TestNativeWankelEventModel();
            TestMultiCrankTopology();
            TestTwoStrokeTopology();
            TestOpposedPistonTopology();
            TestRadialAndCamRingTopology();
            TestAxialPistonTopology();
            TestFreePistonTopology();
            TestElectricMachineTopology();
            TestElectricMachineTypeTimbres();
            TestCompositeTopology();
            TestFixedFiringPistonTopology();
            TestCombustionAcousticProfiles();
            TestSplitSingleTopology();
            TestCoupledPistonCycleTopology();
            TestSingleCrankOpocTopology();
            TestNonWankelRotaryTopology();
            TestThermalFluidTopology();
            TestThermalCompoundComposition();
            TestAuxiliaryMachineTopology();
            TestAcceptedFamilyRenderers();
            TestAdvancedTopologyRenderers();
            TestNewTopologyRenderers();
            TestEventSourceRoundTripAndRender();
            await TestCoordinatorAndArtifactsAsync().ConfigureAwait(false);
            Console.WriteLine("PASS: headless recorder core contract tests");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {exception}");
            return 1;
        }
    }

    private static void TestDeterministicPlan()
    {
        var request = CreateRequest(Path.Combine(Path.GetTempPath(), "esrecorder-plan"));
        var plan = RecordingPlanBuilder.Build(request);

        AssertEqual(2, plan.WorkerAssignments.Count, "worker count");
        AssertEqual(3, plan.WorkerAssignments[0].Count, "worker 0 sample count");
        AssertEqual(3, plan.WorkerAssignments[1].Count, "worker 1 sample count");

        var ordered = plan.Samples;
        AssertEqual(6, ordered.Count, "sample count");
        AssertEqual(1000, ordered[0].Rpm, "first RPM");
        AssertEqual(0, ordered[0].Throttle, "first throttle");
        AssertEqual(2000, ordered[^1].Rpm, "last RPM");
        AssertEqual(100, ordered[^1].Throttle, "last throttle");
    }

    private static void TestDuplicateThrottleRejected()
    {
        var request = CreateRequest(Path.Combine(Path.GetTempPath(), "esrecorder-duplicates")) with
        {
            ThrottlePoints = new[] { 0, 100, 100 }
        };

        try
        {
            _ = RecordingPlanBuilder.Build(request);
            throw new InvalidOperationException("Duplicate throttle values were accepted.");
        }
        catch (ArgumentException exception) when (
            exception.Message.Contains("Duplicate throttle", StringComparison.Ordinal))
        {
        }
    }

    private static void TestCapabilityContract()
    {
        var capabilities = RecorderCapabilityCatalog.Create();
        AssertEqual(1, capabilities.SchemaVersion, "capability schema");
        AssertEqual(
            "nextcar-recorder-capabilities-v1",
            capabilities.CapabilityContract,
            "capability contract id");

        var eventBackend = capabilities.Backends.Single(backend => backend.Id == "event-source-v1");
        AssertTrue(
            eventBackend.SourceFamilies.Contains("wankel", StringComparer.Ordinal),
            "event backend exposes Wankel");
        AssertTrue(
            eventBackend.SourceFamilies.Contains("multi-crank", StringComparer.Ordinal),
            "event backend exposes multi-crank");
        AssertTrue(
            eventBackend.SourceFamilies.Contains("two-stroke-piston", StringComparer.Ordinal),
            "event backend exposes two-stroke piston");
        AssertTrue(
            eventBackend.SourceFamilies.Contains("opposed-piston", StringComparer.Ordinal),
            "event backend exposes opposed-piston");
        foreach (var family in new[]
        {
            "radial-cam-ring",
            "axial-piston",
            "free-piston",
            "electric-machine",
            "multi-source-composite",
            "fixed-firing-piston",
            "split-single-two-stroke",
            "rotary-combustion",
            "thermal-fluid-machine",
            "coupled-piston-cycle"
        })
        {
            AssertTrue(
                eventBackend.SourceFamilies.Contains(family, StringComparer.Ordinal),
                $"event backend exposes {family}");
        }
        foreach (var feature in new[]
        {
            "explicit-ignition-angle-table",
            "cylinder-bank-assignment",
            "paired-piston-phase-model",
            "non-wankel-rotary-combustion",
            "thermal-pressure-event-model",
            "continuous-turbomachinery-harmonics",
            "thermal-response-metadata",
            "coupled-primary-secondary-pressure-trains",
            "pneumatic-accumulator-acoustic-layer",
            "combustion-acoustic-profiles",
            "electric-machine-timbre-profiles"
        })
        {
            AssertTrue(
                eventBackend.Features.Contains(feature, StringComparer.Ordinal),
                $"event backend exposes {feature}");
        }
        AssertTrue(
            capabilities.Guarantees.Contains("no-silent-topology-fallback", StringComparer.Ordinal),
            "capability contract forbids fallback");
    }

    private static void TestNativeWankelEventModel()
    {
        var source = WankelSourceFactory.Create("test-four-rotor", 4, 2.6, 9500);
        AssertEqual("wankel", source.Family, "Wankel family");
        AssertEqual(1, source.EventTrains.Length, "Wankel event train count");
        AssertEqual(4, source.EventTrains[0].EventPhases.Length, "Wankel power event count");
        AssertNear(0.0, source.EventTrains[0].EventPhases[0], 1e-9, "rotor phase 0");
        AssertNear(0.25, source.EventTrains[0].EventPhases[1], 1e-9, "rotor phase 1");
        AssertNear(0.50, source.EventTrains[0].EventPhases[2], 1e-9, "rotor phase 2");
        AssertNear(0.75, source.EventTrains[0].EventPhases[3], 1e-9, "rotor phase 3");
        AssertEqual(
            "4",
            source.Metadata["power_events_per_eccentric_shaft_revolution"],
            "Wankel power events metadata");
    }

    private static void TestMultiCrankTopology()
    {
        var source = MultiCrankSourceFactory.Create(
            "test-h8",
            new[]
            {
                new MultiCrankModuleSpec("upper", 2, 1.0, 0.0),
                new MultiCrankModuleSpec("lower", 2, 1.0, 0.25)
            },
            redlineRpm: 7500,
            family: "h-layout");

        AssertEqual("h-layout", source.Family, "multi-crank family");
        AssertEqual(2, source.EventTrains.Length, "multi-crank event train count");
        AssertEqual(4, source.HarmonicLayers.Length, "multi-crank harmonic layer count");
        AssertNear(0.0, source.EventTrains[0].EventPhases[0], 1e-9, "upper crank first phase");
        AssertNear(0.25, source.EventTrains[1].EventPhases[0], 1e-9, "lower crank first phase");
        AssertEqual("2", source.Metadata["module_count"], "multi-crank module metadata");
        AssertEqual(
            "4",
            source.Metadata["effective_power_events_per_reference_revolution"],
            "multi-crank effective event count");
    }

    private static void TestTwoStrokeTopology()
    {
        var source = TwoStrokePistonSourceFactory.Create(
            "test-i3-2t",
            3,
            1.5,
            8500,
            "inline-3",
            "uniflow");

        AssertEqual("two-stroke-piston", source.Family, "two-stroke family");
        AssertEqual(1, source.EventTrains.Length, "two-stroke event train count");
        AssertEqual(3, source.EventTrains[0].EventPhases.Length, "two-stroke power event count");
        AssertNear(0.0, source.EventTrains[0].EventPhases[0], 1e-9, "two-stroke phase 0");
        AssertNear(1.0 / 3.0, source.EventTrains[0].EventPhases[1], 1e-9, "two-stroke phase 1");
        AssertNear(2.0 / 3.0, source.EventTrains[0].EventPhases[2], 1e-9, "two-stroke phase 2");
        AssertEqual(
            "3",
            source.Metadata["power_events_per_crankshaft_revolution"],
            "two-stroke event metadata");
        AssertEqual("uniflow", source.Metadata["scavenging"], "two-stroke scavenging metadata");
    }

    private static void TestOpposedPistonTopology()
    {
        var source = OpposedPistonSourceFactory.Create(
            "test-op6",
            chamberCount: 6,
            displacementLitres: 3.6,
            redlineRpm: 4500,
            crankshaftCount: 2,
            crankPhaseDegrees: 12.0,
            combustionClass: "diesel");

        AssertEqual("opposed-piston", source.Family, "opposed-piston family");
        AssertEqual(1, source.EventTrains.Length, "opposed-piston combustion train count");
        AssertEqual(6, source.EventTrains[0].EventPhases.Length, "opposed-piston combustion event count");
        AssertEqual(5, source.HarmonicLayers.Length, "opposed-piston mechanical layer count");
        AssertEqual("6", source.Metadata["chamber_count"], "opposed-piston chamber metadata");
        AssertEqual("12", source.Metadata["piston_count"], "opposed-piston piston metadata");
        AssertEqual("2", source.Metadata["crankshaft_count"], "opposed-piston crank metadata");
        AssertEqual(
            "6",
            source.Metadata["power_events_per_output_revolution"],
            "opposed-piston power event metadata");
    }

    private static void TestRadialAndCamRingTopology()
    {
        var radial = RadialCamRingSourceFactory.Create(
            "radial-7",
            "radial-piston",
            workingElementCount: 7,
            displacementLitres: 7.0,
            maxRpm: 3200,
            cycleRevolutions: 2);

        AssertEqual("radial-cam-ring", radial.Family, "radial family");
        AssertEqual(7, radial.EventTrains[0].EventPhases.Length, "radial event count over cycle");
        AssertNear(0.5, radial.EventTrains[0].ShaftRatio, 1e-9, "radial event-cycle shaft ratio");
        AssertEqual("2", radial.Metadata["event_cycle_revolutions"], "radial cycle metadata");
        AssertEqual("3.5", radial.Metadata["power_events_per_output_revolution"], "radial power-event rate");

        var dualCam = RadialCamRingSourceFactory.Create(
            "dcr16",
            "dual-cam-ring",
            workingElementCount: 16,
            displacementLitres: 4.8,
            maxRpm: 7000,
            cycleRevolutions: 1,
            camRingCount: 2,
            camLobesPerRing: 4);

        AssertEqual(16, dualCam.EventTrains[0].EventPhases.Length, "dual-cam-ring event count");
        AssertEqual("2", dualCam.Metadata["cam_ring_count"], "dual-cam-ring count metadata");
        AssertEqual(3, dualCam.HarmonicLayers.Length, "dual-cam-ring mechanical layer count");
    }

    private static void TestAxialPistonTopology()
    {
        var source = AxialPistonSourceFactory.Create(
            "axial-12",
            pistonCount: 12,
            displacementLitres: 2.0,
            maxRpm: 9500,
            cycleRevolutions: 2,
            mechanism: "swashplate");

        AssertEqual("axial-piston", source.Family, "axial-piston family");
        AssertEqual(12, source.EventTrains[0].EventPhases.Length, "axial-piston event count over cycle");
        AssertNear(0.5, source.EventTrains[0].ShaftRatio, 1e-9, "axial-piston event-cycle shaft ratio");
        AssertEqual("6", source.Metadata["power_events_per_output_revolution"], "axial-piston power-event rate");
        AssertEqual("swashplate", source.Metadata["mechanism"], "axial-piston mechanism metadata");
    }

    private static void TestFreePistonTopology()
    {
        var source = FreePistonSourceFactory.Create(
            "free-piston-4",
            moduleCount: 4,
            displacementEquivalentLitres: 4.0,
            maxCyclesPerMinute: 3600,
            generatorClass: "linear-generator",
            combustionClass: "diesel");

        AssertEqual("free-piston", source.Family, "free-piston family");
        AssertEqual(4, source.EventTrains.Length, "free-piston module event-train count");
        AssertEqual("cycles_per_minute", source.Metadata["reference_rate_unit"], "free-piston reference-rate unit");
        AssertEqual("4", source.Metadata["combustion_events_per_reference_cycle"], "free-piston event metadata");
    }

    private static void TestElectricMachineTopology()
    {
        var source = ElectricMachineSourceFactory.Create(
            "electric-dual",
            polePairs: 4,
            maxRpm: 18000,
            machineCount: 2,
            machineType: "permanent-magnet",
            slotOrder: 24,
            inverterOrder: 48);

        AssertEqual("electric-machine", source.Family, "electric-machine family");
        AssertEqual(0, source.EventTrains.Length, "electric-machine has no combustion trains");
        AssertEqual(10, source.HarmonicLayers.Length, "electric-machine harmonic layer count");
        AssertEqual("2", source.Metadata["machine_count"], "electric-machine count metadata");
        AssertEqual("4", source.Metadata["electrical_fundamental_order"], "electric fundamental order metadata");
        AssertEqual("permanent-magnet-v1", source.Metadata["machine_timbre_profile"], "electric timbre profile metadata");
        AssertEqual("electric-machine-timbre-v1", source.Metadata["machine_timbre_policy"], "electric timbre policy metadata");
        AssertEqual(
            "deterministic_golden_angle_per_machine",
            source.Metadata["acoustic_phase_policy"],
            "electric multi-machine phase policy");
    }

    private static void TestElectricMachineTypeTimbres()
    {
        var switchedReluctance = ElectricMachineSourceFactory.Create(
            "sr-machine",
            polePairs: 4,
            maxRpm: 18000,
            machineCount: 1,
            machineType: "switched-reluctance motor",
            slotOrder: 24,
            inverterOrder: 48);
        var synchronousReluctance = ElectricMachineSourceFactory.Create(
            "synrm-machine",
            polePairs: 4,
            maxRpm: 18000,
            machineCount: 1,
            machineType: "synchronous-reluctance motor",
            slotOrder: 24,
            inverterOrder: 48);

        AssertEqual(
            "switched-reluctance-v1",
            switchedReluctance.Metadata["machine_timbre_profile"],
            "SR timbre profile");
        AssertEqual(
            "synchronous-reluctance-v1",
            synchronousReluctance.Metadata["machine_timbre_profile"],
            "SynRM timbre profile");
        AssertTrue(
            switchedReluctance.HarmonicLayers.Length == 5 &&
            synchronousReluctance.HarmonicLayers.Length == 5,
            "single electric machine exposes five harmonic layers");
        AssertTrue(
            switchedReluctance.HarmonicLayers
                .Zip(synchronousReluctance.HarmonicLayers)
                .Any(pair =>
                    Math.Abs(pair.First.Order - pair.Second.Order) > 1e-9 ||
                    Math.Abs(pair.First.Gain - pair.Second.Gain) > 1e-9),
            "SR and SynRM timbre layers differ");

        var root = Path.Combine(
            Path.GetTempPath(),
            $"esrecorder-electric-timbre-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var srPath = Path.Combine(root, "sr.wav");
            var synrmPath = Path.Combine(root, "synrm.wav");
            _ = EventAudioRenderer.Render(
                switchedReluctance,
                new EventRenderRequest(
                    srPath,
                    Rpm: 9000,
                    Throttle: 100,
                    SampleRate: 16000,
                    LengthSeconds: 1));
            _ = EventAudioRenderer.Render(
                synchronousReluctance,
                new EventRenderRequest(
                    synrmPath,
                    Rpm: 9000,
                    Throttle: 100,
                    SampleRate: 16000,
                    LengthSeconds: 1));

            var srBytes = File.ReadAllBytes(srPath);
            var synrmBytes = File.ReadAllBytes(synrmPath);
            AssertTrue(
                !srBytes.SequenceEqual(synrmBytes),
                "SR and SynRM must not render bit-identical PCM");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void TestCompositeTopology()
    {
        var rotary = WankelSourceFactory.Create("rotary-child", 2, 1.3, 9000);
        var electric = ElectricMachineSourceFactory.Create("motor-child", 4, 18000);

        var source = CompositeSourceFactory.Create(
            "rotary-hybrid",
            new[]
            {
                new CompositeSourceComponent("ice", rotary, SpeedRatio: 1.0, Gain: 1.0),
                new CompositeSourceComponent("front-motor", electric, SpeedRatio: 2.5, Gain: 0.55, PhaseOffsetRevolutions: 0.125)
            });

        AssertEqual("multi-source-composite", source.Family, "composite family");
        AssertEqual("2", source.Metadata["component_count"], "composite component count");
        AssertTrue(source.Metadata["component_families"].Contains("wankel", StringComparison.Ordinal), "composite includes Wankel");
        AssertTrue(source.Metadata["component_families"].Contains("electric-machine", StringComparison.Ordinal), "composite includes electric machine");
        AssertTrue(source.EventTrains.All(train => train.Name.StartsWith("ice/", StringComparison.Ordinal)), "composite combustion train provenance");
        AssertTrue(source.HarmonicLayers.Any(layer => layer.Name.StartsWith("front-motor/", StringComparison.Ordinal)), "composite electric layer provenance");
    }

    private static void TestFixedFiringPistonTopology()
    {
        var asymmetricV9 = FixedFiringPistonSourceFactory.CreateEven(
            "asymmetric-v9",
            cylinderCount: 9,
            bankCount: 2,
            displacementLitres: 3.6,
            maxRpm: 8800,
            cylinderBankAssignments: new[] { 0, 1, 0, 1, 0, 1, 0, 1, 0 },
            cycleDegrees: 720,
            layout: "asymmetric-v9-5+4",
            firingLabel: "EVEN");

        AssertEqual("fixed-firing-piston", asymmetricV9.Family, "fixed-firing family");
        AssertEqual(9, asymmetricV9.EventTrains.Length, "asymmetric V9 cylinder event-train count");
        AssertEqual("2", asymmetricV9.Metadata["bank_count"], "asymmetric V9 bank count");
        AssertEqual("4.5", asymmetricV9.Metadata["power_events_per_output_revolution"], "asymmetric V9 event rate");
        AssertEqual(
            "0,1,0,1,0,1,0,1,0",
            asymmetricV9.Metadata["cylinder_bank_assignments"],
            "asymmetric V9 bank assignments");
        AssertTrue(
            asymmetricV9.EventTrains.All(train => Math.Abs(train.ShaftRatio - 0.5) < 1e-9),
            "four-stroke fixed-firing event trains use half crankshaft rate");

        var twin270 = FixedFiringPistonSourceFactory.Create(
            "twin-270",
            cylinderCount: 2,
            bankCount: 1,
            displacementLitres: 1.0,
            maxRpm: 9000,
            ignitionAnglesDegrees: new[] { 0.0, 270.0 },
            cylinderBankAssignments: new[] { 0, 0 },
            cycleDegrees: 720,
            layout: "inline-2",
            firingLabel: "270/450");

        AssertEqual("0,270", twin270.Metadata["ignition_angles_degrees"], "270 twin ignition table");
        AssertNear(0.0, twin270.EventTrains[0].EventPhases[0], 1e-9, "270 twin first phase");
        AssertNear(270.0 / 720.0, twin270.EventTrains[1].EventPhases[0], 1e-9, "270 twin second phase");

        var fan15Assignments = Enumerable.Range(0, 15).Select(index => index % 5).ToArray();
        var fan15 = FixedFiringPistonSourceFactory.CreateEven(
            "pentafan-15",
            cylinderCount: 15,
            bankCount: 5,
            displacementLitres: 4.5,
            maxRpm: 9500,
            cylinderBankAssignments: fan15Assignments,
            layout: "5-banks-x-3",
            firingLabel: "EVEN");

        AssertEqual(15, fan15.EventTrains.Length, "pentafan-15 event train count");
        AssertEqual("5", fan15.Metadata["bank_count"], "pentafan-15 bank count");
        AssertEqual("7.5", fan15.Metadata["power_events_per_output_revolution"], "pentafan-15 event rate");
    }

    private static void TestCombustionAcousticProfiles()
    {
        var assignments = new[] { 0, 1, 0, 1, 0, 1 };
        var angles = Enumerable.Range(0, 6).Select(index => index * 120.0).ToArray();

        var conventional = FixedFiringPistonSourceFactory.Create(
            "profile-conventional",
            6,
            2,
            3.0,
            8500,
            angles,
            assignments,
            720,
            "V6",
            "EVEN",
            "petrol",
            "conventional-spark");

        var tji = FixedFiringPistonSourceFactory.Create(
            "profile-tji",
            6,
            2,
            3.0,
            8500,
            angles,
            assignments,
            720,
            "V6",
            "EVEN",
            "petrol",
            "turbulent-jet-ignition");

        AssertEqual(
            CombustionAcousticProfileCatalog.ContractId,
            tji.Metadata["combustion_acoustic_profile_contract"],
            "combustion acoustic profile contract");
        AssertEqual(
            "turbulent-jet-ignition",
            tji.Metadata["combustion_acoustic_profile"],
            "TJI acoustic profile metadata");
        AssertEqual(
            "conventional-spark",
            conventional.Metadata["combustion_acoustic_profile"],
            "conventional acoustic profile metadata");
        AssertTrue(
            Math.Abs(tji.EventTrains[0].DecayMilliseconds - conventional.EventTrains[0].DecayMilliseconds) > 0.01,
            "combustion profiles alter event decay");
        AssertTrue(
            Math.Abs(tji.EventTrains[0].NoiseMix - conventional.EventTrains[0].NoiseMix) > 0.01,
            "combustion profiles alter noise texture");

        var dieselDefault = FixedFiringPistonSourceFactory.Create(
            "profile-diesel-default",
            4,
            1,
            2.0,
            5000,
            new[] { 0.0, 180.0, 360.0, 540.0 },
            new[] { 0, 0, 0, 0 },
            combustionClass: "diesel");
        AssertEqual(
            "diesel-ci",
            dieselDefault.Metadata["combustion_acoustic_profile"],
            "diesel default acoustic profile");

        AssertTrue(
            CombustionAcousticProfileCatalog.KnownProfileIds.Contains(
                "hcci",
                StringComparer.OrdinalIgnoreCase),
            "HCCI acoustic profile is registered");
        AssertTrue(
            CombustionAcousticProfileCatalog.KnownProfileIds.Contains(
                "rcci",
                StringComparer.OrdinalIgnoreCase),
            "RCCI acoustic profile is registered");
    }

    private static void TestSplitSingleTopology()
    {
        var source = SplitSingleSourceFactory.Create(
            "split-single-six",
            chamberCount: 6,
            bankCount: 2,
            displacementLitres: 3.0,
            maxRpm: 9000,
            transferPistonPhaseDegrees: 15.0,
            layout: "V-split-single",
            combustionClass: "petrol");

        AssertEqual("split-single-two-stroke", source.Family, "split-single family");
        AssertEqual(1, source.EventTrains.Length, "split-single combustion train count");
        AssertEqual(6, source.EventTrains[0].EventPhases.Length, "split-single chamber event count");
        AssertEqual(5, source.HarmonicLayers.Length, "split-single mechanical/exhaust layer count");
        AssertEqual("6", source.Metadata["combustion_chamber_count"], "split-single chamber metadata");
        AssertEqual("12", source.Metadata["piston_count"], "split-single piston metadata");
        AssertEqual("2", source.Metadata["bank_count"], "split-single bank metadata");
        AssertEqual("6", source.Metadata["power_events_per_output_revolution"], "split-single event rate");
        AssertEqual("15", source.Metadata["transfer_piston_phase_degrees"], "split-single piston phase");
    }

    private static void TestCoupledPistonCycleTopology()
    {
        var splitCycle = CoupledPistonCycleSourceFactory.Create(
            "split-cycle-4x4",
            cycleClass: "split-cycle",
            combustionCylinderCount: 4,
            secondaryCylinderCount: 4,
            secondaryPressureEventsPerCycle: 4,
            displacementLitres: 2.0,
            maxRpm: 8000,
            cycleRevolutions: 2.0,
            secondaryPhaseDegrees: 180.0,
            pneumaticAccumulator: false,
            combustionClass: "petrol");

        AssertEqual("coupled-piston-cycle", splitCycle.Family, "split-cycle source family");
        AssertEqual(2, splitCycle.EventTrains.Length, "split-cycle event train count");
        AssertEqual(4, splitCycle.EventTrains[0].EventPhases.Length, "split-cycle combustion event count");
        AssertEqual(4, splitCycle.EventTrains[1].EventPhases.Length, "split-cycle compression event count");
        AssertEqual("4", splitCycle.Metadata["combustion_cylinder_count"], "split-cycle combustion cylinders");
        AssertEqual("4", splitCycle.Metadata["secondary_cylinder_count"], "split-cycle compression cylinders");
        AssertEqual("split-cycle", splitCycle.Metadata["cycle_class"], "split-cycle class");
        AssertEqual("false", splitCycle.Metadata["pneumatic_accumulator"], "split-cycle accumulator flag");

        var airHybrid = CoupledPistonCycleSourceFactory.Create(
            "split-cycle-4x4-air-hybrid",
            cycleClass: "split-cycle-air-hybrid",
            combustionCylinderCount: 4,
            secondaryCylinderCount: 4,
            secondaryPressureEventsPerCycle: 4,
            displacementLitres: 2.0,
            maxRpm: 8000,
            cycleRevolutions: 2.0,
            secondaryPhaseDegrees: 180.0,
            pneumaticAccumulator: true,
            combustionClass: "petrol");

        AssertEqual(4, airHybrid.HarmonicLayers.Length, "air-hybrid accumulator layer count");
        AssertEqual("true", airHybrid.Metadata["pneumatic_accumulator"], "air-hybrid accumulator flag");

        var fiveStroke = CoupledPistonCycleSourceFactory.Create(
            "five-stroke-2plus1",
            cycleClass: "five-stroke",
            combustionCylinderCount: 2,
            secondaryCylinderCount: 1,
            secondaryPressureEventsPerCycle: 2,
            displacementLitres: 2.4,
            maxRpm: 8500,
            cycleRevolutions: 2.0,
            secondaryPhaseDegrees: 180.0,
            pneumaticAccumulator: false,
            combustionClass: "petrol");

        AssertEqual(2, fiveStroke.EventTrains[0].EventPhases.Length, "five-stroke combustion event count");
        AssertEqual(2, fiveStroke.EventTrains[1].EventPhases.Length, "five-stroke expansion event count");
        AssertEqual("1", fiveStroke.Metadata["secondary_cylinder_count"], "five-stroke expansion cylinder count");
        AssertEqual("2", fiveStroke.Metadata["secondary_pressure_events_per_cycle"], "five-stroke expansion event metadata");
    }

    private static void TestSingleCrankOpocTopology()
    {
        var source = OpposedPistonSourceFactory.Create(
            "single-crank-opoc",
            chamberCount: 6,
            displacementLitres: 3.0,
            redlineRpm: 6500,
            crankshaftCount: 1,
            crankPhaseDegrees: 0.0,
            combustionClass: "petrol");

        AssertEqual("opposed-piston", source.Family, "single-crank OPOC family");
        AssertEqual("6", source.Metadata["chamber_count"], "single-crank OPOC chamber count");
        AssertEqual("12", source.Metadata["piston_count"], "single-crank OPOC piston count");
        AssertEqual("1", source.Metadata["crankshaft_count"], "single-crank OPOC crank count");
        AssertEqual("6", source.Metadata["power_events_per_output_revolution"], "single-crank OPOC event rate");
        AssertEqual(1, source.EventTrains.Length, "single-crank OPOC shared combustion train");
    }

    private static void TestNonWankelRotaryTopology()
    {
        var articulated = RotaryCombustionSourceFactory.Create(
            "articulated-four",
            "articulated four-chamber rotary",
            workingElementCount: 4,
            powerEventsPerOutputRevolution: 4.0,
            maxOutputRpm: 9500);

        AssertEqual("rotary-combustion", articulated.Family, "non-Wankel rotary family");
        AssertEqual(4, articulated.EventTrains[0].EventPhases.Length, "articulated rotary element count");
        AssertEqual("4", articulated.Metadata["power_events_per_output_revolution"], "articulated rotary event rate");
        AssertTrue(
            !articulated.Metadata["mechanism"].Contains("Wankel", StringComparison.OrdinalIgnoreCase),
            "articulated rotary is not labeled Wankel");

        var gerotor = RotaryCombustionSourceFactory.Create(
            "gerotor-seven",
            "seven-lobe gerotor combustion",
            workingElementCount: 7,
            powerEventsPerOutputRevolution: 7.0,
            maxOutputRpm: 7500);

        AssertEqual("7", gerotor.Metadata["working_element_count"], "gerotor element count");
        AssertNear(1.0, gerotor.EventTrains[0].ShaftRatio, 1e-9, "gerotor event train shaft ratio");

        var toroidal = RotaryCombustionSourceFactory.Create(
            "toroidal-eight",
            "eight-piston toroidal opposed rotary combustion",
            workingElementCount: 8,
            powerEventsPerOutputRevolution: 8.0,
            maxOutputRpm: 9000);

        AssertEqual(8, toroidal.EventTrains[0].EventPhases.Length, "toroidal rotary event count");
        AssertEqual("8", toroidal.Metadata["power_events_per_output_revolution"], "toroidal event rate");
    }

    private static void TestThermalFluidTopology()
    {
        var stirling = ThermalFluidMachineSourceFactory.Create(
            "stirling-six",
            "reciprocating-external-combustion",
            "six-cylinder double-acting Stirling",
            workingElementCount: 6,
            pressureEventsPerReferenceRevolution: 6.0,
            maxReferenceRpm: 4500,
            bladeOrLobeOrder: 6,
            workingFluid: "helium",
            thermalResponseClass: "slow-thermal");

        AssertEqual("thermal-fluid-machine", stirling.Family, "Stirling source family");
        AssertEqual(1, stirling.EventTrains.Length, "Stirling pressure event train count");
        AssertEqual(6, stirling.EventTrains[0].EventPhases.Length, "Stirling working element event count");
        AssertEqual(
            "reciprocating-external-combustion",
            stirling.Metadata["machine_class"],
            "Stirling machine class");
        AssertEqual("helium", stirling.Metadata["working_fluid"], "Stirling working fluid");

        var rotarySteam = ThermalFluidMachineSourceFactory.Create(
            "rotary-steam",
            "rotary-expander",
            "rotary steam expander",
            workingElementCount: 4,
            pressureEventsPerReferenceRevolution: 4.0,
            maxReferenceRpm: 8000,
            bladeOrLobeOrder: 4,
            workingFluid: "steam",
            thermalResponseClass: "boiler-lag");

        AssertEqual(1, rotarySteam.EventTrains.Length, "rotary steam event train count");
        AssertEqual(
            "4",
            rotarySteam.Metadata["pressure_events_per_reference_revolution"],
            "rotary steam pressure event rate");

        var turbine = ThermalFluidMachineSourceFactory.Create(
            "steam-turbine",
            "turbine",
            "two-stage steam turbine",
            workingElementCount: 2,
            pressureEventsPerReferenceRevolution: 0.0,
            maxReferenceRpm: 18000,
            bladeOrLobeOrder: 32,
            workingFluid: "steam",
            thermalResponseClass: "slow-thermal");

        AssertEqual(0, turbine.EventTrains.Length, "continuous turbine has no discrete pressure event train");
        AssertEqual(4, turbine.HarmonicLayers.Length, "turbine harmonic layer count");
        AssertEqual(
            "0",
            turbine.Metadata["pressure_events_per_reference_revolution"],
            "turbine zero event metadata");

        var waveRotor = ThermalFluidMachineSourceFactory.Create(
            "wave-rotor",
            "wave-rotor",
            "wave-rotor pressure exchanger",
            workingElementCount: 1,
            pressureEventsPerReferenceRevolution: 0.0,
            maxReferenceRpm: 12000,
            bladeOrLobeOrder: 12,
            workingFluid: "exhaust-gas",
            thermalResponseClass: "fast-pressure-wave");

        AssertEqual("wave-rotor", waveRotor.Metadata["machine_class"], "wave-rotor class");
        AssertEqual(0, waveRotor.EventTrains.Length, "wave rotor can be continuous harmonic source");
    }

    private static void TestThermalCompoundComposition()
    {
        var piston = FixedFiringPistonSourceFactory.CreateEven(
            "compound-v8",
            cylinderCount: 8,
            bankCount: 2,
            displacementLitres: 4.0,
            maxRpm: 7000,
            cylinderBankAssignments: new[] { 0, 1, 0, 1, 0, 1, 0, 1 },
            layout: "V8",
            firingLabel: "EVEN");

        var turbine = ThermalFluidMachineSourceFactory.Create(
            "power-turbine",
            "turbine",
            "mechanically coupled power turbine",
            workingElementCount: 1,
            pressureEventsPerReferenceRevolution: 0.0,
            maxReferenceRpm: 50000,
            bladeOrLobeOrder: 24,
            workingFluid: "exhaust-gas",
            thermalResponseClass: "spool-lag");

        var composite = CompositeSourceFactory.Create(
            "v8-turbocompound",
            new[]
            {
                new CompositeSourceComponent("ice", piston, 1.0, 1.0),
                new CompositeSourceComponent("power-turbine", turbine, 4.0, 0.35, 0.07)
            });

        AssertEqual("multi-source-composite", composite.Family, "thermal compound composite family");
        AssertTrue(
            composite.Metadata["component_families"].Contains("fixed-firing-piston", StringComparison.Ordinal),
            "thermal compound contains piston child");
        AssertTrue(
            composite.Metadata["component_families"].Contains("thermal-fluid-machine", StringComparison.Ordinal),
            "thermal compound contains turbine child");
    }

    private static void TestAuxiliaryMachineTopology()
    {
        var flywheel = AuxiliaryMachineSourceFactory.Create(
            "flywheel-kers",
            "flywheel",
            workingElementCount: 1,
            maxRpm: 30000,
            primaryOrder: 1.0,
            rippleEventsPerRevolution: 0.0,
            noiseMix: 0.02);

        AssertEqual("auxiliary-machine", flywheel.Family, "flywheel auxiliary family");
        AssertEqual(0, flywheel.EventTrains.Length, "flywheel has no pulse event train");
        AssertEqual(3, flywheel.HarmonicLayers.Length, "flywheel harmonic layer count");
        AssertEqual("flywheel", flywheel.Metadata["machine_class"], "flywheel class metadata");

        var hydraulic = AuxiliaryMachineSourceFactory.Create(
            "hydraulic-assist",
            "hydraulic-machine",
            workingElementCount: 7,
            maxRpm: 6000,
            primaryOrder: 7.0,
            rippleEventsPerRevolution: 7.0,
            noiseMix: 0.20);

        AssertEqual(1, hydraulic.EventTrains.Length, "hydraulic ripple event train count");
        AssertEqual(7, hydraulic.EventTrains[0].EventPhases.Length, "hydraulic ripple element count");
        AssertEqual(
            "7",
            hydraulic.Metadata["ripple_events_per_revolution"],
            "hydraulic ripple event metadata");

        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-aux-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            foreach (var source in new[] { flywheel, hydraulic })
            {
                var output = Path.Combine(root, $"{source.Id}.wav");
                var measurement = EventAudioRenderer.Render(
                    source,
                    new EventRenderRequest(
                        output,
                        Rpm: 4000,
                        Throttle: 75,
                        SampleRate: 16000,
                        LengthSeconds: 1));
                AssertTrue(File.Exists(output), $"{source.Id} WAV exists");
                AssertTrue(measurement.PeakAbsolute > 0.005, $"{source.Id} peak non-zero");
                AssertTrue(measurement.RootMeanSquare > 0.0005, $"{source.Id} RMS non-zero");
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void TestAcceptedFamilyRenderers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-accepted-family-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var sources = new[]
            {
                FixedFiringPistonSourceFactory.CreateEven(
                    "render-asymmetric-v9",
                    9,
                    2,
                    3.6,
                    8800,
                    new[] { 0, 1, 0, 1, 0, 1, 0, 1, 0 },
                    720,
                    "asymmetric-v9-5+4",
                    "EVEN"),
                FixedFiringPistonSourceFactory.Create(
                    "render-270-twin",
                    2,
                    1,
                    1.0,
                    9000,
                    new[] { 0.0, 270.0 },
                    new[] { 0, 0 },
                    720,
                    "inline-2",
                    "270/450"),
                SplitSingleSourceFactory.Create(
                    "render-split-single-six",
                    6,
                    2,
                    3.0,
                    9000,
                    15.0,
                    "V-split-single"),
                OpposedPistonSourceFactory.Create(
                    "render-single-crank-opoc",
                    6,
                    3.0,
                    6500,
                    1,
                    0.0,
                    "petrol"),
                RotaryCombustionSourceFactory.Create(
                    "render-gerotor-seven",
                    "seven-lobe gerotor combustion",
                    7,
                    7.0,
                    7500),
                ThermalFluidMachineSourceFactory.Create(
                    "render-stirling-six",
                    "reciprocating-external-combustion",
                    "six-cylinder Stirling",
                    6,
                    6.0,
                    4500,
                    6,
                    "helium",
                    "slow-thermal"),
                ThermalFluidMachineSourceFactory.Create(
                    "render-steam-turbine",
                    "turbine",
                    "two-stage steam turbine",
                    2,
                    0.0,
                    18000,
                    32,
                    "steam",
                    "slow-thermal")
,
                CoupledPistonCycleSourceFactory.Create(
                    "render-coupled-split-cycle",
                    "split-cycle",
                    4,
                    4,
                    4,
                    2.0,
                    8000,
                    2.0,
                    180.0,
                    false,
                    "petrol")            };

            foreach (var source in sources)
            {
                var output = Path.Combine(root, $"{source.Id}.wav");
                var measurement = EventAudioRenderer.Render(
                    source,
                    new EventRenderRequest(
                        output,
                        Rpm: 4000,
                        Throttle: 75,
                        SampleRate: 16000,
                        LengthSeconds: 1));

                AssertTrue(File.Exists(output), $"{source.Id} render WAV exists");
                AssertTrue(new FileInfo(output).Length > 44, $"{source.Id} render WAV contains PCM");
                AssertTrue(measurement.PeakAbsolute > 0.005, $"{source.Id} render peak is non-zero");
                AssertTrue(measurement.RootMeanSquare > 0.0005, $"{source.Id} render RMS is non-zero");
                AssertTrue(double.IsFinite(measurement.PeakAbsolute), $"{source.Id} render peak is finite");
                AssertTrue(double.IsFinite(measurement.RootMeanSquare), $"{source.Id} render RMS is finite");
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void TestAdvancedTopologyRenderers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-advanced-topology-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var electric = ElectricMachineSourceFactory.Create(
                "render-electric",
                polePairs: 4,
                maxRpm: 18000,
                machineCount: 2,
                slotOrder: 24,
                inverterOrder: 48);
            var rotary = WankelSourceFactory.Create("render-rotary", 2, 1.3, 9000);
            var sources = new[]
            {
                RadialCamRingSourceFactory.Create("render-radial", "radial-piston", 7, 7.0, 3200, 2),
                RadialCamRingSourceFactory.Create("render-dcr", "dual-cam-ring", 16, 4.8, 7000, 1, 2, 4),
                AxialPistonSourceFactory.Create("render-axial", 12, 2.0, 9500, 2),
                FreePistonSourceFactory.Create("render-free-piston", 4, 4.0, 3600),
                electric,
                CompositeSourceFactory.Create(
                    "render-hybrid",
                    new[]
                    {
                        new CompositeSourceComponent("rotary", rotary, 1.0, 1.0),
                        new CompositeSourceComponent("motor", electric, 2.0, 0.5, 0.1)
                    })
            };

            foreach (var source in sources)
            {
                var output = Path.Combine(root, $"{source.Id}.wav");
                var measurement = EventAudioRenderer.Render(
                    source,
                    new EventRenderRequest(
                        output,
                        Rpm: source.Family == "free-piston" ? 2400 : 3000,
                        Throttle: 75,
                        SampleRate: 8000,
                        LengthSeconds: 1));

                AssertTrue(File.Exists(output), $"{source.Id} render WAV exists");
                AssertTrue(new FileInfo(output).Length > 44, $"{source.Id} render WAV contains PCM");
                AssertTrue(measurement.PeakAbsolute > 0.005, $"{source.Id} render peak is non-zero");
                AssertTrue(measurement.RootMeanSquare > 0.0005, $"{source.Id} render RMS is non-zero");
                AssertTrue(double.IsFinite(measurement.PeakAbsolute), $"{source.Id} render peak is finite");
                AssertTrue(double.IsFinite(measurement.RootMeanSquare), $"{source.Id} render RMS is finite");
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void TestNewTopologyRenderers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-topology-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var sources = new[]
            {
                MultiCrankSourceFactory.Create(
                    "render-h8",
                    new[]
                    {
                        new MultiCrankModuleSpec("upper", 2, 1.0, 0.0),
                        new MultiCrankModuleSpec("lower", 2, 1.0, 0.25)
                    },
                    7500),
                TwoStrokePistonSourceFactory.Create("render-2t", 4, 2.0, 8000, "inline-4", "loop"),
                OpposedPistonSourceFactory.Create("render-op", 4, 2.4, 5000, 2, 10.0, "diesel")
            };

            foreach (var source in sources)
            {
                var output = Path.Combine(root, $"{source.Id}.wav");
                var measurement = EventAudioRenderer.Render(
                    source,
                    new EventRenderRequest(
                        output,
                        Rpm: 3000,
                        Throttle: 75,
                        SampleRate: 8000,
                        LengthSeconds: 1));

                AssertTrue(File.Exists(output), $"{source.Id} render WAV exists");
                AssertTrue(new FileInfo(output).Length > 44, $"{source.Id} render WAV contains PCM");
                AssertTrue(measurement.PeakAbsolute > 0.01, $"{source.Id} render peak is non-zero");
                AssertTrue(measurement.RootMeanSquare > 0.001, $"{source.Id} render RMS is non-zero");
                AssertTrue(double.IsFinite(measurement.PeakAbsolute), $"{source.Id} render peak is finite");
                AssertTrue(double.IsFinite(measurement.RootMeanSquare), $"{source.Id} render RMS is finite");
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void TestEventSourceRoundTripAndRender()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-event-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var source = WankelSourceFactory.Create("test-two-rotor", 2, 1.3, 9000);
            var sourcePath = Path.Combine(root, "source.json");
            AcousticEventSourceSerializer.Write(source, sourcePath);
            var loaded = AcousticEventSourceSerializer.Read(sourcePath);

            AssertEqual(source.Id, loaded.Id, "event source round-trip id");
            AssertEqual(2, loaded.EventTrains[0].EventPhases.Length, "event source round-trip phases");

            var output = Path.Combine(root, "sample.wav");
            var measurement = EventAudioRenderer.Render(
                loaded,
                new EventRenderRequest(
                    output,
                    Rpm: 6000,
                    Throttle: 100,
                    SampleRate: 8000,
                    LengthSeconds: 1));

            AssertEqual(8000, measurement.SampleCount, "event render sample count");
            AssertTrue(File.Exists(output), "event render WAV exists");
            AssertTrue(new FileInfo(output).Length > 44, "event render WAV contains PCM");
            AssertTrue(measurement.PeakAbsolute > 0.01, "event render peak is non-zero");
            AssertTrue(measurement.RootMeanSquare > 0.001, "event render RMS is non-zero");
            AssertTrue(double.IsFinite(measurement.PeakAbsolute), "event render peak is finite");
            AssertTrue(double.IsFinite(measurement.RootMeanSquare), "event render RMS is finite");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static async Task TestCoordinatorAndArtifactsAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-core-tests-{Guid.NewGuid():N}");
        var recordingRoot = Path.Combine(root, "recording");
        var beamNgRoot = Path.Combine(root, "beamng");
        Directory.CreateDirectory(recordingRoot);

        try
        {
            var request = CreateRequest(recordingRoot);
            var backend = new FakeRecorderBackend();
            var coordinator = new RecordingCoordinator(backend);
            var session = await coordinator.RecordAsync(request).ConfigureAwait(false);

            AssertEqual(6, session.Measurements.Count, "coordinator result count");
            AssertEqual("Test Engine", session.Engine.Name, "engine name");
            AssertEqual(2, backend.InitialisedInstances.Count, "initialised instance count");
            AssertEqual(2, backend.CompiledInstances.Count, "compiled instance count");

            foreach (var measurement in session.Measurements)
            {
                await File.WriteAllBytesAsync(
                    measurement.Sample.OutputPath,
                    new byte[] { 82, 73, 70, 70 }).ConfigureAwait(false);
            }

            await RecordingArtifactStore.WriteAsync(session, recordingRoot).ConfigureAwait(false);
            var manifestPath = Path.Combine(recordingRoot, "recording-manifest.json");
            var loaded = await RecordingArtifactStore.ReadAsync(manifestPath).ConfigureAwait(false);
            AssertEqual(session.Measurements.Count, loaded.Measurements.Count, "manifest measurement count");
            AssertTrue(File.Exists(Path.Combine(recordingRoot, "dyno.csv")), "dyno CSV exists");

            await BeamNgExporter.ExportAsync(
                loaded,
                new BeamNgExportOptions(
                    beamNgRoot,
                    "event:>Engine>default",
                    "i4",
                    800,
                    7500,
                    10.0f,
                    0.01f)).ConfigureAwait(false);

            AssertTrue(
                Directory.EnumerateFiles(beamNgRoot, "*.jbeam.fragment").Any(),
                "BeamNG JBeam fragment exists");
            AssertTrue(
                Directory.EnumerateFiles(beamNgRoot, "*.sfxBlend2D.json").Any(),
                "BeamNG blend file exists");
            AssertEqual(
                4,
                Directory.EnumerateFiles(Path.Combine(beamNgRoot, "samples"), "*.wav").Count(),
                "BeamNG copied sample count");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static RecordingRequest CreateRequest(string outputDirectory) => new(
        "engine.mr",
        outputDirectory,
        "Test Engine",
        new[] { new RpmPoint(1000, 44100), new RpmPoint(2000, 48000) },
        new[] { 0, 50, 100 },
        SampleLength: 5,
        WarmupCount: 1,
        MaxInstances: 2);

    private static void AssertEqual<T>(T expected, T actual, string name)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected {expected}, observed {actual}");
    }

    private static void AssertNear(double expected, double actual, double tolerance, string name)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{name}: expected {expected} +/- {tolerance}, observed {actual}");
        }
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Assertion failed: {name}");
    }

    private sealed class FakeRecorderBackend : IRecorderBackend
    {
        public ConcurrentDictionary<int, byte> InitialisedInstances { get; } = new();
        public ConcurrentDictionary<int, byte> CompiledInstances { get; } = new();

        public int NativeLibraryVersion => 1011;

        public void Initialise(int instanceId) => InitialisedInstances.TryAdd(instanceId, 0);

        public void Compile(int instanceId, string engineScriptPath)
        {
            if (!InitialisedInstances.ContainsKey(instanceId))
                throw new InvalidOperationException("Instance was compiled before initialisation.");
            CompiledInstances.TryAdd(instanceId, 0);
        }

        public EngineMetadata GetEngineMetadata(int instanceId) =>
            new("Test Engine", 7500.0f, 2.0f, NativeLibraryVersion);

        public SampleMeasurement Record(int instanceId, RecordingSample sample)
        {
            if (!CompiledInstances.ContainsKey(instanceId))
                throw new InvalidOperationException("Instance was recorded before compilation.");

            return new SampleMeasurement(
                sample,
                sample.Rpm * 0.1f,
                sample.Rpm * (sample.Throttle / 100.0f),
                2.5f,
                10);
        }

        public RecorderStatus GetStatus(int instanceId) =>
            new(RecorderState.Idle, 100, SimulatorReady: true);
    }
}
