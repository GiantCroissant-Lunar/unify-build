# UnifyBuild Analytics Dashboard

A single-page build analytics dashboard that visualizes JSON metrics exported by `BuildMetrics`.

## Quick Start

1. Open `index.html` in any modern browser — no build tools or server required.
2. Enter the team password (default: `unifybuild`).
3. Load one or more JSON metrics files using the **Load Metrics** button or drag-and-drop.

## Features

| Feature | Description |
|---------|-------------|
| Duration trend | Line chart showing build duration over time |
| Success/failure rate | Doughnut chart of pass vs. fail builds |
| Cache hit rate | Line chart tracking cache efficiency over time |
| Slowest targets | Horizontal bar chart of the top 10 slowest Build_Targets (avg) |
| Project compilation | Bar chart of average compilation time per project |
| Date range filter | Filter builds by start/end date |
| Target filter | Filter to builds containing a specific Build_Target |
| Project filter | Filter to builds containing a specific project |
| CSV export | Download filtered data as CSV |
| Auth gate | Simple password-based access control |

## Metrics JSON Format

The dashboard reads JSON files produced by `BuildMetrics.ExportJson()`. Expected shape:

```json
{
  "timestamp": "2026-01-15T10:30:00+00:00",
  "totalDuration": "0.00:02:15.1234567",
  "targetDurations": {
    "Compile": "0.00:01:05.0000000",
    "Pack": "0.00:00:45.0000000"
  },
  "projectDurations": {
    "MyApp.csproj": "0.00:00:30.0000000"
  },
  "cacheHits": 5,
  "cacheMisses": 2,
  "cacheHitRate": 0.714,
  "success": true
}
```

You can also load an array of reports in a single file (`[{...}, {...}]`).

Both camelCase and PascalCase property names are accepted.

## Customizing Authentication

The default password is `unifybuild`. To change it, edit the `authenticate()` function in `index.html`. For production use, consider serving the dashboard behind a reverse proxy with proper authentication (e.g., OAuth, SSO).

## Technology

- **Chart.js 4.x** (loaded from CDN) for all charts
- Vanilla HTML/CSS/JS — no build step, no dependencies to install
- Responsive layout using CSS grid and flexbox
