# Redis Usage in SaaS API

This document explains where Redis is used in the API, how to configure it, and how the app behaves when Redis is not available.

## Where Redis is used

- `IDistributedCache` is used in OTP flow (`checkAuthenticate`) to store short-lived signup OTP values.
- Redis is registered in `src/Api/Program.cs` using `AddStackExchangeRedisCache(...)` when a Redis connection string is configured.

## Optional behavior (important)

Redis is **optional** in this project.

- If `ConnectionStrings:Redis` is present, the app uses Redis-backed distributed cache.
- If `ConnectionStrings:Redis` is missing/empty, the app falls back to `AddDistributedMemoryCache()`.
- In OTP flow, cache write is wrapped with safe fallback logic. If Redis is temporarily unavailable, OTP still works using `catalog.OtpVerifications` table storage.

## Configuration keys

Add these keys in `appsettings.json` (or environment variables):

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "Redis": {
    "InstanceName": "SaaSApp:"
  }
}
```

### Environment variable format

- `ConnectionStrings__Redis`
- `Redis__InstanceName`

## Local development setup

### Option A: No Redis

- Remove or leave `ConnectionStrings:Redis` empty.
- App uses in-memory distributed cache automatically.

### Option B: Use local Redis

- Run Redis locally (Docker example):
  - `docker run -d --name saas-redis -p 6379:6379 redis:7`
- Set:
  - `ConnectionStrings:Redis = localhost:6379`

## IIS / on-prem hosting

- Redis is not mandatory.
- If server has no Redis, leave `ConnectionStrings:Redis` empty and app will still run.
- If using Redis server, set `ConnectionStrings__Redis` in IIS application settings or in deployed `appsettings.json`.

## Azure hosting

Use Azure Cache for Redis connection string format:

```text
<name>.redis.cache.windows.net:6380,password=<key>,ssl=True,abortConnect=False
```

Recommended settings:

- Keep `ssl=True` for Azure Redis.
- Use app settings / Key Vault instead of hardcoding secrets in `appsettings.json`.

## Health and troubleshooting

### Symptoms when Redis is misconfigured

- Startup can still succeed (because Redis is optional).
- OTP requests continue to work due to DB persistence fallback.

### Checks

- Verify final resolved value of `ConnectionStrings:Redis`.
- Confirm Redis host/port/firewall access.
- For Azure, confirm TLS (`6380`, `ssl=True`) and network rules.

## Current project defaults

- Redis package is included and supported.
- Fallback to memory cache is enabled.
- OTP cache write failures are tolerated to avoid signup failure.

