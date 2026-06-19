# FreeFlow Windows Parity Matrix

Track behavior-level parity between macOS FreeFlow and Windows FreeFlow.

## Status legend

- **Not started**
- **In progress**
- **Partial**
- **Parity**
- **Deferred**

## Matrix

| Area | Feature | macOS behavior reference | Windows expected behavior | v1 target | v2 target | Status | Owner | Notes |
|---|---|---|---|---|---|---|---|---|
| Input | Hold-to-talk shortcut | Start on press, stop on release | Same | Yes | Yes | Not started | | |
| Input | Toggle shortcut | Start/stop on repeated trigger | Same | Yes | Yes | Not started | | |
| Input | Paste-again shortcut | Repaste latest successful output | Same | Yes | Yes | Not started | | |
| Input | Shortcut capture UX | Capture + validate + conflict message | Equivalent | Yes | Yes | Not started | | |
| Audio | Mic selection | User-selected or default fallback | Equivalent | Yes | Yes | Not started | | |
| Audio | Level meter | Live meter during recording | Equivalent | Yes | Yes | Not started | | |
| Pipeline | Transcription API | Configurable model/base URL/key | Equivalent | Yes | Yes | Not started | | |
| Pipeline | Post-processing API | Prompted cleanup with fallback model | Equivalent | Yes | Yes | Not started | | |
| Pipeline | Timeouts | Configurable override values | Equivalent | Yes | Yes | Not started | | |
| Context | App/window metadata | Foreground app and window title | Equivalent fallback-aware | Partial | Yes | Not started | | |
| Context | Selected text | Capture current selected text | Baseline in supported apps | Partial | Yes | Not started | | |
| Context | Screenshot context | Optional screenshot prompt input | Optional | Optional | Yes | Not started | | |
| Editing | Command/edit mode | Transform selected text via voice instruction | Baseline | Partial | Yes | Not started | | |
| Paste | Clipboard preserve/restore | Preserve full clipboard snapshot | Equivalent | Yes | Yes | Not started | | |
| Paste | Enter-after-paste policy | Optional behavior by command flow | Equivalent | Yes | Yes | Not started | | |
| UX | Setup wizard | Guided onboarding with checks | Equivalent | Yes | Yes | Not started | | |
| UX | Settings tabs | Full settings UI | Equivalent | Yes | Yes | Not started | | |
| UX | Run log/history | Session list with outputs and errors | Equivalent | Yes | Yes | Not started | | |
| UX | Overlay/notifications | Recording + processing + errors | Equivalent baseline | Yes | Yes | Not started | | |
| Release | Update checks | Version parsing and update prompts | Baseline | Yes | Yes | Not started | | |
| Release | Signed artifacts | Installable release output | Equivalent trust level | Yes | Yes | Not started | | |

## Parity review checklist

| Check | Result | Notes |
|---|---|---|
| Core dictation workflow parity reviewed | Pending | |
| Shortcut behavior edge cases reviewed | Pending | |
| Clipboard safety parity reviewed | Pending | |
| Context/edit mode deltas documented | Pending | |
| Known intentional deviations approved | Pending | |
