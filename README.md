# ESRecorder-nextcar

Fork of ES-Recorder used as the offline engine-audio renderer source for nEXTcAR.

The repository preserves the original WPF/BeamNG application while adding a
separate headless architecture:

- `src/ESRecorder.Core` — platform-neutral recording domain and orchestration;
- `src/ESRecorder.Native` — Windows adapter for `esrecord-lib.dll`;
- `src/ESRecorder.Cli` — non-interactive recording host;
- `src/ESRecorder.BeamNG` — optional BeamNG exporter;
- `tests/ESRecorder.Core.Tests` — executable contract tests.

The fork also owns a platform-neutral `event-source-v1` acoustic renderer. This is
the extension point for powertrains that cannot be represented honestly by the
legacy engine-sim 0.1.11a DLL. Native source families currently include Wankel,
multi-crank, generic two-stroke piston and opposed-piston two-stroke topologies.

See `docs/HEADLESS_ARCHITECTURE.md` for commands and architectural boundaries.

## Compatibility baseline

The imported native library and original application still use engine-sim
0.1.11a for the legacy piston-script backend. That backend remains available for
historical compatibility.

New nEXTcAR-only topology work uses the fork-owned `event-source-v1` backend.
Run `ESRecorder.Cli capabilities` for the machine-readable capability contract.
Unsupported topologies must fail closed; they must never be silently mapped to a
generic piston engine.

## Build the legacy WPF application

```powershell
dotnet build ESRecorder.csproj --configuration Release --runtime win-x64
```

## Build the headless host

```powershell
dotnet build src/ESRecorder.Cli/ESRecorder.Cli.csproj \
  --configuration Release \
  --runtime win-x64
```

## Run platform-neutral contract tests

```sh
dotnet run \
  --project tests/ESRecorder.Core.Tests/ESRecorder.Core.Tests.csproj \
  --configuration Release
```

## Original upstream purpose

The original ES-Recorder is an engine-sim recording application with BeamNG.drive
conversion. The conversion process is intended to assist BeamNG modding and does
not create a complete mod automatically.

Original upstream: `DDev247/ESRecorder`.


## Native Wankel rendering

Example four-rotor recording bank:

```powershell
dotnet run --project src/ESRecorder.Cli/ESRecorder.Cli.csproj -- `
  render-wankel `
  --name four-rotor `
  --rotors 4 `
  --displacement 2.6 `
  --redline 9500 `
  --output recordings/four-rotor `
  --rpm 1500:44100,4500:44100,8500:44100 `
  --throttle 0,100 `
  --length 5
```

The command writes `event-source.json`, one PCM16 WAV per RPM/throttle point,
and `event-render-report.json`. See `docs/EVENT_SOURCE_V1.md`.


## Multi-crank and two-stroke rendering

The headless CLI additionally exposes:

```text
render-multi-crank
render-two-stroke
render-opposed-piston
```

These commands materialize versioned `event-source.json` graphs and PCM16 WAV
banks through the same deterministic event renderer used by the Wankel path.
