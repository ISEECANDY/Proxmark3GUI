# Repository Source Audit

**Audited:** 2026-10-31  
**Repository:** ISEECANDY/Proxmark3GUI  
**Branches compared:** main and version-2  
**Working branch:** phase-0-project-baseline

## Executive summary

This is an existing Windows desktop application, not an empty repository. The current application is WPF on .NET 8, with an established MVVM-inspired layout, 27 XAML views, a process-based Proxmark3 client integration service, and a large vendored client source tree.

The correct next step is to improve and modernize the existing application incrementally. Replacing it with a new WinUI 3 application would discard substantial existing work and is not justified by the current audit. WPF is a native Windows desktop UI framework and can remain the UI foundation while the project is upgraded and hardened.

## Repository shape

The initial commit contains 300 tracked files:

| Area | Count / observation |
|---|---:|
| Application and documentation files outside client/ | 85 |
| Files under client/ | 215 |
| XAML views | 27 |
| XAML code-behind files | 27 |
| Main project file | Proxmark3GUI.csproj |
| Main UI | MainWindow.xaml, App.xaml |
| Main view model | ViewModels/MainViewModel.cs |
| Device/client service | Services/Pm3ConnectionService.cs |
| Command database | Services/CommandDatabase.cs |
| Static command entries | 819 in commands.json |

The client/ subtree is substantial and includes upstream-style C/C++ client sources and dependencies. Before modifying or replacing any of it, we need to identify its exact upstream revision, local modifications, build configuration, and licensing notices.

## Branch comparison

GitHub reports that version-2 is one commit ahead of main, with no commits behind. The branch delta contains only:

- Proxmark3GUI.csproj: version metadata changes from 1.0.0 to 2.0.0.
- Services/Pm3ConnectionService.cs: six added lines.
- Views/DeviceInfoView.xaml.cs: device-info view changes.

Both branches target net8.0-windows, use WPF, and have the same principal package references. This is a small branch divergence, not a separate application rewrite. The current project should be treated as the source of truth only after the user confirms which branch they have been developing against; this audit branch is based on main.

## Existing technology

The current project file specifies:

- net8.0-windows
- UseWPF enabled
- Nullable reference types and implicit usings
- Newtonsoft.Json 13.0.3
- System.IO.Ports 8.0.0
- LiveChartsCore.SkiaSharpView.WPF 2.0.0-rc2
- Microsoft.Extensions.DependencyInjection 8.0.0
- Microsoft.Extensions.Logging 8.0.0

There is no solution file in the root paths checked; the project is currently built directly from the project file. CI/build reproducibility has not been verified in this session.

## Existing application structure

The existing source is organized into Models, Services, ViewModels, Views, Converters, Resources/Assets, and client/. The application already has a dark visual theme and a broad set of RFID-oriented workspaces. Some views are functional, while others contain sample data, simulated progress, TODOs, or incomplete operations. Existing project analysis documents describe approximately 27 views and identify a mixture of implemented and placeholder functionality. Those documents are useful leads, but their claims need to be verified against the current source and hardware before being treated as test results.

## Existing client integration: strengths and risks

Pm3ConnectionService already starts the client as a hidden child process: UseShellExecute=false, CreateNoWindow=true, redirected standard input/output/error, asynchronous output reads, commands written to standard input, one active command at a time, and graceful exit followed by forced termination if needed.

This is a useful starting point and aligns with the requirement that normal use must not expose a terminal window. Preserve this approach until tests prove that it cannot reliably support the upstream client.

### Integration issues to address

1. **Idle-gap completion heuristic:** a command is considered complete after 900 ms without output. This can truncate valid output from commands that pause longer than that and can misidentify progress output as a completed result. Completion should use client prompt/operation semantics where possible, with a carefully tested fallback.
2. **Cancellation and timeout are conflated:** the linked cancellation token calls a completion path that returns accumulated text. The caller is not given a distinct cancelled or timed-out result, so an empty/partial string can be mistaken for success.
3. **Connection state is optimistic:** the service raises its connected event immediately after the process starts. A running client process does not prove that a device has been detected or initialized.
4. **Hard-coded defaults and environment assumptions:** COM9 and several fixed MSYS/ProxSpace PATH locations are embedded in the service. These should become validated configuration and runtime discovery.
5. **Output channels need explicit semantics:** stdout and stderr are read independently. We need ordered event handling and clear classification without relying on terminal colour formatting.
6. **Lifecycle races need tests:** disconnect, process exit, output callbacks, timer callbacks, cancellation, and active command completion all interact with shared state. These paths need deterministic tests.
7. **Static singleton complicates testing:** the singleton service makes isolated fake-client and parallel tests harder. Move toward an injected interface and a managed lifetime while retaining one transport owner per device.
8. **Raw command surface:** command strings are passed to the interactive client. Guided workflows need structured parameter validation, risk classification, confirmation, and redaction.

These are audit findings from source inspection, not proof that every issue reproduces on physical hardware.

## Command catalogue findings

commands.json contains **819 command entries**. This is close to the earlier rough estimate of 816, but it is not proof of 819 distinct executable operations.

The JSON represents command names, descriptions, notes, an offline flag, option strings, and usage strings. It does not provide a complete formal parameter schema, command provenance, supported client/firmware version range, runtime hardware capability requirements, risk classification, or parser/result adapters.

Preserve the existing file as one catalogue input. Reconcile it against the actual pinned Iceman client parser registrations, help output, and runtime capabilities. Parent/help nodes and aliases must not be counted as independent executable operations.

## Existing reports and code quality

The repository includes historical reports such as DETAILED_ANALYSIS_REPORT.md, COMPLETION_SUMMARY.md, and console-logging status documents. These contain useful implementation notes and lists of known gaps, but some make strong claims such as production-ready or tested. No independent build, unit-test, or hardware-test run was performed as part of this audit. Treat historical status claims as unverified until reproduced.

## Architecture decision for the next phase

**Retain WPF and modernize the existing application. Do not create a parallel WinUI 3 application at this stage.**

Recommended target direction:
- Windows 11 x64.
- Upgrade the existing WPF application from .NET 8 to .NET 10 after a clean baseline build is recorded.
- Keep the existing views and visual identity while consolidating duplicated logic and replacing placeholders incrementally.
- Introduce IPm3ClientHost / IPm3ConnectionService abstractions around the current process host.
- Separate device-process lifecycle from actual device readiness.
- Replace the idle-only completion model with explicit operation state, cancellation, timeout, and result semantics.
- Add fake-client integration tests before changing the transport implementation.
- Preserve the upstream client subtree until provenance and licensing are confirmed.

A future WinUI 3 migration can be reconsidered only if there is a concrete requirement that WPF cannot meet and a migration plan that preserves existing feature coverage.

## What was not verified

- No Windows machine/build environment was available through this GitHub-only audit.
- No dotnet restore, dotnet build, or test suite was run.
- No physical Proxmark3 device was connected.
- No client executable was launched.
- No firmware operation or RFID command was executed.
- The exact upstream commit and local patch status of the vendored client/ directory remain unverified.
- The complete contents of every one of the 300 files were not reviewed; the audit focused on project metadata, core service, command database, main view model, branch differences, and project reports.

## Audit conclusion

The project already has a meaningful foundation and a broad GUI. The priority is not to start over. The priority is to make the existing app reliable: prove a clean build, stabilize the client host, establish real device state, verify each feature against the pinned Iceman client, and add tests around the critical device-command lifecycle.