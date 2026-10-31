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

## Startup connection safety and baud-rate consistency

A follow-up review found that the main view model still attempted an automatic connection to COM9 during application startup. This bypassed the new explicit-port-selection workflow in Settings and could attach to an unrelated serial device. Startup no longer launches the client or selects a port automatically; the operator must initiate connection after choosing the intended port.

The connection service's own default baud rate also differed from the Settings page's selected default. It now defaults to 115200 so callers that do not explicitly apply Settings do not silently launch with 460800 while the UI indicates 115200.

These are source-level corrections. Windows build, serial enumeration, and physical Proxmark3 connection tests remain outstanding.

## Command catalogue deployment path

The application previously loaded `commands.json` using a working-directory-relative path. That can fail when the GUI is launched from a shortcut or another directory. Startup now resolves the catalogue relative to `AppContext.BaseDirectory`, matching the project configuration that copies the file beside the application executable.

This prevents launch location from changing whether the command browser can load its catalogue. It remains unverified by a Windows build or packaged-app launch test.

## Process lifecycle and connection-state wording

The client host now cleans up a failed process start (event subscriptions, process handle, and stored reference) rather than leaving a stale process object behind. The process-exit handler only updates state when the exiting process is still the active client, preventing a late event from an old process from marking a newer session as disconnected.

Connection notifications and the Settings confirmation now describe what is actually known: the local Proxmark3 client process has started or stopped. Process creation alone does not prove that a physical device completed USB/serial initialisation. A future readiness handshake should establish that distinction before the UI reports a device as ready.

Settings no longer sends an unsolicited `hf mf info` command after connection. Connecting should establish the client session only; any card or tag operation must be initiated deliberately by the operator.

These changes are source-reviewed only. Windows compilation, process fault-injection tests, and hardware readiness validation remain outstanding.


## Testable client-output protocol

The output-line handling used by the process host has been extracted into `Services/ClientOutputProtocol.cs`. This isolates ANSI escape removal, blank-line handling, and exact completion-marker recognition from WPF and operating-system process APIs.

A small .NET 8 MSTest project under `tests/Proxmark3GUI.Tests` links that protocol source directly, allowing these deterministic cases to run on a non-Windows test runner as well as Windows:

- Null and whitespace-only lines are ignored.
- ANSI terminal sequences are removed while meaningful text is preserved.
- Trailing whitespace is trimmed without changing meaningful leading/interior spacing.
- The exact private completion marker is recognised, including harmless surrounding whitespace.
- Partial markers and ordinary output are not treated as command completion.

The Windows workflow now runs this test project after building the WPF application. The tests have been committed but have not yet been executed in this session. They test the extracted line protocol only; they do not replace the planned fake-process integration tests for cancellation, timeout, process exit, reconnect, or command lifecycle races.


## Command submission pipe failure

Command submission now checks caller cancellation before creating a command session. The stdin write and flush are guarded because the child process can exit after the initial `HasExited` check but before the command is written. If the pipe is already closed or unavailable, the active command session is completed with an I/O failure and its state is cleared rather than leaving the service permanently reporting that another command is running.

This is a source-level race hardening change. A process-backed integration test is still required to exercise the exact exit-during-write timing on Windows.


## Command-session callback isolation

A further concurrency review found that cancellation registrations and the shared idle timer could outlive the command that created them. If an old callback was already queued as one command completed, it could run after the next command started and accidentally complete that newer command.

The command host now binds cancellation, timeout, and idle-timer completion to the exact `TaskCompletionSource` instance for that command session. Completion checks that the expected session is still active before changing shared state. The idle timer is created for the active session and disposed when that session completes, so a queued callback from an earlier timer cannot finish a later command.

This closes the identified stale-callback race at source level. The explicit completion marker itself still carries no session identifier; a very late marker arriving after a previous command has timed out could theoretically be mistaken for the current command's marker. A future protocol revision should include a monotonically increasing command/session identifier in the marker and validate it in the host.

No build or runtime test was run in this session. Deterministic callback-race tests and fake-client process integration tests remain required.


## Thread-safe client process state

The public `IsConnected` property previously read the mutable `Process` reference without taking the service lock. During startup, the process object is assigned before `Start()` completes; a concurrent state query could therefore inspect a process that has not started yet. Concurrent disconnect/disposal could also race the read.

The property now reads the process reference under the same synchronization lock used by connect and disconnect. It treats an unstarted or disposed process handle as not connected rather than allowing `Process.HasExited` to throw through a UI binding or status query.

This only reports the local child-process state. It does not prove that the Proxmark3 hardware is present or ready. No build or runtime test was run in this session.


## Serialized service disposal

A lifecycle review found that `Dispose()` changed the disposed flag outside the service lock. A concurrent `ConnectAsync()` or command submission could pass its disposed check while shutdown was beginning.

Disposal now acquires the same lock used by connection startup and command submission before setting the disposed flag. It then releases the lock and calls `Disconnect()`, which performs its normal synchronized cleanup. This makes disposal idempotent and serializes the transition into the disposed state with operations that create or use the child process.

This is source-level hardening only. Concurrent connect/dispose and repeated-dispose tests should be added to the planned fake-process integration suite; no build or runtime test was run in this session.
