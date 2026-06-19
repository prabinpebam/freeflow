# FreeFlow Windows Test Matrix

This matrix tracks validation coverage for OS versions, hardware, devices, and target applications.

## 1. Environment matrix

| Dimension | Required coverage | Status | Notes |
|---|---|---|---|
| OS | Windows 11 23H2+ | Pending | |
| OS | Windows 10 22H2 (if supported) | Pending | |
| CPU | Intel | Pending | |
| CPU | AMD | Pending | |
| Audio device | Built-in microphone | Pending | |
| Audio device | USB headset microphone | Pending | |
| Audio device | Bluetooth microphone | Pending | |
| Display | Single monitor | Pending | |
| Display | Multi-monitor | Pending | |

## 2. Application compatibility matrix

| App | Dictation | Paste reliability | Selected text capture | Command mode baseline | Notes |
|---|---|---|---|---|---|
| Notepad | Pending | Pending | Pending | Pending | |
| Word | Pending | Pending | Pending | Pending | |
| VS Code | Pending | Pending | Pending | Pending | |
| PowerShell | Pending | Pending | Pending | Pending | |
| Windows Terminal | Pending | Pending | Pending | Pending | |
| Chrome textarea | Pending | Pending | Pending | Pending | |
| Edge textarea | Pending | Pending | Pending | Pending | |
| Slack desktop | Pending | Pending | Pending | Pending | Optional |
| Outlook desktop | Pending | Pending | Pending | Pending | Optional |

## 3. Core scenario checklist

| Scenario | Expected result | Status | Defect ID / Notes |
|---|---|---|---|
| Fresh install and setup completion | User reaches first dictation successfully | Pending | |
| Hold shortcut dictation flow | Start/stop works with no stuck state | Pending | |
| Toggle shortcut dictation flow | Toggle starts/stops reliably | Pending | |
| Paste-again flow | Latest successful transcript repastes correctly | Pending | |
| Clipboard restore | Original clipboard content preserved/restored | Pending | |
| Mic disconnect mid-recording | Error handled, no crash, recovery path available | Pending | |
| Provider auth error | Clear actionable error shown | Pending | |
| Provider timeout | Timeout surfaced and recoverable | Pending | |
| Context unavailable | Fallback behavior used without crash | Pending | |
| App switch before paste | Behavior deterministic and documented | Pending | |

## 4. Performance and stability checks

| Check | Target | Status | Notes |
|---|---|---|---|
| Hotkey press -> recording indicator | Under defined budget | Pending | |
| Stop recording -> request dispatch | Under defined budget | Pending | |
| Final text ready -> paste dispatch | Under defined budget | Pending | |
| Long-running session stability | No crash during endurance run | Pending | |
| Idle resource usage | Within budget | Pending | |

## 5. Release gate summary

| Gate | Requirement | Status | Notes |
|---|---|---|---|
| Automated test suite | Pass | Pending | |
| Manual smoke suite | Pass | Pending | |
| P0/P1 defects | Zero open | Pending | |
| Known limitations | Documented | Pending | |
| Signed package install/upgrade | Verified | Pending | |
