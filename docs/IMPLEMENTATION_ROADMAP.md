# Implementation Roadmap

The project is delivered in gated phases. Each phase must have acceptance criteria, automated tests where practical, and a clear status report before the next phase is treated as complete.

**Strategy:** evolve the existing WPF application. Do not replace it with a parallel WinUI 3 application unless a documented requirement justifies that migration.

## Phase 0 — Repository and technical baseline

- [x] Confirm repository access and default branch.
- [x] Create an isolated audit branch.
- [x] Inspect the initial source inventory (300 tracked files).
- [x] Inspect project metadata, main client service, command database, main view model, and historical reports.
- [x] Compare main and version-2.
- [x] Record the existing WPF/.NET 8 architecture and key integration risks.
- [ ] Confirm which development branch is the user's preferred source of truth.
- [ ] Verify the vendored client revision and local modifications.
- [ ] Confirm upstream licensing and distribution plan.

**Gate:** existing application code is preserved; architecture decisions are based on the actual repository rather than a greenfield assumption.

## Phase 1 — Reproducible foundation

- Establish a Windows 11 development/build environment.
- Record a clean baseline build of the current .NET 8 WPF project before changing target frameworks.
- Pin .NET SDK and NuGet dependencies.
- Upgrade to .NET 10 only after baseline build and dependency compatibility are verified.
- Add or adapt a solution structure without breaking the existing project.
- Replace static singleton service access with injectable interfaces and managed lifetimes.
- Add structured logging and centralized error handling.
- Add xUnit unit and integration test projects.
- Add Windows CI for restore, build, and tests.
- Define coding standards and contribution guidance.

**Gate:** reproducible clean Windows build and passing foundation tests. No claim of completion until the Windows build actually runs.

## Phase 2 — Stabilize the Iceman client host

- Preserve hidden child-process hosting as the initial integration path.
- Add a fake client executable for deterministic tests.
- Test process start/exit, stdout/stderr, output ordering, cancellation, timeout, disconnect, and forced shutdown.
- Replace the 900 ms idle-only command completion heuristic with explicit completion detection where possible and a tested fallback.
- Return distinct success, failure, cancellation, timeout, process-crash, and disconnect outcomes.
- Make client executable path, working directory, environment, and serial settings configurable.
- Separate process running from device detected/initialized/ready states.

**Gate:** all process-host integration tests pass without a visible terminal.

## Phase 3 — Device discovery and identity

- Enumerate serial ports and provide manual selection.
- Track USB arrival/removal and disconnect events.
- Identify hardware, bootrom/firmware, client version, and device capabilities.
- Model lifecycle states and actionable errors.
- Enforce one transport owner per physical device.

**Gate:** simulated and hardware tests cover connect, unplug, reconnect, and readiness.

## Phase 4 — Command catalogue and execution engine

- Preserve the existing commands.json as an input source.
- Reconcile entries with parser registrations, client help output, and runtime capabilities from the pinned Iceman revision.
- Track provenance, aliases, parent/help nodes, parameter schemas, risk, and version compatibility.
- Provide global search, guided forms, favourites, recent operations, and advanced raw-command mode.
- Validate parameters and require confirmation for state-changing/sensitive actions.
- Redact secrets from logs by default.

**Gate:** catalogue coverage and execution behaviour are verified against the pinned upstream client. Documented entries are not automatically treated as executable commands.

## Phase 5 — Live output and operation history

- Stream raw output and structured events.
- Preserve output ordering and source channel.
- Parse progress/results without depending on terminal colour codes.
- Add operation IDs, timestamps, status transitions, cancellation, and history.
- Persist metadata without storing large dumps or plaintext secrets in SQLite.

## Phase 6 — Existing feature completion

Audit and complete existing views rather than rebuilding them blindly. Prioritize:
- Commands and Quick Actions
- Device Info and Diagnostics
- Dump Explorer / Dump Center
- Reader/Writer and MIFARE tools
- LF/HF protocol workspaces
- Trace Analyzer
- Script Manager and Automation
- Tag Database and Key Manager
- Firmware Manager / Recovery

Each view must be classified as real hardware-backed, file-backed, simulated, or placeholder. Remove misleading sample-success states.

## Phase 7 — Data, secrets, and persistence

- Add robust dump import/export and format identification.
- Store large artifacts on disk with indexed metadata.
- Protect key material using Windows-protected storage.
- Redact secrets from normal logs and support bundles.
- Add durable tag/profile/session metadata.

## Phase 8 — Automation and reporting

- User-defined workflows with preconditions, validated parameters, expected outcomes, cancellation, and failure handling.
- No silent retries for non-idempotent operations.
- Exportable session and operation reports with configurable redaction.

## Phase 9 — Firmware and diagnostics hardening

- Verify client/firmware compatibility before flashing.
- Require explicit confirmation for firmware operations.
- Provide recovery guidance and clear failure states.
- Export redacted diagnostics.

## Phase 10 — Plugin system

Only after core interfaces are stable. Plugins must declare compatibility, permissions, and supported capabilities.

## Phase 11 — Release hardening

- Windows installer and code signing.
- Update strategy.
- GPL notices and corresponding-source compliance for distributed upstream components.
- Supported hardware/client matrix.
- Regression suite, release notes, and recovery documentation.

## Product areas

Dashboard · Device Manager · Command Explorer · Activity · Sessions · HF RFID · LF RFID · NFC/protocol workspaces · Dump Center · Key Manager · Firmware Center · Diagnostics · Automation · Reports · Documentation · Plugin Manager.

## Definition of done for a phase

- Acceptance criteria are written and met.
- Relevant unit/integration tests pass.
- Failure and cancellation paths are covered.
- No unsupported capability is presented as available.
- User-facing changes are documented.
- Build/test status is reported accurately, including tests not run.