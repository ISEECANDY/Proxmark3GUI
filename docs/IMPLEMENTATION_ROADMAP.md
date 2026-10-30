# Implementation Roadmap

The project is delivered in gated phases. Each phase must have acceptance criteria, automated tests where practical, and a clear status report before the next phase is treated as complete.

## Phase 0 — Repository and technical baseline

- [x] Confirm repository access and default branch.
- [x] Create an isolated audit branch.
- [x] Record known repository facts and limitations.
- [ ] Complete a full source-tree audit of `main` and `version-2`.
- [ ] Reconcile existing code with the intended product architecture.
- [ ] Confirm the exact upstream Iceman revision and licensing plan.

**Gate:** no existing project structure is replaced until the full tree has been inspected.

## Phase 1 — Foundation

- Establish the Windows 11 / .NET 10 build environment.
- Pin SDK, Windows App SDK, and NuGet dependencies.
- Create or adapt the solution and project layout.
- Add dependency injection, configuration, structured logging, and error handling.
- Add unit-test and integration-test projects.
- Add CI that builds and tests on Windows.
- Define coding standards and repository contribution guidance.

**Gate:** clean Windows build and passing foundation tests.

## Phase 2 — Native GUI shell

- WinUI 3 navigation shell and application lifecycle.
- Dashboard, reusable controls, theme resources, and status bar.
- Live activity/output panel.
- Accessible keyboard navigation and scalable layouts.

**Gate:** app launches without a terminal and navigation tests pass.

## Phase 3 — Device discovery

- Enumerate serial devices and provide manual port selection.
- Track connect/disconnect and USB arrival/removal.
- Model device lifecycle and error states.
- Never allow competing transport owners for one device.

**Gate:** hardware and simulated-device discovery tests pass.

## Phase 4 — Iceman integration proof

- Pin and build the upstream client on Windows.
- Test hidden process hosting and redirected standard input/output.
- Verify interactive command behaviour, output ordering, cancellation, timeouts, and shutdown.
- Decide whether a narrow native bridge is necessary based on evidence.

**Gate:** a documented, repeatable integration test works without a visible console.

## Phase 5 — Device identity and capabilities

- Identify hardware, bootrom/firmware, and client versions.
- Build a capability model from runtime evidence.
- Clearly distinguish unsupported, unavailable, and unknown features.

## Phase 6 — Command catalogue and execution engine

- Merge command parser registrations, help output, documentation, and runtime capability data.
- Track provenance, aliases, parent/help nodes, parameters, risks, and version compatibility.
- Provide global search, guided parameter forms, favourites, and recent operations.
- Record structured operation lifecycle and redacted diagnostics.

**Gate:** catalogue coverage and execution behaviour are tested against the pinned upstream client.

## Phase 7 — Live output and operation history

- Stream output and structured events.
- Preserve raw output for diagnostics.
- Parse progress/results without depending on terminal colour codes.
- Persist operation history and support cancellation.

## Phase 8 — Research workspaces

Build capability-aware workspaces incrementally for supported LF, HF, NFC, and protocol families. Do not expose controls that the connected hardware/client cannot support.

## Phase 9 — Dump Center

- File browser, import/export, metadata, and format identification.
- Safe handling of large files.
- Clear provenance and operation linkage.

## Phase 10 — Key Manager

- Protected secret storage.
- Redaction by default.
- Explicit user intent for sensitive operations.
- No plaintext key material in ordinary logs or SQLite metadata.

## Phase 11 — Firmware Center

- Firmware/client compatibility checks.
- Clear warnings and confirmation for flashing.
- Recovery guidance and failure-state reporting.

## Phase 12 — Diagnostics

- Device, client, firmware, and application diagnostics.
- Exportable redacted support bundle.

## Phase 13 — Automation

- User-defined workflows with preconditions, validated inputs, expected outcomes, cancellation, and failure handling.
- No silent retries for non-idempotent operations.

## Phase 14 — Reporting

- Exportable operation/session reports.
- Configurable redaction and artifact references.

## Phase 15 — Plugin system

Only after core interfaces are stable. Plugins must declare compatibility, permissions, and supported capabilities.

## Phase 16 — Release hardening

- Windows installer and signing.
- Update strategy.
- GPL notices and corresponding-source compliance for distributed upstream components.
- Hardware support matrix, regression suite, release notes, and recovery documentation.

## Product areas

Dashboard · Device Manager · Command Explorer · Activity · Sessions · HF RFID · LF RFID · NFC/protocol workspaces · Dump Center · Key Manager · Firmware Center · Diagnostics · Automation · Reports · Documentation · Plugin Manager.

## Definition of done for a phase

- Acceptance criteria are written and met.
- Relevant unit/integration tests pass.
- Failure and cancellation paths are covered.
- No unsupported capability is presented as available.
- User-facing changes are documented.
- Build/test status is reported accurately, including any tests not run.
