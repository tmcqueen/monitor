# Design notes

Decisions from the initial brainstorm, with the reasoning, so future changes can argue with them.

## Scope

- Personal tool first: no backend, no accounts, no shared "I'm investigating this build" state.
- Azure DevOps Services (cloud) only, YAML pipelines only. Classic Release pipelines are a
  separate API and are out of scope.
- "Interesting" stays narrow: pipeline health first; later, PRs I authored or am asked to review,
  and work items from a saved query I choose. Not a second inbox.

## Platform and stack

- **.NET 10 + Avalonia.** Avalonia's `TrayIcon` speaks StatusNotifierItem over D-Bus on Linux, and
  KDE Plasma supports left-click activation, so a click can open a window.
- **The tray menu must be useful on its own.** Some SNI hosts (and GNOME, via the AppIndicator
  extension) only ever show the menu.
- **Notifications go straight to `org.freedesktop.Notifications`** via `Tmds.DBus.Protocol`,
  because Avalonia has no notification API. The package is pinned to the version Avalonia itself uses.
- **Raw REST over `HttpClient`** rather than `Microsoft.TeamFoundationServer.Client`, so we control
  headers, throttling and retries.

## Model

Each pipeline has a **health** (the outcome of its last *completed* run) and an **activity**
(idle, queued or running). This is CCTray's `lastBuildStatus` plus `activity` split, so a running
pipeline keeps showing its last known health, and CCTray maps onto the model without loss.

Notifications fire only on **Broke** (failed after not failing) and **Fixed** (succeeded after
failing). "Still failing" never notifies twice. The first observation of a pipeline is a baseline
and never notifies, so startup doesn't replay old failures.

## Polling and events

Polling is the source of truth. `IEventFeed` is an optional doorbell that wakes the engine early;
event payloads are never trusted, only used as a hint to re-read state over REST.

- Busy: poll every 20s. Idle: every 2m. Idle with a healthy feed: every 10m as reconciliation.
- Errors back off exponentially to 5m. `Retry-After` from a 429 is honored.
- Feed bursts are coalesced; polls are at least 3s apart.

### Phase 1.5: Azure DevOps service hooks → Azure Service Bus

No public endpoint or tunnel needed: ADO service hooks have built-in Azure Service Bus and
Azure Storage queue consumers. The app holds an outbound AMQP connection and receives in near real time.

- Basic tier namespace and queue. Separate SAS policies: Send-only for ADO, Listen-only for the app.
- Subscribe to `ms.vss-pipelines.run-state-changed-event` (plus stage/job events if wanted).
- **Enable "Send as nonserialized string" (`bypassSerializer`)**, or the body arrives wrapped in
  legacy .NET serialization.
- Set resource details to minimal: the message is a doorbell.
- On startup: drain the queue, poll once, then notify. Otherwise a night of stale events replays.
- **Blocker to check first:** creating subscriptions needs the *Edit subscriptions* permission,
  which only project administrators have by default.

### Not pursued (yet)

- **ADO's internal SignalR hub.** The web UI streams live build updates over legacy ASP.NET
  SignalR (not ASP.NET Core SignalR). It is undocumented, appears to be per-build (so it can't
  discover new runs), and may only accept browser cookies. Maybe useful later for a live log view;
  verify with DevTools before investing in it.
- **GitHub webhooks.** GitHub has no queue consumer. Polling with ETags (a `304` doesn't count
  against the rate limit) is likely enough. If not, a small Azure Function can verify the HMAC
  signature and forward into the same Service Bus queue.

## Roadmap

1. **Phase 0 (done):** tray, notifications, adaptive polling of one ADO project, env var config.
2. **Phase 1:** settings UI, PAT in the Secret Service, SQLite state and history, autostart entry.
3. **Phase 1.5:** Service Bus event feed.
4. **Phase 2:** Entra ID sign-in (MSAL), ADO pull requests and work-item queries.
5. **Phase 3:** GitHub (Actions runs, GraphQL PR dashboard).
6. **Phase 4:** CCTray, plus a CLI/socket for scripts and Plasma widgets.
