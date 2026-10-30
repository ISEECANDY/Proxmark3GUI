# Project Baseline and Repository Audit

**Repository:** `ISEECANDY/Proxmark3GUI`  
**Default branch observed:** `main`  
**Audit branch:** `phase-0-project-baseline`  
**Audit date:** 2026-10-31

## What has been verified

- The GitHub repository is accessible to the connected GitHub integration.
- The default branch is `main`.
- A second branch named `version-2` exists.
- The repository has an existing `.gitignore` with Visual Studio/.NET build-output exclusions and a local Proxmark client working-directory exclusion.
- The `.gitignore` file was read from `main`; it was not modified.
- The root `README.md`, `.github/copilot-instructions.md`, and `Proxmark3GUI.sln` paths were not present on `main` at the time of this audit.
- The repository has at least two commits on its history. The latest observed commit on 2026-06-11 is `338791c70eebb2f58497a6a29e36da034b7a81fe` ("Add Copilot repository context file"). The initial observed commit is `958ef2b624f2a836daf579a66ae22c3fb772670f`.

## Important audit limitation

The connected GitHub tools available in this session did not expose a repository tree listing, and GitHub code search reported no indexed results. Therefore this is a partial audit, not proof that the repository contains no application source. In particular, no existing source files have been overwritten or deleted.

Before creating or replacing application projects, inspect the full tree and both `main` and `version-2` locally or through a GitHub tree API-capable workflow.

## Upstream integration baseline

The intended upstream is [RfidResearchGroup/proxmark3](https://github.com/RfidResearchGroup/proxmark3).

A previously investigated upstream commit was:

`754bbae3d946429a6354cd34a97a2dd29ce6f157`

This SHA is a proposed reproducibility pin from prior research, not evidence that this application has cloned, built, or tested that revision. Revalidate the upstream commit and its build instructions when the integration phase begins.

## Non-negotiable engineering constraints

1. Normal workflows must not require a visible terminal window.
2. Do not replace the upstream serial/protocol implementation until an integration proof demonstrates that this is necessary.
3. Exactly one component owns a device transport/COM port at a time.
4. Serialize commands unless upstream behaviour proves safe concurrency.
5. Treat command documentation, parser registrations, runtime capabilities, and aliases as separate catalogue evidence.
6. Do not claim every documented command is a distinct executable operation.
7. Require explicit confirmation for destructive, state-changing, or sensitive operations.
8. Redact key material and sensitive card data from logs by default.
9. Keep large dump/trace artifacts on disk; store indexed metadata in SQLite.
10. Test unplug, timeout, cancellation, process crash, and partial-result paths.

## Current implementation status

No application code has been added by this audit commit. This commit establishes project documentation only. No Windows build or tests have been run.
