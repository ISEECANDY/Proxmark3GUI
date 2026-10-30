# Proxmark3 Studio

A native Windows desktop application for working with Proxmark3 devices and the RRG/Iceman client.

## Project direction

The goal is a polished, professional Windows GUI that makes supported Proxmark3 workflows discoverable and usable without requiring a terminal for normal operation. The application will be capability-aware: available features depend on the connected hardware, firmware, and client version.

The GUI is not intended to replace or casually reimplement the upstream Proxmark3 protocol stack. The integration strategy will be validated against the pinned upstream client before it is finalized.

## Current status

This repository is being organized in staged phases. The current branch, `phase-0-project-baseline`, records the initial project baseline and implementation plan. It does not yet contain a complete or buildable Windows application.

See:
- [Project baseline](docs/PROJECT_BASELINE.md)
- [Implementation roadmap](docs/IMPLEMENTATION_ROADMAP.md)

## Initial technology direction

- Windows 11 x64
- C# / .NET 10
- WinUI 3 and Windows App SDK
- MVVM and dependency injection
- SQLite for application metadata
- Serilog for structured diagnostics
- xUnit for automated tests

Exact package versions and SDK requirements will be pinned when the Windows build environment is established.

## Upstream

The application integrates with the [RfidResearchGroup Proxmark3 repository](https://github.com/RfidResearchGroup/proxmark3). Upstream licensing and source-distribution obligations must be respected for any bundled or modified components.
