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
