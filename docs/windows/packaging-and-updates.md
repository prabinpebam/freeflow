# Packaging and Updates (Windows)

This document specifies how FreeFlow for Windows is packaged, signed, distributed,
and auto-updated. It implements the decisions recorded in
[`decisions.md`](./decisions.md): **ADR-001** (unpackaged self-contained app + signed
MSI via WiX Toolset v4) and **ADR-002** (Velopack in-app auto-update fed by GitHub
Releases). Use it together with [`release-checklist.md`](./release-checklist.md).

## 1. Goals

- Single-file-ish, self-contained install with **no .NET runtime prerequisite**.
- **Two distribution flavors** from one set of binaries:
  - **Velopack** release set (`Setup.exe` + full/delta `.nupkg`) that powers the
    in-app auto-updater. This is the primary channel for beta users.
  - **Signed MSI** (WiX v4) for enterprise/managed deployment and for users who
    prefer a conventional installer.
- **Authenticode signing** of the installer and app executables for SmartScreen
  reputation and tamper resistance.
- Support **x64** and **arm64**; keep x86 buildable but not shipped by default.

## 2. Version source of truth

The version is defined **once** in [`windows/Directory.Build.props`](../../windows/Directory.Build.props)
as `<Version>`, so every assembly and the packaged artifacts share it:

```xml
<Version>0.1.0</Version>
```

- Pre-release builds use a SemVer pre-release tag (e.g. `0.2.0-beta.1`).
- `scripts/windows/pack.ps1` defaults to this value; CI can override with
  `-Version` for tagged releases.
- The running app's current version feeds `UpdatePlanner` (see §5). The Platform
  updater adapter derives it from the entry assembly / Velopack's `UpdateManager`.

## 3. Build & publish

Publishing is **self-contained** and **unpackaged** (`WindowsPackageType=None`,
`EnableMsixTooling=false`). Per RID:

```pwsh
dotnet publish windows/src/FreeFlow.App/FreeFlow.App.csproj `
  -c Release -r win-x64 -p:Platform=x64 -p:Version=0.1.0 --self-contained true `
  -o artifacts/publish/win-x64
```

`Release` config enables `PublishReadyToRun` and `PublishTrimmed` (see the App
`.csproj`). Repeat for `win-arm64` (`-p:Platform=ARM64`).

## 4. Velopack packaging (auto-update channel)

[`scripts/windows/pack.ps1`](../../scripts/windows/pack.ps1) automates publish +
`vpk pack` for each RID:

```pwsh
# Stable release, both architectures, signed:
pwsh -File scripts/windows/pack.ps1 -Version 0.1.0 `
  -SignParams "/a /fd sha256 /tr http://timestamp.digicert.com /td sha256"

# Beta candidate, x64 only, unsigned (dev/test):
pwsh -File scripts/windows/pack.ps1 -Version 0.2.0-beta.1 -Channel beta -Runtimes win-x64
```

Outputs land under `artifacts/release/<rid>/`:

- `FreeFlow-<version>-full.nupkg` (and `-delta.nupkg` for upgrades)
- `Setup.exe` (Velopack bootstrapper/installer)
- `RELEASES` / `releases.<channel>.json` feed metadata

Channels (`stable`, `beta`) map to `ReleaseChannel` in Core so the in-app updater
only offers builds the user opted into.

### Signing

`-SignParams` is forwarded to `vpk`, which invokes `signtool` over the app and
installer. When omitted, **unsigned** packages are produced (local/dev only —
never ship unsigned). EV/OV certificate handling and timestamping follow the
org signing policy; the cert is **never** committed to the repo.

## 5. In-app update flow

Pure decision logic lives in Core and is covered by the inner loop (L0):

- [`SemanticVersion`](../../windows/src/FreeFlow.Core/Updates/SemanticVersion.cs) —
  SemVer 2.0 parse/compare (pre-release precedence; build metadata ignored).
- [`UpdatePlanner`](../../windows/src/FreeFlow.Core/Updates/UpdatePlanner.cs) —
  given the current version, the available `ReleaseInfo` list, and a
  `ReleaseChannel`, returns an `UpdateDecision` (the highest applicable release
  strictly newer than current; stable channel ignores pre-releases).
- [`IUpdateService`](../../windows/src/FreeFlow.Core/Updates/IUpdateService.cs) —
  Platform seam. The real adapter (Velopack `UpdateManager` over GitHub Releases)
  performs the network fetch, download, and apply/restart at L4/L5. It maps
  Velopack's release list into `ReleaseInfo`, calls `UpdatePlanner.Plan`, then
  downloads/applies the chosen target.

Runtime sequence:

1. On launch (and/or on a timer), the updater fetches the channel feed from
   GitHub Releases.
2. `UpdatePlanner.Plan(current, releases, channel)` decides if/what to offer.
3. If an update is available, the app downloads the delta and prompts the user;
   on accept it applies and restarts. Failures are non-fatal (logged; app keeps
   running on the current version).

## 6. GitHub Releases layout

- Tag each release `vX.Y.Z` (or `vX.Y.Z-beta.N`).
- Attach the Velopack release set per architecture and the signed MSI.
- Publish SHA-256 checksums (release-checklist gate 5).
- `vpk upload github` can push the Velopack set; do this only after the review
  gates pass — `pack.ps1` deliberately stops at local artifacts.

## 7. MSI (WiX v4) flavor

The MSI is produced by a separate signed job (out of scope for `pack.ps1`) using
the same `artifacts/publish/<rid>` output as payload. It targets managed/enterprise
installs and is signed with the same certificate. The auto-updater is **not**
used for MSI-managed installs; those update via the MSI channel.

## 8. CI/release wiring (summary)

1. Bump `<Version>` in `Directory.Build.props`; commit.
2. Tag `vX.Y.Z`; CI runs the inner loop (must be GREEN) + Platform/App x64 build.
3. CI runs `pack.ps1` with signing params from secrets for `win-x64` + `win-arm64`.
4. CI builds + signs the MSI.
5. Draft a GitHub Release, attach artifacts + checksums, finalize notes.
6. Promote beta → stable by re-tagging/uploading to the `stable` channel.

## 9. Open items / follow-ups

- Wire the real Velopack `UpdateManager` adapter implementing `IUpdateService`
  into Platform DI (currently the seam + pure planner exist; adapter is Phase 6/7
  Platform work, exercised at L4/L5).
- Decide update cadence/UX (silent vs. prompt) and a "skip this version" option.
- Add staging-channel smoke test of a full upgrade cycle before each release.
