# SaaSApp – Modular Monolith SaaS

Production-ready ASP.NET Core Web API with Clean Architecture, CQRS (MediatR), domain events, multi-tenancy, and Azure-ready configuration.

## Structure

```
/src
  /Api                          # ASP.NET Core Web API
  /BuildingBlocks
    /SharedKernel                # Domain events, base entities, ITenantEntity
    /MultiTenancy                # ITenantProvider, HttpTenantProvider (JWT claim)
    /Logging                     # Serilog, correlation ID middleware
    /Security                    # JWT/Entra ID, policies (Admin, TenantUser), secure headers
  /Modules
    /Users                       # Users.Domain, Users.Application, Users.Infrastructure
    /Billing                     # Billing.Domain, Billing.Application, Billing.Infrastructure
    /Reporting                   # Reporting.Domain, Reporting.Application, Reporting.Infrastructure
```

## Features

- **Clean Architecture** – Domain, Application, Infrastructure per module
- **CQRS** – MediatR commands/queries and pipeline behaviors (e.g. transaction)
- **Domain events** – `UserCreatedEvent` raised from entity, dispatched after `SaveChanges`, handled (e.g. welcome email job)
- **Multi-tenancy** – `TenantId` column; `ITenantProvider` (JWT `tid` claim); global query filter in EF
- **Auth** – Microsoft Entra ID (Azure AD) + JWT Bearer; policy-based roles (Admin, TenantUser)
- **Logging** – Serilog, structured JSON, Application Insights, correlation ID
- **Background jobs** – Hangfire with SQL Server storage
- **Security** – HTTPS redirection, secure headers, Key Vault placeholders, service-to-service auth readiness

## Configuration

- **Connection string**: `ConnectionStrings:DefaultConnection`
- **Entra ID**: `AzureAd` (TenantId, ClientId, Audience). Use Key Vault references in production.
- **Application Insights**: `ApplicationInsights:ConnectionString` (Key Vault ready)
- **Key Vault**: See `src/Api/Configuration/KeyVaultExtensions.cs` and configure `KeyVault:Endpoint` to enable.

## Running

1. Set connection string and Azure AD settings (e.g. in `appsettings.Development.json`).
2. Create **catalog** (tenant registry) database: `.\scripts\CreateDatabase.ps1`  
   This applies the catalog migration to `DefaultConnection` (e.g. SaaSApp_Dev) and creates `catalog.Tenants`.
3. For each **tenant** you can either:
   - **Signup (auto-create DB)**: `POST /api/signup` with body `{ "tenantId": "<guid>", "name": "Tenant 01", "databaseName": "SaaSApp_Tenant_01" }`. This creates the database on the server, applies Users migrations, and registers the tenant in the catalog. Requires that `DefaultConnection` uses a login with CREATE DATABASE permission (e.g. Azure SQL server admin). No auth required by default (add middleware or API key in production if needed).
   - **Manual**: create the DB in Azure, run `.\scripts\UpdateTenantDatabase.ps1`, then `POST /api/admin/tenants` (Admin).
4. Run the API:
   ```bash
   cd src/Api
   dotnet run
   ```
5. Health: `GET https://localhost:5001/health`
6. Hangfire dashboard: `https://localhost:5001/hangfire` (add auth in production).

## Azure API Management

- Use correlation ID header `X-Correlation-ID` for tracing.
- Secure headers and CORS can be tuned at APIM or in-app as needed.
- Backend can validate JWT issued by Entra ID; APIM can also validate and pass claims.

## Users module

- **Create user**: `POST /api/users` with `{ "email": "...", "displayName": "..." }` (requires TenantUser or Admin).
- **Flow**: `CreateUserCommand` → handler creates `User` and adds to repository → `TransactionBehavior` calls `IUnitOfWork.SaveChangesAsync()` → `UsersUnitOfWork` runs `SaveChangesAsync` then `DomainEventDispatcher.DispatchAsync` → `UserCreatedEvent` published → `UserCreatedEventHandler` enqueues Hangfire welcome-email job.

## Billing & Reporting

Skeleton modules are in place; add entities, commands, and infrastructure following the same pattern as Users.
