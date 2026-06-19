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
| ADR-001 | Packaging model (MSIX vs signed EXE installer) | Before Phase 1 | Proposed | |
| ADR-002 | Update channel strategy | Before Phase 1 | Proposed | |
| ADR-003 | Supported Windows versions | Before test matrix lock | Proposed | |
| ADR-004 | Telemetry and diagnostics policy | Before beta | Proposed | |
| ADR-005 | Context/screenshot defaults | Before setup UX finalization | Proposed | |
| ADR-006 | Command mode v1 scope | Before v1 freeze | Proposed | |
| ADR-007 | Tray-icon component (no built-in WinUI 3 tray API) | Before Phase 1 | Proposed | e.g., H.NotifyIcon vs custom |
| ADR-008 | AI-eval thresholds + golden-corpus ownership | Before Phase 3 | Proposed | Who tunes scores; how corpus is curated/grown |

## Decision history

Add accepted/superseded ADR entries below this line.
