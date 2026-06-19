# FreeFlow Windows Port Plan (.NET 8 + WinUI 3)

## 1. Objective

Build a Windows-native FreeFlow app in this repository with strong behavioral parity to the existing macOS app, using:

- **.NET 8**
- **WinUI 3 (Windows App SDK)**
- **Windows-native APIs** for hotkeys, audio, clipboard/input, UI Automation, and capture

The plan prioritizes a usable first release quickly, then closes parity gaps in a controlled second wave.

## 2. Scope and non-goals

### In scope

- System tray dictation app with setup + settings
- Hold-to-talk and toggle dictation shortcuts
- Microphone selection + audio level feedback
- STT + post-processing + optional context pipeline (OpenAI-compatible APIs)
- Paste insertion + clipboard preservation + paste-again
- Run history/logs and actionable error states
- Windows packaging/signing/release automation

### Out of scope (initially)

- Perfect context extraction in every Windows app from day one
- Feature parity for every debug/dev-only macOS utility in v1
- Cross-platform unification/refactor of macOS app internals during first delivery

## 3. Recommended architecture

## 3.1 Project layout

```text
windows/
  FreeFlow.Windows.sln
  src/
    FreeFlow.App/             # WinUI shell, views, setup, settings, tray app bootstrap
    FreeFlow.Core/            # State machine, orchestration, domain models/interfaces
    FreeFlow.Platform/        # Windows integrations (audio/hotkeys/clipboard/UIA/capture)
    FreeFlow.Infrastructure/  # HTTP clients, storage, logging, update plumbing
  tests/
    FreeFlow.Core.Tests/
    FreeFlow.Infrastructure.Tests/
    FreeFlow.Platform.Tests/  # where practical with mocks/fakes
```

## 3.2 Layer boundaries

- **Core** must have no direct Win32/WinRT calls.
- **Platform** implements OS interfaces (`IAudioCapture`, `IHotkeyService`, etc).
- **Infrastructure** handles external I/O (HTTP, filesystem, updater feeds).
- **App** composes everything via DI and owns UI state.

## 3.3 Runtime flow

1. Hotkey event starts/stops recording.
2. Audio service records + meters and emits normalized PCM/WAV.
3. Pipeline submits transcription request.
4. Optional context service adds app/window/selection/screenshot context.
5. Post-processing service cleans output.
6. Clipboard/input service inserts text and restores clipboard state.
7. History and diagnostics persist the run.

## 4. Feature parity matrix

| Capability | macOS status | Windows v1 target | Windows v2 target |
|---|---|---|---|
| Hold/toggle dictation | Complete | Complete | Hardened |
| Global shortcut customization | Complete | Complete | Hardened collision UX |
| Mic selection + levels | Complete | Complete | Complete |
| Transcription model/provider config | Complete | Complete | Complete |
| Post-processing + fallback model | Complete | Complete | Complete |
| Custom vocabulary | Complete | Complete | Complete |
| Paste again | Complete | Complete | Complete |
| Run log/history | Complete | Complete | Complete |
| Command/edit mode | Complete | Partial | Full parity |
| App/selection context | Complete | Basic + fallback | Broader app coverage |
| Screenshot context | Complete | Optional/basic | Full parity |
| In-app update UX | Complete | Basic | Full parity |

## 5. Detailed work plan

## Phase 0: Discovery and parity contract (3-5 days)

Deliverables:

- Existing behavior inventory from macOS app
- Windows parity checklist with **Must / Should / Could**
- Release acceptance criteria for v1 and v2

Exit criteria:

- Agreed MVP list and explicit deferrals
- No unresolved architecture blocker

## Phase 1: Solution scaffold and foundations (4-6 days)

Tasks:

- Create `windows/` solution and projects
- Configure DI (`Microsoft.Extensions.DependencyInjection`)
- Add logging abstraction and file log sink
- Define domain interfaces and pipeline state machine
- Add typed settings/options and config validation

Exit criteria:

- App boots with mocked services
- CI builds all Windows projects

## Phase 2: Audio capture and normalization (1-2 weeks)

Tasks:

- Implement microphone enumeration and default/fallback strategy
- Record audio with NAudio/WASAPI
- Live audio level metering for overlay/settings test
- Normalize to pipeline-required format (e.g., PCM16 mono WAV)
- Add interruption/error handling for device changes

Exit criteria:

- Stable recordings across common devices
- Deterministic output format for STT service

## Phase 3: Global hotkeys and shortcut capture (1 week)

Tasks:

- Implement hold/toggle/paste-again hotkeys
- Shortcut serialization and validation
- Resolve collisions with OS and in-app bindings
- Add capture UI and conflict messaging

Exit criteria:

- Reliable hotkeys under normal desktop use
- No stuck recording state from key transition races

## Phase 4: Transcription and post-processing services (1 week)

Tasks:

- Port API clients using `HttpClientFactory`
- Match timeout, retry, and cancellation semantics
- Implement transcription + cleanup + fallback model behavior
- Validate provider/base URL/model configuration UX

Exit criteria:

- End-to-end transcript pipeline returns expected output
- Errors surfaced in short actionable form

## Phase 5: Clipboard, paste, and text insertion (4-6 days)

Tasks:

- Preserve/restore clipboard transactionally
- Paste synthesized text into focused app using `SendInput`
- Implement “paste again” behavior
- Optional trailing enter behavior for command flows

Exit criteria:

- Paste succeeds in common targets (Notepad, Word, browser inputs, IDE text editors)
- Clipboard restored reliably after operation

## Phase 6: Context and edit-mode baseline (1-2 weeks)

Tasks:

- Foreground app + window metadata service
- Selection text extraction with UI Automation
- Capability detection and fallback text when unsupported
- Integrate context payload into post-processing pipeline

Exit criteria:

- Context works in supported apps and fails gracefully otherwise
- Edit-mode baseline behavior available

## Phase 7: Screenshot context integration (4-7 days)

Tasks:

- Active-window capture via `Windows.Graphics.Capture`
- Resize/compress and payload-size budgeting
- Integrate optional image context in prompt path

Exit criteria:

- Screenshot context gated by capability and configured safely
- No major UI stalls from capture pipeline

## Phase 8: WinUI shell and UX parity (1-2 weeks)

Tasks:

- Tray-first app behavior
- Setup wizard parity (API key, mic, accessibility-like guidance, startup)
- Settings tabs, model selectors, prompt editors, run log views
- Overlay and in-app notifications

Exit criteria:

- User can complete onboarding and dictation flow without terminal/tools
- UI parity good enough for beta testers

## Phase 9: Packaging, updates, and release automation (1 week)

Tasks:

- Decide distribution channel: **MSIX-first** or signed installer
- Integrate code signing
- Implement update checks/install UX compatible with package type
- Add release artifacts to GitHub Releases

Exit criteria:

- Install/upgrade/uninstall path works on clean Windows machines
- Reproducible signed release artifacts

## Phase 10: Hardening and beta cycle (1-2 weeks)

Tasks:

- Fix top issues from beta telemetry and logs
- Improve target-app compatibility and shortcut reliability
- Tune performance and memory under long sessions

Exit criteria:

- Stable beta candidate
- Known limitations documented

## 6. Technical decisions

## 6.1 Framework and libraries

- UI: **WinUI 3**
- Language/runtime: **C# / .NET 8**
- Audio: **NAudio** (WASAPI capture + metering + conversion)
- HTTP: `HttpClientFactory` + Polly-style transient handling
- JSON: `System.Text.Json`
- Settings/secrets: JSON settings + **DPAPI** for sensitive values

## 6.2 Windows integration choices

- Hotkeys: `RegisterHotKey` first, low-level keyboard hook as fallback only when necessary
- Clipboard + paste: Win32 clipboard APIs + `SendInput`
- App context + selection: UI Automation (`IUIAutomation`)
- Screenshot capture: `Windows.Graphics.Capture`
- Startup: Task Scheduler or Startup Task depending on packaging model

## 7. Testing strategy

## 7.1 Automated

- Unit tests for state machine, shortcut parsing, settings validation, API payload shaping
- Integration tests for transcription/post-processing clients using mock HTTP handlers
- Deterministic tests for clipboard/paste transaction logic where possible

## 7.2 Manual compatibility suite

Run smoke scenarios on:

- Windows 11 (primary), Windows 10 (if supported)
- Common targets: Notepad, Word, Chrome/Edge text areas, VS Code, PowerShell/Terminal
- Device matrix: built-in mic, USB headset, Bluetooth mic

## 7.3 Release gates

- No crash in core dictation path across compatibility suite
- Hotkeys stable under rapid key press/release sequences
- Clipboard restoration reliability validated
- Setup flow completes end-to-end on clean machine

## 8. CI/CD plan

Add workflows under `.github/workflows/`:

- `windows-ci.yml`: restore, build, test on `windows-latest`
- `windows-release.yml`: version stamp, package, sign, upload artifacts
- `windows-dev-release.yml` (optional): continuous pre-release builds

Release assets:

- Installer/MSIX
- Symbol files (if applicable)
- Checksums and release notes

## 9. Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| UI Automation inconsistency by app | Context/edit mode gaps | Capability detection + clear fallbacks + app-specific tuning |
| Hotkey interception conflicts | Recording reliability issues | Dual backend strategy + diagnostics + user override |
| Clipboard race conditions | Data loss/user distrust | Transactional clipboard model + strict rollback rules |
| Audio driver/device variance | Recording failures | Strong fallback chain + defensive format conversion |
| Update/packaging complexity | Release delays | Lock packaging decision early in Phase 1 |

## 10. Delivery milestones

1. **M1 - Core MVP engine ready**: hotkey -> record -> transcribe -> paste
2. **M2 - UX-ready beta**: setup/settings/history/notifications
3. **M3 - Advanced context parity**: richer edit mode + screenshot context
4. **M4 - Stable public release**: hardened compatibility + release automation

## 11. Documentation and review cadence

- Keep this file as the source-of-truth roadmap.
- Add a short progress log entry at each milestone.
- Revisit scope after each milestone and reclassify deferred items.
- Track known limitations explicitly rather than silently deferring.

## 12. Immediate next actions

1. Create `windows/` solution skeleton and baseline projects.
2. Land architecture contracts (`Core` interfaces + state machine).
3. Implement vertical slice: hotkey -> audio capture -> transcription -> paste.
4. Validate vertical slice manually before broad UI work.
