# FreeFlow Windows Port Program Plan (.NET 8 + WinUI 3)

This document is an execution-grade plan for porting FreeFlow to Windows. It is intentionally detailed so design gaps are resolved before implementation begins.

## 1. Program charter

## 1.1 Goal

Deliver a Windows-native FreeFlow that preserves core user value from macOS:

- Reliable hotkey-driven dictation
- Fast transcription + cleanup pipeline
- Correct paste behavior in real-world apps
- Optional context-aware improvements without privacy regression

## 1.2 Target stack

- **Language/runtime:** C# / .NET 8
- **UI:** WinUI 3 (Windows App SDK)
- **OS integration:** Win32 + WinRT APIs
- **Audio:** NAudio (WASAPI + conversion)
- **HTTP:** HttpClientFactory + resilient policies

## 1.3 Program success criteria

The port is successful when all are true:

1. Median hotkey-to-paste latency is competitive with current macOS behavior for similar provider/model settings.
2. Core dictation path runs crash-free through defined compatibility matrix.
3. Clipboard restore behavior is non-destructive in validated scenarios.
4. Setup-to-first-dictation completion rate is high on clean Windows machines.
5. Release pipeline can produce signed, reproducible public artifacts.

## 2. Product scope and release strategy

## 2.1 Release slices

- **Windows v1 (MVP):** dependable dictation workflow
- **Windows v1.5:** UX and compatibility hardening
- **Windows v2:** advanced parity (edit mode depth + richer context)

## 2.2 Scope by release

| Capability | v1 | v1.5 | v2 |
|---|---|---|---|
| Hold/toggle shortcuts | Required | Hardened | Hardened |
| Shortcut customization | Required | Hardened | Hardened |
| Mic selection and meters | Required | Hardened | Hardened |
| Transcription and post-processing | Required | Hardened | Hardened |
| Paste and paste-again | Required | Hardened | Hardened |
| Settings + setup flow | Required | Hardened | Hardened |
| Run history/log view | Required | Improved filters/diagnostics | Hardened |
| Command/edit mode | Baseline | Better app support | Parity target |
| Context from foreground app | Baseline fallback-heavy | Broader coverage | Parity target |
| Screenshot context | Optional off-by-default | Optional improved | Full feature path |
| In-app updates | Basic | Improved UX | Mature |

## 2.3 Explicit non-goals for v1

- Perfect text-selection extraction in every application
- Immediate parity for all dev/debug macOS-only tooling
- Cross-platform engine merge with macOS code during initial port

## 3. Pre-execution decisions (must close before implementation)

These specs must be decided before Phase 1 starts:

| Decision | Options | Owner | Due |
|---|---|---|---|
| Packaging strategy | MSIX vs signed EXE installer | Maintainer | Before scaffolding |
| Update model | Store/MSIX auto-update vs GitHub updater | Maintainer | Before update module |
| OS support floor | Win11 only vs Win10+Win11 | Maintainer | Before test matrix lock |
| Telemetry policy | Local-only diagnostics vs opt-in telemetry | Maintainer | Before beta |
| Context defaults | Context on/off default, screenshot default | Maintainer | Before setup UX |
| Command mode scope | Minimal transform set vs full parity | Maintainer | Before v1 freeze |

Unresolved items above are release blockers.

## 4. Technical architecture

## 4.1 Repository layout

```text
windows/
  FreeFlow.Windows.sln
  src/
    FreeFlow.App/
    FreeFlow.Core/
    FreeFlow.Platform/
    FreeFlow.Infrastructure/
  tests/
    FreeFlow.Core.Tests/
    FreeFlow.Infrastructure.Tests/
    FreeFlow.Platform.Tests/
```

## 4.2 Layer responsibilities

| Layer | Responsibilities | Prohibited |
|---|---|---|
| App | WinUI pages/views, viewmodels, composition root, app lifecycle | Direct network/audio/Win32 business logic |
| Core | Pipeline state machine, domain contracts, validation rules, use-cases | Platform calls, filesystem/network calls |
| Platform | Win32/WinRT adapters for hotkeys/audio/clipboard/UIA/capture/startup | Provider-specific HTTP logic |
| Infrastructure | HTTP clients, persistence, logs, release feed parsing | UI components, direct Win32 hooks |

## 4.3 Core interfaces (contracts)

Define and stabilize these first:

- `IHotkeyService`
- `IShortcutCaptureService`
- `IAudioCaptureService`
- `ITranscriptionClient`
- `IPostProcessingClient`
- `IContextService`
- `IClipboardPasteService`
- `IHistoryStore`
- `IUpdateService`
- `IStartupRegistrationService`
- `IPermissionProbeService`

Each interface needs:

- Async and cancellation semantics
- Error taxonomy mapping
- Threading guarantees
- Event callback contract

## 4.4 State machine specification

Canonical pipeline states:

1. `Idle`
2. `Arming` (hold/toggle transitions)
3. `Recording`
4. `Stopping`
5. `Transcribing`
6. `PostProcessing`
7. `Pasting`
8. `Completed`
9. `Error`
10. `Cancelled`

Rules:

- Only one active dictation session at a time.
- Every state transition is explicit and logged.
- Cancellation can happen from any non-terminal state.
- Paste failures must not destroy clipboard recovery behavior.

## 5. Behavior parity spec (what must match macOS semantics)

## 5.1 Shortcut semantics

Specify exact behavior for:

- Hold shortcut press starts recording; release stops.
- Toggle shortcut press starts if idle; press again stops.
- Escape handling during recording/transcribing.
- Shortcut suppression while setup captures bindings.
- Collision validation for hold/toggle/copy-again combos.

Edge cases to codify:

- Key-repeat suppression
- Modifier order changes while held
- Shortcut backend reset after hook restart
- Focus changes between start and paste

## 5.2 Recording semantics

Must define:

- Preferred sample rate/channel/bit depth output
- Device fallback priority when selected device disappears
- Minimum recording length behavior
- Silence/no-buffer timeout behavior
- Interruption recovery behavior

## 5.3 Clipboard and paste semantics

Must define:

- Snapshot format coverage (text/data/other clipboard formats)
- Preserve/restore transaction boundaries
- Timeout for paste completion checks
- Retry policy when target app rejects first paste attempt
- “Paste again” source of truth (raw vs cleaned transcript rules)

## 5.4 Context and command mode semantics

Must define:

- When command mode activates
- Selected-text requirement and fallback path
- How context is included in prompts
- What happens when selected text extraction fails
- Strict non-fabrication rules for model prompts

## 5.5 Failure UX semantics

Must define:

- User-visible error messages per subsystem
- Which errors are transient vs sticky
- Notification display duration and dedupe behavior
- User recovery actions exposed in UI

## 6. Functional specification by subsystem

## 6.1 App shell and lifecycle

Requirements:

- Tray-first app startup
- Single-instance enforcement
- Settings window behavior from tray and relaunch
- Setup wizard appears until minimum requirements completed
- Startup-at-login registration toggle

Implementation notes:

- Use app lifecycle host in WinUI with hidden main window model if tray-only mode is desired.
- Define explicit relaunch and deep-link commands.

## 6.2 Hotkey subsystem

Implementation plan:

1. Primary backend: `RegisterHotKey`
2. Fallback backend: low-level keyboard hook (only when required)
3. Unified event normalization layer
4. Hotkey conflict detector
5. Shortcut capture component for settings/setup

Diagnostic requirements:

- Backend in use
- Registration success/failure code
- Collision reason detail

## 6.3 Audio subsystem

Implementation plan:

1. Enumerate devices via MMDevice APIs
2. Select active capture endpoint
3. Capture PCM with WASAPI shared mode
4. Meter live levels for overlay
5. Convert to pipeline target format (mono PCM16 WAV)

Hard requirements:

- Handle device hotplug without crash
- Accurate and low-jitter level updates
- Deterministic WAV encoding

## 6.4 Networking and provider subsystem

Endpoints (OpenAI-compatible):

- `GET /models` (validation)
- `POST /audio/transcriptions`
- `POST /chat/completions`

Requirements:

- Timeout knobs consistent with macOS defaults and overrides
- Retry only where safe and idempotent
- Clear distinction between auth, provider, and network failures
- Support separate base URLs/keys for transcription vs LLM if configured

## 6.5 Context subsystem (app/window/selection/screenshot)

Metadata requirements:

- Foreground process name
- Window title
- App identity metadata
- Optional selected text (when available)

Screenshot requirements:

- Active window capture
- Max-dimension downscale
- Payload budget enforcement
- MIME/type and failure reason reporting

Privacy requirements:

- Context generation should be transparent and user-controllable.
- Screenshot context should be opt-in at first release.

## 6.6 Paste subsystem

Requirements:

- Clipboard snapshot and restoration with failure-safe path
- Paste injection using `Ctrl+V` via `SendInput`
- Optional trailing Enter for command mode policy
- Verify paste-again path references latest valid transcript artifact

## 6.7 Settings and persistence subsystem

Storage categories:

- Non-sensitive: JSON config
- Sensitive: DPAPI-protected values
- Session/history: append-only records + bounded retention

Requirements:

- Backward-compatible schema evolution
- Corruption recovery path
- Validation + user feedback for invalid values

## 6.8 Update subsystem

Requirements depend on packaging decision:

- Release feed parsing
- Semantic version comparison
- Deferred reminder logic
- Install workflow and relaunch prompt

Needs a single abstraction so update strategy can differ by distribution model.

## 7. Data model and configuration spec

## 7.1 Settings schema (draft)

Define a typed settings class with fields equivalent to macOS semantics:

- API keys and base URLs
- transcription/post-processing/context model IDs
- shortcut bindings
- custom vocabulary
- language settings (input/output if applicable)
- command mode toggles
- timeout overrides
- overlay mode and display target
- update preferences
- startup preference

Add:

- Schema version integer
- Last migration timestamp

## 7.2 History record schema

Each run should persist:

- Session ID and timestamps
- Intent type (dictation/command)
- Raw transcript
- Post-processed transcript
- Context summary metadata (not full screenshot bytes)
- Final outcome (success/error/cancel)
- Error category and message (if any)

Retention policy must be specified (count-based or age-based).

## 8. Non-functional requirements

## 8.1 Reliability

- No unhandled exceptions on core path.
- Hooks, audio, and paste subsystems auto-recover where possible.
- Corrupted settings/history should not block app startup.

## 8.2 Performance budgets

Set explicit budgets for internal overhead (excluding provider latency):

- Hotkey press-to-recording indicator: target under 100 ms
- Recording stop-to-request dispatch: target under 150 ms
- Post-processing overhead from app logic: target under 100 ms
- Paste dispatch after final text ready: target under 80 ms

## 8.3 Resource budgets

- Memory footprint target when idle
- CPU target in idle and active dictation states
- Log file size caps and rotation policy

## 8.4 Security and privacy

- Secrets never written in plaintext logs.
- Sensitive settings encrypted at rest via DPAPI.
- Context/screenshot collection clearly user-controlled.
- Explicit documentation on what leaves machine (provider calls only).

## 8.5 Accessibility and UX

- Keyboard-navigable settings/setup
- High-contrast friendly UI colors
- Clear status text for recording/transcribing/processing/error states

## 9. Compatibility matrix

## 9.1 OS matrix

| OS | Required at launch | Notes |
|---|---|---|
| Windows 11 23H2+ | Yes | Primary development target |
| Windows 10 22H2 | Decision required | If supported, extend QA matrix |

## 9.2 Hardware matrix

| Category | Minimum test set |
|---|---|
| CPU | Intel + AMD modern desktop/laptop |
| Audio devices | Built-in mic, USB headset mic, Bluetooth mic |
| Display | Single monitor + multi-monitor |

## 9.3 Application compatibility matrix

Minimum validated apps:

- Notepad
- Word
- VS Code
- PowerShell / Windows Terminal
- Chrome textareas
- Edge textareas

Stretch validation:

- Slack desktop
- Outlook desktop
- JetBrains IDEs

## 10. Testing strategy

## 10.1 Unit tests (required)

- State transitions and cancellation behavior
- Shortcut parsing and validation
- Settings schema migration logic
- Version comparison and update selection logic

## 10.2 Integration tests (required)

- HTTP request/response handling with fake handlers
- Transcription timeout race behavior
- Post-processing fallback model path
- Clipboard transaction logic with abstractions/fakes

## 10.3 End-to-end test harness (required)

Build internal harness that can:

- Simulate shortcut events
- Feed deterministic audio sample files
- Assert pipeline outputs and persisted run records

## 10.4 Manual test playbooks

Create scripts for:

- Fresh install + setup completion
- Device change mid-recording
- Target app switch before paste
- Network failure and provider auth failure
- Retry and paste-again flows

## 10.5 Release quality gates

Every release candidate must pass:

1. Full automated suite
2. Manual smoke matrix on defined OS/apps/devices
3. No open P0/P1 bugs
4. Crash-free long-run session test

## 11. CI/CD and release engineering

## 11.1 Workflows

- `windows-ci.yml`
  - restore
  - build
  - unit/integration tests
  - artifact upload for diagnostics

- `windows-release.yml`
  - version stamp
  - package
  - sign
  - publish release assets

- `windows-dev-release.yml` (optional)
  - prerelease artifacts from main

## 11.2 Build artifacts

- Installer/MSIX package
- Symbols (if enabled)
- Checksums
- Machine-readable release notes snippet

## 11.3 Secrets and signing checklist

- Code signing certificate and access policy
- CI secret naming conventions
- Rotation cadence and emergency revocation procedure

## 12. Detailed phased execution plan

## Phase 0: Spec lock and parity inventory (Week 1)

Deliverables:

- This plan finalized
- Behavior parity matrix with explicit pass/fail specs
- Decision log entries for all unresolved architecture choices

Exit criteria:

- All pre-execution decisions resolved
- Interfaces and state machine reviewed and approved

## Phase 1: Solution bootstrap and contracts (Week 2)

Tasks:

- Create solution/project skeleton
- Implement DI and logging infrastructure
- Implement interface contracts and no-op adapters
- Add initial unit tests for state machine

Exit criteria:

- App launches with mock services
- CI passes on Windows

## Phase 2: Vertical slice MVP core (Weeks 3-4)

Tasks:

- Hotkey service
- Audio capture service
- Transcription client
- Paste service
- Minimal status UI

Exit criteria:

- End-to-end: hotkey -> record -> transcribe -> paste
- History entries persisted

## Phase 3: Setup/settings and operational UX (Weeks 5-6)

Tasks:

- Setup wizard pages
- Settings tabs and validation
- Shortcut capture UX
- Error notifications and run log UI

Exit criteria:

- New user can configure and complete first dictation without manual config edits

## Phase 4: Context baseline and command mode (Weeks 7-8)

Tasks:

- Foreground app metadata
- Selection extraction baseline
- Context prompt integration
- Command mode baseline transformations

Exit criteria:

- Context/command mode works in validated app subset with explicit fallback behavior

## Phase 5: Packaging, updates, and beta hardening (Weeks 9-10)

Tasks:

- Packaging and update path
- Signed release artifacts
- Compatibility sweep and bug burn-down

Exit criteria:

- Public beta candidate
- Known limitation list published

## Phase 6: Advanced parity closure (Post-v1)

Tasks:

- Screenshot context maturity
- Broader app compatibility for selected text/edit mode
- Updater polish and reliability improvements

## 13. Work breakdown structure (WBS)

## 13.1 Epics

1. Program governance and specs
2. Platform integration
3. Pipeline and model services
4. App UX and setup
5. Persistence and diagnostics
6. Release engineering
7. Hardening and parity closure

## 13.2 Dependency-critical sequence

1. Specs and decisions
2. Contracts and state machine
3. Vertical slice
4. Setup/settings UX
5. Context/command extensions
6. Packaging and release

## 14. Risk register with triggers

| Risk | Trigger | Impact | Mitigation |
|---|---|---|---|
| UI Automation inconsistency | Selection extraction fails in major apps | Command/context quality drops | Capability detection, app-specific adapters, explicit fallback UX |
| Global hook conflicts | Hotkeys fail under security software | Core function blocked | Dual backend, diagnostics, guidance |
| Clipboard race | User clipboard overwritten | Trust damage | Transaction boundaries, retries, restoration checks |
| Audio device churn | Device disconnect mid-session | Session failure | Dynamic fallback, clear error, quick recovery |
| Packaging mismatch | Update path fails in deployed channel | Release delay/user friction | Decide packaging early; test update cycle in staging |

## 15. Governance and operating cadence

## 15.1 Weekly review

- Milestone status
- Open blockers and decision queue
- New risks and mitigation updates
- Scope changes and deferrals

## 15.2 Change control

Any scope change must include:

- User impact
- Engineering effort change
- Test matrix impact
- Revised milestone target

## 15.3 Definition of done (program-level)

A milestone is done only if:

- Code merged
- Tests added/updated and passing
- Documentation updated
- Known limitations captured
- Release checklist items completed

## 16. Documentation pack to maintain during implementation

Maintain these docs under `docs/windows/`:

- `porting-plan.md` (this file)
- `decisions.md` (architecture decision log)
- `parity-matrix.md` (feature-by-feature status)
- `test-matrix.md` (OS/app/device matrix and results)
- `known-limitations.md` (current gaps and workarounds)
- `release-checklist.md` (shipping gate checklist)

## 17. Immediate next actions

1. Create `docs/windows/decisions.md` and close all pre-execution decisions.
2. Create `docs/windows/parity-matrix.md` with macOS behavior references and expected Windows behavior.
3. Scaffold `windows/` solution and contract interfaces from Section 4.
4. Implement and validate vertical slice before expanding UX surface area.
