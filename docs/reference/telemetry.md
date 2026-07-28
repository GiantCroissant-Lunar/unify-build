# Telemetry

UnifyBuild includes an optional, anonymous telemetry feature that collects build performance data locally. Telemetry is **disabled by default** and must be explicitly opted in.

## What data is collected

When telemetry is enabled, the following anonymous data is recorded after each build:

| Field | Description |
|---|---|
| `sessionId` | Random GUID generated per build — not tied to any user identity |
| `timestamp` | UTC timestamp of the build |
| `buildDurationMs` | Total build duration in milliseconds |
| `targetCount` | Number of build targets executed |
| `cacheHitRate` | Cache hit rate (0.0–1.0) |
| `operatingSystem` | OS description (e.g., "Windows 10", "Ubuntu 22.04") |
| `dotNetSdkVersion` | .NET runtime version used |
| `success` | Whether the build succeeded |

### What is NOT collected

- No usernames, machine names, or IP addresses
- No file paths or project names
- No source code or build output content
- No personally identifiable information (PII)

## How to opt in

Add the following to your `build.config.json`:

```json
{
  "observability": {
    "enableTelemetry": true
  }
}
```

## How to opt out

Remove the `enableTelemetry` property or set it to `false`:

```json
{
  "observability": {
    "enableTelemetry": false
  }
}
```

Telemetry is disabled by default — no action is needed if you never enabled it.

## Where data is stored

Telemetry records are saved as JSON files in the `build/_telemetry/` directory within your repository. Data is **never sent to any remote server**. You can inspect, delete, or `.gitignore` these files at any time.

Each build produces one file named `telemetry-{yyyyMMdd-HHmmss}-{sessionIdPrefix}.json`, where the
suffix is the first 8 characters of that build's `sessionId` — for example
`telemetry-20260115-103000-a3f9c1d2.json`.

Telemetry is separate from build metrics. Metrics are on by default, are written to
`build/_metrics`, and feed the [dashboard](tooling.md#metrics-dashboard); telemetry is opt-in and
written to `build/_telemetry`. See [`observability`](configuration-reference.md#observability-configuration)
for both settings.
