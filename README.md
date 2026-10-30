# Proxmark3 Studio

A professional native Windows desktop application for Proxmark3 devices, built around the existing Proxmark3GUI project and the RRG/Iceman client.

## Project direction

The goal is a polished Windows GUI that makes supported Proxmark3 workflows discoverable and usable without requiring a visible terminal for normal operation. Features must be capability-aware: availability depends on the connected hardware, firmware, and client version.

**This project is an evolution of the existing WPF application, not a replacement rewrite.** The current repository already contains 27 WPF views, a client process integration service, a command database, and a large set of device/client files. The plan is to preserve that work and improve it in controlled phases.

## Current technology

- Windows 11 x64
- Existing WPF desktop UI
- Current project target: .NET 8 (net8.0-windows)
- Planned framework upgrade: .NET 10, after a reproducible baseline build
- MVVM-inspired structure and dependency injection
- Existing command catalogue: 819 JSON entries (not 819 proven unique executable operations)
- Existing hidden child-process integration for the Proxmark3 client

See:
- [Repository source audit](docs/REPOSITORY_AUDIT.md)
- [Project baseline](docs/PROJECT_BASELINE.md)
- [Implementation roadmap](docs/IMPLEMENTATION_ROADMAP.md)

## Working branch

The current planning and audit work is on phase-0-project-baseline. No existing application source files have been replaced by this branch's documentation changes.

## Engineering priorities

1. Establish a clean, reproducible Windows build and test baseline.
2. Harden client process lifecycle, output handling, cancellation, and timeouts.
3. Separate process-running state from actual device readiness.
4. Verify command catalogue entries against the real upstream client.
5. Replace simulated or placeholder features incrementally.
6. Protect sensitive key/card data and require confirmation for state-changing operations.

## Upstream

The application integrates with [RfidResearchGroup/proxmark3](https://github.com/RfidResearchGroup/proxmark3). Upstream revision pinning, notices, and corresponding-source obligations must be respected for distributed upstream components.