# Monitor

A cross-platform system tray app that watches CI/CD pipelines, pull requests and work items,
in the spirit of CatLight. Linux (KDE Plasma) first, built with .NET 10 and Avalonia.

Planned providers, in order: **Azure DevOps** (YAML pipelines, cloud), **GitHub**, then anything
that serves a **CCTray** (`cc.xml`) feed.

## Status: phase 0 spike

What works today:

- A tray icon colored by overall health (green, amber, red, gray) with a blue ring while anything is running
- A tray menu listing each pipeline (click one to open its latest run), plus Refresh and Quit
- A status window (left-click the tray icon, or "Show status…")
- Desktop notifications over D-Bus when a pipeline **breaks** or is **fixed**. Clicking one opens the run.
- Adaptive polling of the Azure DevOps Builds API: 20s while busy, 2m while idle, exponential backoff on errors, and `Retry-After` honored when throttled

## Running it

Requires the .NET 10 SDK.

```bash
export MONITOR_ADO_ORG=my-org                 # dev.azure.com/{org}
export MONITOR_ADO_PROJECT=my-project
export MONITOR_ADO_PAT=xxxxxxxx               # PAT with the "Build (Read)" scope
export MONITOR_ADO_DEFINITIONS=12,15          # pipeline IDs (the definitionId in the pipeline URL)
export MONITOR_ADO_BRANCH=refs/heads/main     # optional; omit to watch every branch

dotnet run --project src/Monitor.Desktop
```

The PAT in an environment variable is a phase 0 shortcut. It moves to the Secret Service
(KWallet / GNOME Keyring) in phase 1.

On stock GNOME you need the AppIndicator extension to see any tray icon. KDE Plasma works out of the box.

## Layout

```
src/Monitor.Core           Provider-agnostic engine: model, polling, transition rules, event feed seam
src/Monitor.Desktop        Avalonia tray app, status window, freedesktop notifications
tests/Monitor.Core.Tests   xUnit tests for the engine, rules and Azure DevOps source
docs/design-notes.md       Decisions made so far, and why
```

```bash
dotnet test
```
