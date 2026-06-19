# FreeFlow Windows Development Environment Setup

This guide makes sure a workstation is correctly set up to build the Windows port
(.NET 8 + WinUI 3 / Windows App SDK) described in [`porting-plan.md`](porting-plan.md).

Complete this **before** Phase 1 (spec lock) work that touches code, and re-run the
verification script whenever a machine is reprovisioned.

## 0. Quick start

```powershell
# From the repository root
pwsh -File scripts/windows/check-dev-env.ps1
```

The script is read-only and prints a `PASS / WARN / FAIL` report with remediation hints.
It exits non-zero if a **required** tool is missing. Resolve all `FAIL` items and review
`WARN` items before starting development.

## 1. Required toolchain

| Tool | Minimum | Why it is needed | Install |
|---|---|---|---|
| Windows 11 (or Win10 2004+) | Build 19041+ | WinRT capture APIs + MSIX packaging | OS update |
| .NET SDK | 8.0.x | Primary build/runtime | `winget install Microsoft.DotNet.SDK.8` |
| Windows SDK | 10.0.19041.0+ | `Windows.Graphics.Capture`, UIA, packaging/signing tools | VS Installer or standalone |
| Visual Studio 2022 **or** Build Tools 2022 | 17.8+ | MSBuild, signing, MSIX, WinUI workload/templates | `winget install Microsoft.VisualStudio.2022.BuildTools` |
| Git | 2.4x | Source control | `winget install Git.Git` |
| GitHub CLI | latest | Releases / PRs / CI helpers | `winget install GitHub.cli` |
| PowerShell | 7.4+ | Scripts and automation | `winget install Microsoft.PowerShell` |
| VS Code | latest | Primary editor for this project | `winget install Microsoft.VisualStudio.Code` |

> Note: this repository is developed in **VS Code** (Cursor's bundled `code` CLI also works).
> See Section 5 for important WinUI-in-VS-Code caveats.

## 2. Visual Studio / Build Tools components

WinUI 3 packaged builds and MSIX signing rely on MSBuild plus the Windows App SDK
templates. In the **Visual Studio Installer**, install these on VS 2022 or Build Tools 2022:

- **.NET desktop development** workload
- **Windows App SDK C# Templates** component
- **Windows 11 SDK (10.0.22621 or later)** component
- **MSVC v143 build tools** (transitive native deps / packaging)

Install the project templates for `dotnet new` (recommended — works on Build Tools-only
machines without the full Visual Studio IDE):

```powershell
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
```

> This is the official Microsoft template pack (short names `winui`/`winui3`/`winui-mvvm`,
> etc.). The verification script accepts **either** this CLI pack **or** the Visual Studio
> "Windows App SDK C# Templates" component.

## 3. Required VS Code extensions

These will be committed as workspace recommendations (`windows/.vscode/extensions.json`)
when the solution is scaffolded. Install now if developing immediately:

| Extension | ID | Purpose |
|---|---|---|
| C# Dev Kit | `ms-dotnettools.csdevkit` | Solution/project model, test runner, debugging |
| C# | `ms-dotnettools.csharp` | Roslyn language server (OmniSharp successor) |
| .NET Install Tool | `ms-dotnettools.vscode-dotnet-runtime` | SDK/runtime acquisition |
| NuGet Gallery | `patcx.vscode-nuget-gallery` | Browse/manage NuGet packages |
| XML | `redhat.vscode-xml` | XAML/csproj/MSBuild editing assistance |
| EditorConfig | `editorconfig.editorconfig` | Enforce shared formatting |
| PowerShell | `ms-vscode.powershell` | Author/run build + env scripts |
| GitHub Actions | `github.vscode-github-actions` | CI workflow authoring |

Install all from a terminal:

```powershell
$ext = @(
  'ms-dotnettools.csdevkit','ms-dotnettools.csharp','ms-dotnettools.vscode-dotnet-runtime',
  'patcx.vscode-nuget-gallery','redhat.vscode-xml','editorconfig.editorconfig',
  'ms-vscode.powershell','github.vscode-github-actions'
)
foreach ($e in $ext) { code --install-extension $e }
```

## 4. Workspace configuration files (added during scaffolding)

When `windows/` is created in Phase 1, commit these so the environment is reproducible:

- `global.json` — pin the .NET SDK band:

  ```json
  {
    "sdk": { "version": "8.0.400", "rollForward": "latestfeature" }
  }
  ```

- `.editorconfig` — shared C# style and analyzer severities.
- `nuget.config` — pin `nuget.org` source (and any signed-package settings).
- `Directory.Build.props` — common `LangVersion`, `Nullable`, `TreatWarningsAsErrors`, TFM.
- `windows/.vscode/extensions.json` — the recommendations in Section 3.
- `windows/.vscode/settings.json` — solution path, format-on-save, analyzer config.
- `windows/.vscode/tasks.json` — `build`, `test`, `run`, `check-env` tasks.
- `windows/.vscode/launch.json` — debug profile for the app (unpackaged first).

## 5. Important VS Code + WinUI 3 caveats

These are real constraints to plan around (not blockers):

- **No XAML visual designer in VS Code.** XAML is hand-authored; use the running app to verify layout.
- **Limited XAML Hot Reload.** Expect more rebuilds than in full Visual Studio.
- **Packaged (MSIX) debugging is easiest in Visual Studio.** Prefer an **unpackaged** configuration for the day-to-day VS Code inner loop; validate packaged builds in VS / CI.
- **Build from CLI** for reliability: `dotnet build` / `dotnet test` / `dotnet run` work well from VS Code's terminal even when designer tooling does not.
- **Tray icon needs a library.** WinUI 3 has no built-in tray API; plan to use a maintained component (e.g., H.NotifyIcon). Track this as an architecture decision.

## 6. First-run validation (Definition of "environment ready")

The environment is ready when:

1. `scripts/windows/check-dev-env.ps1` reports **0 FAIL**.
2. A throwaway WinUI 3 desktop app builds and launches:

   ```powershell
   dotnet build
   dotnet run
   ```

3. `dotnet test` executes (even an empty test project) from VS Code's terminal.
4. `signtool.exe` and `makeappx.exe` resolve (needed later for packaging/signing).
5. `git` and `gh auth status` work for this repository/fork.

## 7. Troubleshooting

| Symptom | Likely cause | Resolution |
|---|---|---|
| `dotnet` not found | SDK missing/PATH | Install .NET 8 SDK; restart shell |
| Restore fails for `Microsoft.WindowsAppSDK` | Offline or missing source | Verify NuGet connectivity; check `nuget.config` |
| `net8.0-windows10.0.x` target errors | Windows SDK not installed | Add Windows 11 SDK component in VS Installer |
| MSIX build/sign errors | Missing SDK signing tools | Install Windows SDK signing tools; confirm `signtool.exe` |
| C# Dev Kit cannot load solution | Solution not yet scaffolded | Open after Phase 1 creates `FreeFlow.Windows.sln` |
| `code` not recognized | CLI not on PATH | Enable "Install 'code' command in PATH" in the editor |

## 8. Optional but recommended

- **Windows Terminal** for a better multi-shell experience.
- **`dotnet dev-certs https --trust`** if any local HTTPS tooling is used.
- **DOTNET_CLI_TELEMETRY_OPTOUT=1** if telemetry should be disabled on dev machines.
- A dedicated **test microphone** (USB headset) for repeatable audio testing.
