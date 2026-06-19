# FreeFlow Windows Port Decision Log

Use this file to capture architecture and product decisions that affect implementation and release scope.

## Decision template

| Field | Value |
|---|---|
| Decision ID | ADR-XXX |
| Date | YYYY-MM-DD |
| Status | Proposed / Accepted / Superseded |
| Owner | Name |
| Scope | App / Core / Platform / Infrastructure / Release |
| Context | Why this decision is needed |
| Options considered | Option A, B, C |
| Decision | Chosen option |
| Rationale | Why this option was selected |
| Consequences | Tradeoffs and follow-up work |
| Revisit trigger | Condition that would require revisiting |

## Required pre-execution decisions

| ID | Topic | Deadline | Status | Notes |
|---|---|---|---|---|
| ADR-001 | Packaging model (MSIX vs signed EXE installer) | Before Phase 1 | Accepted | Unpackaged self-contained app + signed MSI (WiX); MSIX kept as optional v2/Store artifact |
| ADR-002 | Update channel strategy | Before Phase 1 | Accepted | Velopack in-app auto-update fed by GitHub Releases |
| ADR-003 | Supported Windows versions | Before test matrix lock | Accepted | Windows 10 1809 (build 17763)+ and Windows 11; x64 + ARM64 |
| ADR-004 | Telemetry and diagnostics policy | Before beta | Accepted | No network telemetry in v1; local Serilog logs + user "export diagnostics" |
| ADR-005 | Context/screenshot defaults | Before setup UX finalization | Accepted | App/window metadata ON, selected-text ON in supported apps, screenshot context OFF by default |
| ADR-006 | Command mode v1 scope | Before v1 freeze | Accepted | v1 = dictation + command/edit (transform selection); Voice Macros deferred to v2 |
| ADR-007 | Tray-icon component (no built-in WinUI 3 tray API) | Before Phase 1 | Accepted | H.NotifyIcon |
| ADR-008 | AI-eval thresholds + golden-corpus ownership | Before Phase 3 | Accepted | Maintainer-owned `eval.config.json`; in-repo curated corpus; baseline-then-tune thresholds |
| ADR-009 | Default shortcut scheme + input interception | Before Phase 1 | Accepted | Right Ctrl (hold) / Ctrl+Alt+Space (toggle) / Ctrl+Alt+V (paste-again) via WH_KEYBOARD_LL hook; `Fn` is not interceptable on Windows |

## Decision history

Add accepted/superseded ADR entries below this line.

### ADR-001 — Packaging model

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | Release |
| Context | macOS ships a directly-downloaded notarized DMG. Windows must choose between MSIX (packaged/sandboxed) and an unpackaged self-contained app with a classic installer. The app needs a global low-level keyboard hook, `SendInput` into arbitrary apps, flexible autostart, and is built on a Build Tools-only machine (no full VS IDE). |
| Options considered | (A) MSIX packaged; (B) Unpackaged self-contained + signed MSI; (C) Unpackaged + Velopack-only |
| Decision | (B) Unpackaged, self-contained Windows App SDK app (win-x64 + win-arm64), distributed as a code-signed MSI built with WiX Toolset v4. |
| Rationale | Full Win32 capability with least friction (hooks, SendInput, Run-key/Task Scheduler autostart, %APPDATA% I/O), mirrors the macOS direct-download model, and builds/runs via `dotnet` without the VS IDE. Avoids MSIX sandbox/startup-task constraints for a system utility. |
| Consequences | We self-manage updates (see ADR-002) and code-signing. MSIX retained only as an optional future Store/sideload artifact; Phase 0's makeappx/signtool validation remains useful for that path. |
| Revisit trigger | Decision to ship via the Microsoft Store, or a dependency that requires package identity. |

### ADR-002 — Update channel strategy

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | Release |
| Context | The macOS app does version parsing + update prompts. An unpackaged Windows app (ADR-001) has no Store/MSIX auto-update, so it needs its own updater. The repo already publishes GitHub Releases. |
| Options considered | (A) WinSparkle (appcast, closest to macOS Sparkle); (B) Velopack (modern .NET-native, delta updates); (C) Hand-rolled updater |
| Decision | (B) Velopack, with the release feed hosted on GitHub Releases. |
| Rationale | Native .NET integration, delta updates, supports unpackaged apps, and reuses existing GitHub Releases infrastructure. |
| Consequences | Build pipeline must produce Velopack release assets and sign them; in-app update UI follows Velopack's flow. |
| Revisit trigger | Move to MSIX/Store distribution (use platform updates instead). |

### ADR-003 — Supported Windows versions

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | App / Release |
| Context | WinUI 3 / Windows App SDK sets the practical OS floor; the test matrix must be locked against a concrete range. |
| Options considered | (A) Windows 11 only; (B) Windows 10 1809+ and Windows 11 |
| Decision | (B) Windows 10 version 1809 (build 17763) and later, plus Windows 11; architectures x64 and ARM64. Primary CI/test targets: Windows 11 23H2/24H2 and Windows 10 22H2. |
| Rationale | 17763 is the Windows App SDK floor and covers effectively all active users without dropping Windows 10. |
| Consequences | Platform code must avoid APIs newer than the floor or guard them by version checks; ARM64 needs native build/test. |
| Revisit trigger | A required dependency raises the minimum OS, or Windows 10 end-of-support changes priorities. |

### ADR-004 — Telemetry and diagnostics policy

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | App / Release |
| Context | The app processes sensitive audio and dictated text. We must decide what (if anything) leaves the device. |
| Options considered | (A) Opt-in product telemetry; (B) No network telemetry, local logs only |
| Decision | (B) No network telemetry in v1. Local structured logs via Serilog (rolling files), with a user-triggered "export diagnostics" action. Opt-in crash reporting is deferred. |
| Rationale | Privacy-first matches a dictation tool; minimizes consent/compliance burden for v1. |
| Consequences | We rely on user-submitted logs for support; any future telemetry must be explicit opt-in. |
| Revisit trigger | Need for aggregate usage insight before/at beta (opt-in only). |

### ADR-005 — Context/screenshot defaults

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | App |
| Context | The pipeline can use foreground app/window metadata, selected text, and an optional screenshot as context. Defaults set the privacy posture during onboarding. |
| Options considered | (A) All context on by default; (B) Screenshot off by default, metadata/selection on |
| Decision | (B) App/window metadata ON and selected-text capture ON (in supported apps) by default; screenshot context OFF by default (opt-in). |
| Rationale | Screenshots are the most sensitive signal; defaulting off respects privacy while metadata/selection still improve cleanup quality. |
| Consequences | Setup UX must explain and expose the screenshot opt-in; eval corpus must cover both modes. |
| Revisit trigger | User feedback shows screenshot context is high-value and expected on. |

### ADR-006 — Command mode v1 scope

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | App |
| Context | macOS supports dictation, command (automatic/manual) edit mode, and named Voice Macros. v1 scope must be bounded. |
| Options considered | (A) Dictation only; (B) Dictation + command/edit mode; (C) Full parity incl. Voice Macros |
| Decision | (B) v1 ships dictation plus command/edit mode (transform selected text via voice instruction). Voice Macros are deferred to v2. |
| Rationale | Delivers the core parity loop first; macros are additive and can follow without reworking the pipeline. |
| Consequences | Parity matrix marks Voice Macros as Deferred; known-limitations records the gap. |
| Revisit trigger | Post-v1 prioritization of macros. |

### ADR-007 — Tray-icon component

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | Platform |
| Context | WinUI 3 has no built-in system-tray/notify-icon API, but the app is tray-first (mirrors the macOS menu-bar model). |
| Options considered | (A) H.NotifyIcon; (B) Custom Win32 Shell_NotifyIcon wrapper |
| Decision | (A) H.NotifyIcon (WinUI 3 support). |
| Rationale | Actively maintained, MVVM-friendly, supports packaged and unpackaged WinUI 3, efficiency-mode aware; avoids hand-rolling Shell_NotifyIcon + context menus. |
| Consequences | New third-party dependency; tray interactions are validated by L4 UI tests. |
| Revisit trigger | The library can't support a required tray interaction or lags a WinUI release. |

### ADR-008 — AI-eval thresholds + golden-corpus ownership

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | Release / Infrastructure |
| Context | The judgmental tier (testing-strategy.md §8) needs an owner for thresholds and the corpus so quality gates are stable and not silently weakened. |
| Options considered | (A) Hard-code thresholds in tests; (B) Versioned `eval.config.json` owned by the maintainer with an in-repo corpus |
| Decision | (B) Thresholds live in a versioned `eval.config.json` (maintainer-owned, protected oracle). The corpus is curated in-repo (small, license-clean subset) and grown from regressions. Initial starting points: transcript token-F1 ≥ 0.90, semantic cosine ≥ 0.85, to be re-baselined after the first corpus run. |
| Rationale | Keeps the quality bar in version control and under human review; supports the agentic loop's guardrails. |
| Consequences | Threshold changes are reviewed like code; corpus growth has a defined process (testing-strategy.md §11). |
| Revisit trigger | Baseline measurement shows starting thresholds are mis-calibrated. |

### ADR-009 — Default shortcut scheme + input interception

| Field | Value |
|---|---|
| Date | 2026-06-19 |
| Status | Accepted |
| Owner | Maintainer |
| Scope | Platform / App |
| Context | macOS defaults are hold = `Fn`, toggle = `Command-Fn`. The `Fn` key is **not interceptable** on Windows (no `RegisterHotKey`/hook delivery), and `RegisterHotKey` cannot bind a lone key or deliver key-up, which hold-to-talk requires. We need new Windows defaults and an interception mechanism. |
| Options considered | Default key: Right Ctrl vs Apps/Menu key vs CapsLock-remap. Mechanism: `RegisterHotKey` vs low-level keyboard hook (`WH_KEYBOARD_LL`). |
| Decision | Defaults: **hold-to-talk = Right Ctrl** (single key, hold-friendly, present on virtually all keyboards), **toggle = Ctrl+Alt+Space**, **paste-again = Ctrl+Alt+V**. Mechanism: a global **low-level keyboard hook (`WH_KEYBOARD_LL`)** as the primary backend so press/hold/release and single-key bindings work; `RegisterHotKey` only where a simple chord toggle suffices. All shortcuts are fully remappable in settings. |
| Rationale | Reproduces the ergonomics of a dedicated PTT key without depending on the unportable `Fn`; the hook provides the key-up and lone-key semantics `RegisterHotKey` lacks. |
| Consequences | The hook must pass keys through when Right Ctrl is chorded (to preserve normal Ctrl shortcuts) and must coexist with security software (mitigation in risks). Capture UX must support single-key bindings. Parity matrix and known-limitations record the `Fn` deviation. |
| Revisit trigger | Telemetry/user feedback shows the default conflicts in common apps, or a better dedicated key emerges. |
