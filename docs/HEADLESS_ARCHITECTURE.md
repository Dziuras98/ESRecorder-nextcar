# Headless recorder architecture

## Boundary

The repository now contains four explicit layers:

- `ESRecorder.Core` — platform-neutral recording domain, deterministic sample planning, orchestration and neutral artifacts;
- `ESRecorder.Native` — Windows-only adapter for `esrecord-lib.dll`;
- `ESRecorder.Cli` — non-interactive host for recording and optional export commands;
- `ESRecorder.BeamNG` — optional BeamNG-specific export adapter.

`ESRecorder.Core` targets plain `net8.0`. It has no dependency on WPF, ScottPlot,
BeamNG formats or the native DLL. This is the reusable recorder core for nEXTcAR.

The existing root `ESRecorder.csproj` remains the legacy WPF application. Its
current behavior is preserved, but its compilation explicitly excludes `src/**`
and `tests/**`. Future GUI work should call the core instead of adding more
recording or export logic to `MainWindow.xaml.cs`.

## Neutral recording contract

The core produces:

- WAV files requested through `RecordingSample`;
- `recording-manifest.json` containing engine metadata and sample measurements;
- `dyno.csv` containing RPM, throttle, power, torque, realtime ratio and WAV path.

These artifacts do not encode BeamNG paths or Unreal runtime assumptions.

## Headless recording

On Windows x64 after a Release build:

```powershell
./src/ESRecorder.Cli/bin/Release/net8.0-windows/win-x64/ESRecorder.Cli.exe record `
  --engine-script es/assets/main.mr `
  --output recordings/example `
  --name example `
  --rpm 1000:44100,2000:44100,3000:44100 `
  --throttle 0,50,100 `
  --length 5 `
  --warmup 1 `
  --instances 4
```

The coordinator initializes and compiles one native simulator per worker,
assigns samples round-robin, records without a dispatcher or window, and writes
neutral artifacts after every requested sample succeeds.

## Optional BeamNG export

BeamNG behavior is isolated behind an explicit command:

```powershell
./src/ESRecorder.Cli/bin/Release/net8.0-windows/win-x64/ESRecorder.Cli.exe export-beamng `
  --manifest recordings/example/recording-manifest.json `
  --output exports/example `
  --starter-event i4 `
  --idle-rpm 800 `
  --max-rpm 7500 `
  --static-friction 12 `
  --dynamic-friction 0.01
```

BeamNG export requires 0% and 100% throttle samples. That restriction belongs to
the adapter and is not imposed by the recorder core.

## nEXTcAR integration rule

nEXTcAR should consume the neutral manifest and WAV files. It must not consume
BeamNG JBeam or `sfxBlend2D` files, and it must not load `esrecord-lib.dll` in the
Unreal runtime.


## Event-source-v1 backend

The headless core now also contains a platform-neutral event renderer for source
topologies that the historical engine-sim 0.1.11a DLL cannot represent.

The event graph consists of:

- periodic event trains with arbitrary phases inside a shaft revolution;
- an explicit shaft-speed ratio relative to the requested engine RPM;
- decaying pulse resonances with deterministic broadband texture;
- independent harmonic/mechanical layers;
- a deterministic PCM16 WAV renderer.

The first native source family is Wankel. For a Wankel engine, each rotor
contributes one power event per eccentric-shaft revolution. Multi-rotor phase
offsets are distributed around that revolution, so a four-rotor source contains
four directly represented rotor power events rather than four invented piston
cylinders.

This backend is intentionally separate from `NativeRecorderBackend`. The latter
continues to wrap `esrecord-lib.dll` for legacy engine-sim scripts. Both remain
offline authoring paths; neither is an Unreal runtime dependency.

Use:

```text
ESRecorder.Cli capabilities
ESRecorder.Cli render-wankel ...
ESRecorder.Cli render-event-source ...
```

The generic command consumes a versioned JSON event graph, allowing future
opposed-piston, multi-crank and other nonstandard source families to reuse the
same render contract without per-vehicle recorder hacks.


## Additional event-source topology factories

The event backend now owns reusable factories for multi-crank, generic
two-stroke piston and opposed-piston two-stroke sources.

Multi-crank sources retain one event train per crank module with explicit
shaft-speed ratio and phase. Generic two-strokes schedule one power event per
cylinder per crankshaft revolution. Opposed-piston sources schedule combustion
per chamber and keep crankshaft motion in separate mechanical layers, avoiding
double-counted combustion events.


## Complete PR #193 source-family primitive set

The event backend now also contains reusable radial/cam-ring, axial-piston,
free-piston, electric-machine and source-composition factories.

Multi-revolution event cycles allow odd-cylinder four-stroke radials to retain
their correct two-revolution firing periodicity. Free-piston sources use a
non-rotating reference cycle. Electric-machine sources are harmonic-only and
apply deterministic non-coherent phase offsets between physical machines.
Composite sources merge child event/harmonic graphs with independent component
speed ratios and gains.

These remain offline authoring abstractions. Their presence does not add an
ESRecorder or event-renderer dependency to Unreal runtime code.
