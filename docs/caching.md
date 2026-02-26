# Build Caching

UnifyBuild supports both local and distributed build caching to speed up incremental builds and share cached outputs across CI agents.

## Local Caching

### How It Works

When caching is enabled, UnifyBuild computes a deterministic cache key for each project by hashing:

- The `.csproj` file content
- All source files in the project directory (sorted for determinism)
- Build configuration properties (version, artifacts version)

Cached outputs are stored under `build/_cache/{cacheKey}/`. On subsequent builds, if the cache key matches, outputs are restored from cache instead of recompiling.

### Configuration

Enable local caching in `build.config.json`:

```json
{
  "performance": {
    "enableCache": true,
    "cacheDir": "build/_cache"
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enableCache` | boolean | `false` | Enable local build caching |
| `cacheDir` | string | `build/_cache` | Directory for cached outputs |
| `enableChangeDetection` | boolean | `true` | Skip builds when no source files changed |

## Distributed Caching

Distributed caching extends local caching by uploading and downloading cache entries via HTTP. This is useful for sharing build outputs across CI agents or team members.

### How It Works

1. On cache lookup, the local cache is checked first
2. If not found locally, a `GET` request is made to `{distributedCacheUrl}/{cacheKey}`
3. Downloaded entries are stored locally for future use
4. On cache store, outputs are saved locally and uploaded via `PUT` to `{distributedCacheUrl}/{cacheKey}/{relativePath}`
5. Network failures are retried with exponential backoff (3 attempts)
6. If the distributed cache is unreachable, the build continues using local cache only

### Configuration

```json
{
  "performance": {
    "enableCache": true,
    "distributedCacheUrl": "https://cache.example.com/unify-build"
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `distributedCacheUrl` | string | `null` | Base URL for the distributed cache server |

### URL Format

Cache entries are addressed as:

```
GET  {distributedCacheUrl}/{cacheKey}          — download cached output
PUT  {distributedCacheUrl}/{cacheKey}/{file}   — upload cached output file
```

### Authentication

If your cache server requires authentication, configure it via environment variables or HTTP headers supported by your server. UnifyBuild uses standard `HttpClient` for all requests.

## Best Practices

### Cache Invalidation

- Cache keys are content-based (SHA256 hash), so any source change automatically invalidates the cache
- To force a full rebuild, delete the `build/_cache` directory or pass `--no-cache`
- Changing the build version or configuration properties also changes the cache key

### Storage Management

- Periodically clean old cache entries to reclaim disk space
- For CI, consider using a shared network drive or object storage (S3, Azure Blob) as the distributed cache backend
- Set a retention policy on your cache server to automatically expire old entries

### CI Integration

For GitHub Actions, combine local caching with `actions/cache`:

```yaml
- uses: actions/cache@v4
  with:
    path: build/_cache
    key: unify-build-${{ hashFiles('**/*.csproj', 'build.config.json') }}
    restore-keys: unify-build-
```

For distributed caching across agents:

```json
{
  "performance": {
    "enableCache": true,
    "distributedCacheUrl": "https://your-cache-server.internal/unify-build"
  }
}
```

## Cache Statistics

When caching is enabled, UnifyBuild reports cache statistics at the end of each build:

```
Cache: 5 hits, 2 misses (71.4% hit rate), 1 downloads, 2 uploads
```

These statistics are also included in the build metrics report when observability is enabled.
