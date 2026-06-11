# Repository Context for Copilot

This repository is a WPF desktop application for the Proxmark3 hardware tool. It is built with .NET 8.0 and follows an MVVM-inspired architecture with a separation between UI views, view models, services, and device-specific models.

## Architecture

- `Views/` contains WPF XAML views and code-behind for UI-specific behavior.
- `ViewModels/` contains view model classes and command wiring used by views.
- `Models/` contains plain data models such as `Pm3DeviceInfo`.
- `Services/` contains application services, including `Pm3ConnectionService` that manages the Proxmark3 client process and command I/O.
- `Converters/` contains value converters for data binding.
- `Resources/` and `Assets/` contain UI resources, styles, and static assets.
- `client/` contains the proxmark3 client source and supporting files, but the main app interacts with it through `Pm3ConnectionService`.

## Coding Standards and Preferred Patterns

- Use C# 12 / .NET 8 syntax and idioms when appropriate.
- Prefer `async`/`await` for I/O and device communication.
- Keep UI updates on the WPF dispatcher thread.
- Use `ObservableCollection<T>` and `ICommand` patterns for data-binding and command actions.
- Keep parsing and device logic in services or view model helpers, not directly in the view markup.
- Avoid relying on external dependencies unless necessary; keep the codebase lightweight.

## UI and Data Binding

- Views are usually bound to view models or updated from view model events.
- Live updates should route through `MainViewModel.CommandOutputReceived` when available.
- `DeviceInfoView`, `KeyCrackingView`, `AntennaDiagnosticsView`, and other views use event-driven updates from the main view model.

## Repository Usage

- The project is built using `dotnet build` from the repository root.
- Git is initialized and the repository is already set up for version control.

## Goals for Copilot Assistance

- Preserve MVVM separation and avoid moving business logic into code-behind unless the change is specifically UI-related.
- Use existing services such as `Pm3ConnectionService` and `MainViewModel` for new device command handling.
- Keep UI changes aligned with current resource styles and layout conventions.
- Add fixes or features incrementally, with minimal disruption to existing views and commands.
