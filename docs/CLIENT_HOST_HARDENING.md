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
3. COM9 and MSYS path assumptions remain hard-coded.
4. The service remains a static singleton and is not yet isolated behind a testable interface.
5. No fake-client integration tests have been added yet.

## Verification status

- Source updated on the isolated branch.
- No Windows build was available in this GitHub-only session.
- No unit or integration tests were run.
- No physical device was connected.

Next engineering step: extract a testable client-host abstraction and add a fake executable/process harness before changing command completion semantics.