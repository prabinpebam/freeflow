# FreeFlow Windows Testing Strategy

This document defines **what "right" looks like** for the Windows port and the **method to
verify it automatically**, so an agent (or CI) can run a closed feedback loop and iterate
until the implementation is correct.

It is a companion to [`porting-plan.md`](porting-plan.md) and the
[`parity-matrix.md`](parity-matrix.md) / [`test-matrix.md`](test-matrix.md).

## 1. Philosophy

1. **Define correctness before building it.** Every subsystem ships with an executable
   specification (a test) that encodes the expected behavior. The spec is the source of
   truth, not the implementation.
2. **Everything that gates the loop must be machine-checkable and deterministic.** Real
   audio hardware, real LLM providers, and real OS focus are removed from the inner loop
   via seams and fixtures so the same input always yields the same verdict.
3. **Non-determinism is quarantined, not ignored.** LLM/STT output quality is evaluated in
   a separate, rubric-scored tier that runs out-of-band and never blocks the fast loop.
4. **Tests are protected assets.** The agent fixes the product to satisfy the spec; it must
   not weaken the spec to turn red green (see Section 9 guardrails).

## 2. The oracle problem: how we define "right"

An *oracle* is the mechanism that decides pass/fail. This app mixes deterministic logic
with non-deterministic AI output, so we use three oracle classes and assign each behavior
to one.

| Oracle class | Decides correctness by | Use for | Example |
|---|---|---|---|
| **Deterministic (exact)** | Exact value / structural equality / snapshot match | Pure logic, serialization, request shaping | Shortcut parsing, settings migration, multipart body bytes, WAV header |
| **Invariant (property)** | A property that must always hold for any input | Stateful/again-and-again behaviors | Clipboard always restored; state machine never double-records; paste-again == last successful output |
| **Judgmental (rubric/semantic)** | Score against a rubric or semantic similarity threshold | LLM cleanup, context summary, translation | "Cleaned text preserves meaning, removes fillers, fabricates nothing" |

Mapping subsystems to oracle classes:

| Subsystem | Primary oracle | Notes |
|---|---|---|
| Shortcut model / parsing / collision | Deterministic | Golden tables of binding -> serialized form |
| Settings + schema migration | Deterministic | Snapshot of migrated object per schema version |
| Provider request shaping (HTTP) | Deterministic | Byte/JSON contract snapshot, header assertions |
| State machine transitions | Invariant | Property tests over event sequences |
| Clipboard preserve/restore | Invariant | Property: post-op clipboard == pre-op snapshot |
| Audio normalization (PCM16/16k/mono) | Deterministic | Golden output bytes for a fixed input WAV |
| Transcription text (from real STT) | Judgmental | Compared to recorded expected with tolerance |
| Post-processing cleanup | Judgmental | Rubric scoring + semantic similarity |
| Context summary | Judgmental | Rubric: grounded in metadata, no fabrication |
| Paste into target app | Invariant + E2E | Received text == final pipeline text |

## 3. The golden corpus (cross-platform, already exists)

The macOS app already exports a complete, self-contained test case via
`Sources/TestCaseExporter.swift`. We adopt **that exact format** as the canonical corpus so
macOS-recorded reality becomes the Windows oracle.

Each case is a ZIP containing:

- `case.json` — inputs + recorded outputs + settings
- `audio.*` — the original recording (pipeline input)
- `screenshot.*` — optional context image (pipeline input)

`case.json` shape (from the existing exporter):

```json
{
  "id": "case-2026-02-24T10-15-03",
  "intent": "dictation | command:automatic | command:manual",
  "metadata": {
    "app_name": "Code",
    "bundle_identifier": "com.microsoft.VSCode",
    "window_title": "porting-plan.md",
    "selected_text": ""
  },
  "pipeline": {
    "raw_transcript": "…",
    "post_processed_transcript": "…",
    "context_summary": "…",
    "context_prompt": "…",
    "post_processing_prompt": "…",
    "post_processing_status": "…",
    "screenshot_status": "…",
    "audio_path": "./audio.wav",
    "screenshot_path": "./screenshot.jpg"
  },
  "settings": {
    "custom_vocabulary": "…",
    "system_prompt": "…",
    "context_system_prompt": "…"
  }
}
```

How the corpus is used on Windows:

- **Inputs** = `audio.*`, `metadata`, `settings`, `screenshot.*`.
- **Recorded outputs** = `pipeline.raw_transcript`, `post_processed_transcript`,
  `context_summary`, statuses.
- The Windows pipeline is fed the inputs and its outputs are compared to the recorded
  outputs using the oracle class for each field (exact for statuses/shape, judgmental for
  transcript/cleanup text).

Action items:

- Add a Windows `bundle_identifier` analog: record process name **and** AUMID/exe path.
- Build a tiny **corpus loader** in `FreeFlow.TestKit` that deserializes a case ZIP into a
  strongly-typed `PipelineCase` and exposes inputs/expected outputs.
- Grow the corpus from real bugs: every defect adds a regression case (Section 11).

## 4. Test taxonomy (tiers)

Tiers are tagged with a `[Trait("Tier","Lx")]` so the runner can select them. L0–L3 are the
**deterministic inner loop**; L4–L5 are heavier/out-of-band. (Reqnroll BDD scenarios map their
`@Tier:Lx` tag to `[Trait("Category","Tier:Lx")]` rather than a `Tier` trait — see §9.1 for why
the loop uses an *exclusive* tier filter.)

| Tier | Name | Scope | Hermetic? | Speed | Gates inner loop |
|---|---|---|---|---|---|
| **L0** | Unit | Pure functions, models, parsing, mappers | Yes | ms | Yes |
| **L1** | Component | One service + fakes (audio encoder, clients, state machine) | Yes | ms–s | Yes |
| **L2** | Contract | HTTP clients vs simulated provider (WireMock/cassettes) | Yes | s | Yes |
| **L3** | Pipeline integration | Full orchestration with all platform seams faked | Yes | s | Yes |
| **L4** | System / UI E2E | Real OS: hotkey -> fake audio -> fake provider -> real paste into receiver app | No (desktop session) | s–min | No (runs in CI gate, not micro-loop) |
| **L5** | Live evaluation | Real provider calls scored by rubric/semantic | No (network + secrets) | min | No (nightly/on-demand) |

## 5. Determinism strategy (the enabler)

The inner loop is only trustworthy if it is reproducible. We inject seams for every source
of non-determinism and forbid real I/O in L0–L3.

| Source of non-determinism | Seam (interface) | Inner-loop fake |
|---|---|---|
| Microphone | `IAudioCaptureService` | `FixtureAudioCapture` streams a fixed WAV at controlled timing |
| STT provider | `ITranscriptionClient` | `CassetteTranscriptionClient` keyed by input hash |
| LLM cleanup/context | `IPostProcessingClient` / `IContextService` | Cassette-backed fakes |
| Global hotkeys | `IHotkeyService` | `FakeHotkey` raises events programmatically |
| Clipboard + paste | `IClipboardPasteService` | `InMemoryClipboard` + `RecordingPasteTarget` |
| Foreground app/UIA | `IContextService` | Static metadata provider from `case.json` |
| Time / timeouts | `TimeProvider` (.NET 8) | `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) |
| GUIDs / IDs | `IIdProvider` | Seeded sequence |
| RNG | injected `Random` | Fixed seed |

Hard rules for L0–L3:

- **No network.** A test-time `HttpMessageHandler` throws on any real socket; provider calls
  must go through cassettes/WireMock.
- **No real clock waits.** All delays/timeouts advance `FakeTimeProvider`.
- **No real OS clipboard/registry/filesystem** outside an isolated temp profile.
- A **determinism guard** test runs the full L3 suite twice and asserts identical verdicts.

## 6. Executable acceptance specs (Given/When/Then)

Behavioral parity items from `parity-matrix.md` are written as BDD specs using **Reqnroll**
(SpecFlow successor). These are the human-readable definition of "right" and bind directly
to step code that drives the faked pipeline.

Example (`Dictation.feature`):

```gherkin
@Tier:L3
Feature: Hold-to-talk dictation
  Scenario: Hold shortcut transcribes and pastes cleaned text
    Given the hold shortcut is configured to "Ctrl+Alt+Space"
    And the focused app is "Notepad"
    And the microphone will play fixture "hello-world.wav"
    And the transcription provider returns cassette "hello-world"
    When the user presses and holds the hold shortcut for 1200 ms
    And releases the hold shortcut
    Then the pipeline reaches state "Completed"
    And the pasted text equals the cleaned cassette output
    And the clipboard is restored to its previous contents
```

Every `parity-matrix.md` row with a v1 target must have at least one scenario before that
row can be marked "Parity".

## 7. Golden / snapshot testing

Use **Verify (Verify.Xunit)** for any structured output where exact shape matters but hand-
writing the expected value is noisy:

- Serialized settings per schema version (migration golden files)
- Outgoing provider HTTP request (method, path, headers, normalized multipart/JSON body)
- Persisted history record (`PipelineHistoryItem` analog)
- Normalized audio header + frame stats for a fixture WAV

Snapshot promotion is **gated**: Verify writes `*.received.*`; turning it into `*.verified.*`
is a reviewed action (Section 9), never an automatic agent step.

## 8. AI output evaluation harness (judgmental tier)

LLM/STT correctness cannot use exact match. We define a rubric-based evaluator in
`FreeFlow.Eval` used by L3 (against cassettes) and L5 (against live providers).

### 8.1 Scoring methods

| Method | Used for | Pass condition (tunable) |
|---|---|---|
| Normalized exact / token F1 | Transcript vs recorded transcript | F1 >= threshold (e.g. 0.9) after casing/punctuation normalization |
| Semantic similarity | Cleanup/translation meaning preserved | cosine(embedding(actual), embedding(expected)) >= threshold |
| Rubric checks (deterministic) | Cleanup rules | All must hold: no fabricated entities; fillers removed; vocabulary terms preserved; non-empty unless input empty |
| Structural guards | Output hygiene | No prompt leakage / no "Here is the cleaned…" preamble; respects EMPTY contract |

### 8.2 Rubric encoded as checks

The cleanup rubric mirrors the app's own post-processing contract (see
`Sources/PostProcessingService.swift`): preserve meaning, remove fillers, never invent
names/terms not spoken, preserve language unless translation is explicitly requested. Each
clause becomes an assertion the evaluator can run deterministically over the output (e.g. a
fabrication check compares named entities in output against input + vocabulary).

### 8.3 Dataset & thresholds

- Eval dataset = the golden corpus (Section 3) + curated edge cases (accents, noise,
  jargon, code, multilingual, empty audio).
- Thresholds are config in `eval.config.json`, versioned, and reviewed when changed.
- L5 produces a **score report** (per-case scores + aggregate) and trends over time; it can
  *soft-gate* releases (regression beyond tolerance fails the release pipeline, not the
  inner loop).

## 9. The agentic loop

Goal: an autonomous run/evaluate/fix cycle on the deterministic tiers.

### 9.1 Single machine-readable contract

One command runs the loop tiers and emits a structured verdict:

```powershell
# Run deterministic tiers, emit TRX (one per assembly) + coverage
dotnet test windows/FreeFlow.Windows.sln `
  --filter "Tier!=L4&Tier!=L5" `
  --logger "trx;LogFilePrefix=results" `
  --results-directory artifacts/test `
  --collect:"XPlat Code Coverage"

# Project TRX + Verify diffs + eval scores into a single verdict.json
pwsh -File scripts/windows/build-verdict.ps1 -ResultsDir artifacts/test -Out artifacts/test/verdict.json
```

> **Filter form matters (Reqnroll trait mapping).** Plain xUnit tests carry `[Trait("Tier","L0".."L3")]`.
> Reqnroll maps a feature/scenario tag such as `@Tier:L3` to `[Trait("Category","Tier:L3")]` — **not** to
> a `Tier` trait. An *inclusive* filter (`Tier=L0|...|L3`) therefore silently skips every BDD scenario.
> The *exclusive* form `Tier!=L4&Tier!=L5` runs all inner-loop tests (untagged, `Tier`-tagged, and
> Reqnroll `Category`-tagged) and excludes only the slow outer tiers. Keep L4 (UI/FlaUI) and L5
> (live-eval) as plain xUnit tests tagged `[Trait("Tier","L4")]` / `[Trait("Tier","L5")]` so the
> exclusion stays reliable. This is verified by the scaffold: the inclusive filter runs 23 tests, the
> exclusive filter runs all 24 (the 24th being the `Dictation.feature` scenario).
>
> **TRX naming.** Use `LogFilePrefix=results` (not a fixed `LogFileName=results.trx`): on a multi-project
> solution every test assembly writes to the same results directory, and a fixed filename makes the last
> assembly overwrite the others. `LogFilePrefix` emits one uniquely-named `results_*.trx` per assembly,
> and `build-verdict.ps1` aggregates them recursively.

`verdict.json` (the agent's only required input):

```json
{
  "run_id": "2026-06-19T10-50-00",
  "summary": { "total": 412, "passed": 408, "failed": 4, "skipped": 0, "eval_score": 0.93 },
  "determinism_ok": true,
  "failures": [
    {
      "id": "Pipeline.HoldToTalk.PastesCleanedText",
      "tier": "L3",
      "oracle": "invariant",
      "spec_ref": "features/Dictation.feature:12",
      "expected_ref": "artifacts/test/expected/hold.txt",
      "actual_ref": "artifacts/test/actual/hold.txt",
      "diff_path": "artifacts/test/diff/hold.diff",
      "category": "clipboard-not-restored",
      "suggested_locus": "FreeFlow.Platform/Clipboard/ClipboardPasteService.cs",
      "reproduce_cmd": "dotnet test --filter FullyQualifiedName~HoldToTalk.PastesCleanedText"
    }
  ]
}
```

### 9.2 Loop algorithm

1. Run deterministic tiers; produce `verdict.json`.
2. If `failed == 0` and `determinism_ok == true`: done (optionally run L4/L5 gates).
3. Otherwise, for each failure: open `suggested_locus`, read `diff_path` and `spec_ref`,
   edit **product code** to satisfy the spec.
4. Re-run only failed tests via each `reproduce_cmd` (fast).
5. When the failed subset is green, re-run the full deterministic suite (catch regressions).
6. Repeat until green, or a stop condition triggers.

### 9.3 Stop / escalate conditions

- `max_iterations` reached.
- **No-progress**: the same failure signature persists N iterations.
- A change would require editing a **protected** path (escalate to human).

### 9.4 Guardrails (prevent "cheating" the spec)

- **Protected oracles**: `**/features/**`, `**/*.verified.*`, `docs/windows/**`, the golden
  corpus, and `eval.config.json` thresholds. The agent may not modify these to pass; changes
  require an explicit, separately-reviewed step.
- **Snapshot promotion is manual**: `*.received.*` -> `*.verified.*` is human/diff-gated.
- **Mutation gate (Stryker.NET)**: periodically mutate product code; tests must catch a
  minimum mutation score, proving the suite actually constrains behavior (stops the agent
  from writing vacuous tests).
- **Flaky quarantine**: a test that only passes on retry is reported as flaky, not passed.
- **Determinism guard** (Section 5) must stay green.

## 10. CI/CD integration

| Stage | Tiers | Trigger | Gate |
|---|---|---|---|
| PR fast check | L0–L3 + determinism guard + coverage | every push | Required |
| PR UI check | L4 (FlaUI on windows runner w/ interactive session) | every PR | Required (can retry-quarantine) |
| Nightly eval | L5 live scoring + mutation gate | schedule | Soft-gate / report |
| Release | L0–L4 + L5 regression within tolerance | tag | Required |

Notes specific to this stack:

- **L4 UI automation requires an interactive desktop session.** Use a Windows runner with
  autologon (or self-hosted) so focus, hotkeys, and paste work; headless agents cannot
  validate real `SendInput`/focus behavior.
- Coverage via **Coverlet** + **ReportGenerator**; enforce a line/branch threshold on
  `FreeFlow.Core` and `FreeFlow.Infrastructure` (platform code is covered more by L3/L4).

## 11. Test data & corpus management

- **Cassettes** (recorded HTTP) live in `windows/tests/fixtures/cassettes/`, keyed by a hash
  of the normalized request; re-record via an explicit, reviewed task.
- **Audio fixtures** in `windows/tests/fixtures/audio/` (short, license-clean clips +
  synthetic edge cases: silence, noise, clipping, very long).
- **Golden corpus** (exported cases) in `windows/tests/fixtures/cases/`.
- **Secrets** (L5 only) come from CI secrets / local user-secrets, never committed.
- **Regression growth**: each fixed P0/P1 bug must add (a) a failing test reproducing it and
  (b) a corpus case if pipeline-related, before the fix merges.

## 12. Recommended tooling

| Concern | Tool | Rationale |
|---|---|---|
| Test framework | xUnit | First-class .NET 8, parallelism, traits for tiers |
| Assertions | FluentAssertions | Readable failure messages |
| Fakes/mocks | NSubstitute | Simple, clean syntax |
| BDD acceptance | Reqnroll | Living spec tied to step code (SpecFlow successor) |
| Snapshots | Verify | Robust golden/approval testing |
| HTTP simulation | WireMock.NET + cassette handler | Contract + replay |
| Property tests | FsCheck or CsCheck | Invariants over generated event sequences |
| Deterministic time | `TimeProvider` + Microsoft.Extensions.TimeProvider.Testing | Built into .NET 8 |
| UI automation | FlaUI (UIA3); Appium.WindowsDriver alt | Works with WinUI 3 |
| Coverage | Coverlet + ReportGenerator | CI thresholds |
| Mutation | Stryker.NET | Proves tests are meaningful |
| Perf budgets | BenchmarkDotNet + budget asserts | Validates Section 8 budgets of porting-plan |

## 13. Project / directory layout

```text
windows/
  tests/
    FreeFlow.Core.Tests/           # L0–L1
    FreeFlow.Infrastructure.Tests/ # L1–L2 (HTTP contracts, persistence)
    FreeFlow.Platform.Tests/       # L1 with fakes where possible
    FreeFlow.Pipeline.Tests/       # L3 orchestration + Reqnroll features
    FreeFlow.System.Tests/         # L4 FlaUI E2E + receiver app
    FreeFlow.Eval/                 # judgmental scoring lib + L5 runner
    FreeFlow.TestKit/              # corpus loader, fakes, cassette handler, fixtures API
    fixtures/
      audio/  cassettes/  cases/  snapshots/
  scripts/windows/
    build-verdict.ps1              # TRX + diffs + scores -> verdict.json
    run-agentic-loop.ps1           # orchestrates run -> verdict (loop driver scaffold)
```

## 14. Definition of done (testing) per release

A release candidate is "right" when:

1. **Parity coverage**: every `parity-matrix.md` row targeted for the release has ≥1 passing
   acceptance scenario.
2. **Inner loop green**: L0–L3 + determinism guard pass; coverage thresholds met.
3. **System green**: L4 E2E passes on the compatibility matrix subset.
4. **Eval within tolerance**: L5 aggregate score ≥ threshold and no per-case regression
   beyond tolerance vs the previous release.
5. **Suite is meaningful**: mutation score ≥ gate; zero unresolved flaky-quarantined tests
   on the core path.
6. **No protected-oracle edits** snuck in to force green (verified in review).

## 15. Anti-patterns to avoid

- Asserting exact strings on LLM output (use judgmental oracle instead).
- `Thread.Sleep` for timing (advance `FakeTimeProvider`).
- Hitting real providers in L0–L3 (breaks determinism + costs money/flakiness).
- Letting the agent edit snapshots/specs to pass.
- UI tests that assert on pixels instead of UIA element values.
- One mega end-to-end test as the only coverage (no fault localization).

## 16. Immediate next actions

1. Stand up `FreeFlow.TestKit` (corpus loader + fakes + cassette handler) alongside the
   Phase 2 solution scaffold.
2. Implement `build-verdict.ps1` and the `verdict.json` contract early so the loop exists
   before features grow.
3. Export an initial golden corpus from the macOS app and commit a small, license-clean
   subset to `fixtures/cases/`.
4. Write the first Reqnroll scenarios for the v1 dictation path and wire them to the faked
   pipeline.
5. Add the determinism guard and a mutation-gate job before the corpus grows large.
