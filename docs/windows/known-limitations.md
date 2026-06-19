# FreeFlow Windows Known Limitations

Track user-visible limitations, temporary workarounds, and planned fixes.

## Template

| ID | Area | Limitation | User impact | Workaround | Target fix version | Status | Notes |
|---|---|---|---|---|---|---|---|
| LIM-001 | Example | Describe limitation | High/Medium/Low | Suggested workaround | vX.Y | Open | |

## Active limitations

| ID | Area | Limitation | User impact | Workaround | Target fix version | Status | Notes |
|---|---|---|---|---|---|---|---|
| LIM-001 | Input | macOS `Fn` / `Command-Fn` defaults cannot be reproduced — `Fn` is not interceptable on Windows | Medium | New Windows defaults (Right Ctrl hold, Ctrl+Alt+Space toggle), fully remappable | v1 | Open | ADR-009 |
| LIM-002 | Paste | `SendInput` paste fails into elevated/admin windows unless the app is also elevated (UIPI) | Medium | Run FreeFlow elevated, or fall back to clipboard + manual paste with a clear prompt | v1 | Open | Surface a focused-window elevation hint |
| LIM-003 | UX | WinUI 3 has no built-in tray icon; relies on the H.NotifyIcon dependency | Low | Bundled library (ADR-007) | v1 | Open | Validated by L4 UI tests |
| LIM-004 | Input | Global low-level keyboard hook can be blocked/slowed by some security software or another hook | Medium | Diagnostics + guidance; allow rebinding; document AV allowlisting | v1 | Open | Risk shared with porting-plan risk register |
| LIM-005 | Editing | Voice Macros are not in v1 | Low | Use command/edit mode; macros arrive in v2 | v2 | Deferred | ADR-006 |
| LIM-006 | Pipeline | Realtime/streaming transcription is not in v1 (batch transcription only) | Low | Batch transcribe after recording stops | v2 | Deferred | macOS RealtimeTranscriptionService |
| LIM-007 | Context | Selected-text and window-title capture quality varies by app UI-Automation support | Medium | UI Automation TextPattern first (side-effect-free), then clipboard-copy probe for normal apps; terminals/consoles are UIA-only (never Ctrl+C); apps with neither are marked unsupported | v1 | Mitigated | UiaSelectionReader + AppCompatibilityPolicy + PolicyAwareSelectionReader (Phase 7 baseline) |
