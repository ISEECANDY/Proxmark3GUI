# Client Host Hardening — Initial Change

**Branch:** phase-1-client-host-hardening  
**Base:** version-2  
**Status:** source change committed; Windows build and runtime tests not yet run.

## Why this change was made

The existing client host used one TaskCompletionSource<string> for command completion. Its cancellation registration called the same success-completion method used by the 900 ms idle timer. As a result, cancellation and the 45-second timeout could complete the task with partial output as though the command had succeeded.

## Change committed

In Services/Pm3ConnectionService.cs:

- Caller cancellation now completes the active command as a cancelled task.
- The internal 45-second deadline now completes as TimeoutException.
- Client process exit during an active command now completes it with IOException.
- Disconnect during an active command now completes it with IOException instead of returning an empty string as a success-shaped result.
- Process exit handling is now a named handler, keeping the lifecycle path easier to test and extend.

## Compatibility

The public method signatures remain unchanged. Existing callers that await SendCommandAsync or SendCommandRawAsync can now receive OperationCanceledException/TaskCanceledException, TimeoutException, or IOException in failure cases. Callers should not treat every returned string as the only possible outcome; UI command execution should catch and display these distinct conditions.

## Known remaining risks

1. The 900 ms idle-gap heuristic still decides ordinary command completion and can truncate commands that pause longer than the idle window.
2. ConnectAsync still reports process startup as connected; it does not yet prove the device has initialized.
3. MSYS runtime-path assumptions remain hard-coded; the COM9 default has now been removed.
4. The service remains a static singleton and is not yet isolated behind a testable interface.
5. No fake-client integration tests have been added yet.

## Verification status

- Source updated on the isolated branch.
- No Windows build was available in this GitHub-only session.
- No unit or integration tests were run.
- No physical device was connected.

Next engineering step: extract a testable client-host abstraction and add a fake executable/process harness before changing command completion semantics.

## Explicit command completion marker

A second change adds an explicit completion signal for the GUI's redirected-stdin mode.

### Upstream client behaviour found during source inspection

The vendored `client/src/proxmark3.c` reads commands with `fgets(..., stdin)` when stdin is a pipe. In this mode, it prints the command prompt before executing a command, but it does not return to the interactive prompt-reading path after each command. Therefore, waiting for a normal trailing `pm3 -->` prompt is not a valid completion strategy for this launch mode.

### Change committed

- After `CommandReceived(cmd)` returns, the client prints the private line `__PM3GUI_COMMAND_COMPLETE__` when `stdinOnPipe` is true.
- `Pm3ConnectionService` recognises that exact line, suppresses it from normal output events, and completes the active command immediately.
- The existing 900 ms idle-gap completion remains as a compatibility fallback for older prebuilt client executables that do not yet emit the marker.

### Deployment requirement

The GUI must run a `proxmark3.exe` rebuilt from the modified vendored client source to use explicit completion. An older executable will continue to use the idle-gap fallback. The marker change has not yet been compiled or exercised on Windows, so it is source-level implementation only until a Windows build and fake-client/hardware test are completed.

### Important edge cases still to test

- Commands that produce no output before the marker.
- Long-running commands and progress output.
- Commands that return an error status.
- Fatal client errors and process exit before the marker.
- Marker handling when output is split across pipe read boundaries (the current line-based reader should deliver the complete marker line).
- Ensuring marker output cannot be mistaken for user-visible command output.

## Windows build automation

A GitHub Actions workflow has been added at `.github/workflows/windows-build.yml`.

- Runs on `windows-latest` for pushes and pull requests targeting `main`, `version-2`, or this hardening branch.
- Installs the .NET 8 SDK.
- Restores and builds the WPF project in Release configuration.
- Does not claim to build the vendored C client or validate device communication.

The workflow has been committed but its first hosted run has not yet been observed. A green CI result is still required before treating the WPF project as build-verified. The next step remains to introduce a controllable client-process seam and automated fake-client tests; CI can then be extended to run those tests.

## Explicit serial-port selection

The connection service no longer defaults to COM9. Its port value starts empty, and `ConnectAsync` rejects a missing selection instead of silently targeting a guessed port. The Settings view now refreshes the actual ports reported by Windows, presents a neutral “Select a serial port...” entry, preserves a still-valid prior selection, and requires a real port entry before applying connection settings. This avoids connecting to an unrelated serial device merely because it happens to occupy a conventional COM number.

This is a source-level change only; the WPF workflow still needs its first successful run. Hardware enumeration and connection have not been tested on a Windows machine with a Proxmark3 attached.


## Configured client executable path

A settings-path defect was found during the next source review: the UI stored `Pm3ExePath`, but the process launcher ignored it and searched only fixed bundled locations. The settings page also started with a directory-like legacy value rather than an executable path.

The launcher now:

- Uses the configured executable path exactly when one is supplied.
- Validates that the selected file exists and reports the missing path instead of silently launching a different client.
- Uses the bundled `client\\proxmark3.exe` only when the configured path is empty.
- Sets the working directory to the selected executable's directory so adjacent client runtime files can be resolved.

The Settings page now defaults and resets to the bundled executable path. This fixes configuration consistency at source level, but has not yet been compiled or exercised on Windows. The fixed MSYS runtime PATH candidates remain a separate portability issue.

A related settings review found the initial baud-rate selection used ComboBox index 2 (38,400 baud) despite a comment claiming 115,200. The default selection now uses index 4, matching the actual 115,200 option and the XAML default. The Reset action already selected index 4. This is a source correction only; no serial-device test has been run.
