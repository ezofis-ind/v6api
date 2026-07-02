using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SaaSApp.Api.Options;
using SaaSApp.Catalog;
using SaaSApp.Catalog.Entities;
using SaaSApp.Catalog.Persistence;
using SaaSApp.MultiTenancy;
using SaaSApp.Users.Domain.Entities;
using SaaSApp.Repository.Application.Contracts;
using SaaSApp.Users.Infrastructure.Persistence;

namespace SaaSApp.Api.Services;

/// <summary>
/// Creates a new tenant: provisions dedicated DB, applies migrations, registers in catalog.
/// Optionally creates admin user with password for Ezofis login.
/// </summary>
public interface ITenantSignupService
{
    Task<TenantSignupResult> SignupAsync(TenantSignupRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Signup request. Name or OrganizationName required. Email + Password create admin with Ezofis login.</summary>
public record TenantSignupRequest(
    Guid? TenantId,
    string Name,
    string? OrganizationName,
    string? Email,
    string? Password,
    string? LoginType,
    int? LicenseType,
    string? FirstName,
    string? LastName,
    string? DatabaseName,
    string? SignupSource,
    string? Platform,
    string? AppVersion);
/// <summary>Result of tenant signup. Save TenantId for X-Tenant-Id header and catalog linkage.</summary>
public record TenantSignupResult(Guid TenantId, string Name, string DatabaseName, string ConnectionString);

public sealed class TenantSignupService : ITenantSignupService
{
    private readonly ITenantDatabaseCreator _dbCreator;
    private readonly IDbContextFactory<CatalogDbContext> _catalogFactory;
    private readonly IConfiguration _configuration;
    private readonly IUserTenantRegistry _userTenantRegistry;
    private readonly IRepositorySchemaService _repositorySchema;
    private readonly IRepositoryStorageSeedService _repositoryStorageSeed;
    private readonly TenantPilotUserOptions _pilotUserOptions;

    public TenantSignupService(
        ITenantDatabaseCreator dbCreator,
        IDbContextFactory<CatalogDbContext> catalogFactory,
        IConfiguration configuration,
        IUserTenantRegistry userTenantRegistry,
        IRepositorySchemaService repositorySchema,
        IRepositoryStorageSeedService repositoryStorageSeed,
        IOptions<TenantPilotUserOptions> pilotUserOptions)
    {
        _dbCreator = dbCreator;
        _catalogFactory = catalogFactory;
        _configuration = configuration;
        _userTenantRegistry = userTenantRegistry;
        _repositorySchema = repositorySchema;
        _repositoryStorageSeed = repositoryStorageSeed;
        _pilotUserOptions = pilotUserOptions.Value;
    }

    public async Task<TenantSignupResult> SignupAsync(TenantSignupRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = request.TenantId ?? Guid.NewGuid();
        var licenseType = request.LicenseType ?? 3; // Old behavior default: DMS + Workflow
        if (licenseType is < 1 or > 3)
            throw new ArgumentException("LicenseType must be 1 (DMS), 2 (Workflow), or 3 (DMS+Workflow).", nameof(request));

        var displayName = !string.IsNullOrWhiteSpace(request.OrganizationName)
            ? request.OrganizationName!.Trim()
            : request.Name.Trim();

        var prefix = _configuration["TenantDatabase:NamePrefix"]?.Trim() ?? "ezofis_Tenant";
        prefix = new string(prefix.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrEmpty(prefix))
            prefix = "ezofis_Tenant";

        string dbName;
        string tenantConnectionString;
        await using (var catalog = await _catalogFactory.CreateDbContextAsync(cancellationToken))
        {
            var exists = await catalog.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
            if (exists)
                throw new InvalidOperationException("Tenant already registered with this Id.");
            var nameExists = await catalog.Tenants.AnyAsync(t => t.Name == displayName, cancellationToken);
            if (nameExists)
                throw new InvalidOperationException("Tenant name already exists. Please use a different organization/name.");

            dbName = request.DatabaseName?.Trim() ?? "";
            if (string.IsNullOrEmpty(dbName))
            {
                var nextNumber = await catalog.Tenants.CountAsync(cancellationToken) + 1;
                dbName = $"{prefix}_{nextNumber}";
                // If catalog was reset but tenant DBs still exist, find next available name
                while (await _dbCreator.DatabaseExistsAsync(dbName, cancellationToken))
                {
                    nextNumber++;
                    dbName = $"{prefix}_{nextNumber}";
                }
            }

            dbName = new string(dbName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            if (string.IsNullOrEmpty(dbName))
                throw new ArgumentException("Invalid database name.", nameof(request));

            await _dbCreator.CreateDatabaseAsync(dbName, cancellationToken);

            tenantConnectionString = BuildTenantConnectionString(dbName);

            await ApplyUsersMigrationsAsync(tenantConnectionString, tenantId, cancellationToken);
        await ApplyWorkflowSchemaAsync(tenantConnectionString, cancellationToken);
        await _repositorySchema.ApplyBaseSchemaAsync(tenantConnectionString, cancellationToken);
        await _repositoryStorageSeed.EnsureDefaultProvidersAsync(tenantConnectionString, tenantId, null, cancellationToken);

        // Old licenseType intent:
        // 1 = DMS, 2 = Workflow, 3 = DMS + Workflow.
        if (licenseType is 2 or 3)
            {
                await ApplyWorkflowSchemaAsync(tenantConnectionString, cancellationToken);
            }

            if (licenseType is 1 or 3)
            {
                await ApplyDmsSchemaAsync(tenantConnectionString, cancellationToken);
            }

            // Add tenant to catalog first (UserTenants FK requires Tenant to exist)
            catalog.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = displayName,
                ConnectionString = tenantConnectionString,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                Email = request.Email?.Trim(),
                SignupSource = request.SignupSource?.Trim(),
                Platform = request.Platform?.Trim(),
                AppVersion = request.AppVersion?.Trim(),
                LoginType = request.LoginType?.Trim() ?? "EZOFIS"
            });
            try
            {
                await catalog.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Tenants_Name", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new InvalidOperationException("Tenant name already exists. Please use a different organization/name.");
            }

            var adminEmail = request.Email?.Trim();
            if (!string.IsNullOrEmpty(adminEmail))
            {
                await CreateTenantUserAsync(
                    tenantConnectionString,
                    tenantId,
                    adminEmail,
                    displayName,
                    request.Password,
                    User.RoleAdmin,
                    request.LoginType,
                    request.FirstName,
                    request.LastName,
                    cancellationToken);
                await _userTenantRegistry.AddOrUpdateAsync(adminEmail, tenantId, User.RoleAdmin, cancellationToken);
            }

            await CreatePilotUserIfConfiguredAsync(
                tenantConnectionString,
                tenantId,
                adminEmail,
                cancellationToken);
        }

        return new TenantSignupResult(tenantId, displayName, dbName, tenantConnectionString);
    }

    private string BuildTenantConnectionString(string databaseName)
    {
        var catalogConnection = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not found.");
        var builder = new SqlConnectionStringBuilder(catalogConnection) { InitialCatalog = databaseName };
        return builder.ConnectionString;
    }

    private static async Task ApplyUsersMigrationsAsync(string tenantConnectionString, Guid tenantId, CancellationToken cancellationToken)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseSqlServer(tenantConnectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.SchemaName);
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
        var tenantProvider = new StaticTenantProvider(tenantId);
        await using var context = new UsersDbContext(optionsBuilder.Options, tenantProvider);
        await context.Database.MigrateAsync(cancellationToken);
    }

    private async Task CreatePilotUserIfConfiguredAsync(
        string tenantConnectionString,
        Guid tenantId,
        string? adminEmail,
        CancellationToken cancellationToken)
    {
        if (!_pilotUserOptions.Enabled)
            return;

        var pilotEmail = _pilotUserOptions.Email?.Trim();
        if (string.IsNullOrWhiteSpace(pilotEmail))
            return;

        if (string.IsNullOrWhiteSpace(_pilotUserOptions.Password))
            return;

        if (string.Equals(pilotEmail, adminEmail, StringComparison.OrdinalIgnoreCase))
            return;

        await CreateTenantUserAsync(
            tenantConnectionString,
            tenantId,
            pilotEmail,
            string.IsNullOrWhiteSpace(_pilotUserOptions.DisplayName)
                ? "AP Agent Pilot"
                : _pilotUserOptions.DisplayName.Trim(),
            _pilotUserOptions.Password,
            string.IsNullOrWhiteSpace(_pilotUserOptions.Role)
                ? User.RoleTenantUser
                : _pilotUserOptions.Role.Trim(),
            "EZOFIS",
            firstName: "AP Agent",
            lastName: "Pilot",
            cancellationToken);

        await _userTenantRegistry.AddOrUpdateAsync(
            pilotEmail,
            tenantId,
            string.IsNullOrWhiteSpace(_pilotUserOptions.Role)
                ? User.RoleTenantUser
                : _pilotUserOptions.Role.Trim(),
            cancellationToken);
    }

    private static async Task CreateTenantUserAsync(
        string tenantConnectionString,
        Guid tenantId,
        string email,
        string displayName,
        string? password,
        string role,
        string? loginType,
        string? firstName,
        string? lastName,
        CancellationToken cancellationToken)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseSqlServer(tenantConnectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.SchemaName);
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
        var tenantProvider = new StaticTenantProvider(tenantId);
        await using var context = new UsersDbContext(optionsBuilder.Options, tenantProvider);

        var normalizedEmail = email.Trim();
        var exists = await context.Users.AnyAsync(
            u => u.Email == normalizedEmail && !u.IsDeleted,
            cancellationToken);
        if (exists)
            return;

        var user = User.Create(
            tenantId,
            normalizedEmail,
            displayName,
            role,
            firstName?.Trim(),
            lastName?.Trim(),
            User.AuthStrategyEzofis);
        user.SetLoginType(loginType ?? "EZOFIS");
        if (!string.IsNullOrWhiteSpace(password))
            user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(password.Trim()));

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task ApplyWorkflowSchemaAsync(string tenantConnectionString, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "CreateWorkflowSchemaComplete.sql");
        if (!File.Exists(scriptPath))
        {
            // Fallback: try relative to project root
            scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "CreateWorkflowSchemaComplete.sql");
        }

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Workflow schema script not found at: {scriptPath}");
        }

        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        
        // Remove USE statements - we're already connected to the right database
        script = System.Text.RegularExpressions.Regex.Replace(script, @"USE\s+\[.*?\]\s*GO", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Split on GO (line containing only GO) - multiline regex for robustness
        // Do NOT filter by StartsWith("--") - batches often start with comments but contain CREATE statements
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"(?m)^\s*GO\s*$")
            .Select(b => b.Trim())
            .Where(b => b.Length > 10)
            .ToList();
        
        await using var connection = new SqlConnection(tenantConnectionString);
        await connection.OpenAsync(cancellationToken);
        
        var batchNumber = 0;
        foreach (var batch in batches)
        {
            batchNumber++;
            var trimmedBatch = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmedBatch))
                continue;
                
            try
            {
                await using var command = new SqlCommand(trimmedBatch, connection);
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex)
            {
                // Log the error but continue with other batches
                Console.WriteLine($"Warning: Workflow schema batch {batchNumber} failed: {ex.Message}");
                // Don't throw - some batches might fail if objects already exist
            }
        }
    }

    private static async Task ApplyDmsSchemaAsync(string tenantConnectionString, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "CreateDmsSchema.sql");
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "CreateDmsSchema.sql");
        }

        if (!File.Exists(scriptPath))
        {
            Console.WriteLine("Warning: DMS schema script not found. Skipping DMS setup.");
            return;
        }

        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        script = System.Text.RegularExpressions.Regex.Replace(script, @"USE\s+\[.*?\]\s*GO", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"(?m)^\s*GO\s*$")
            .Select(b => b.Trim())
            .Where(b => b.Length > 10)
            .ToList();

        await using var connection = new SqlConnection(tenantConnectionString);
        await connection.OpenAsync(cancellationToken);

        var batchNumber = 0;
        foreach (var batch in batches)
        {
            batchNumber++;
            var trimmedBatch = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmedBatch))
                continue;

            try
            {
                await using var command = new SqlCommand(trimmedBatch, connection);
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Warning: DMS schema batch {batchNumber} failed: {ex.Message}");
            }
        }
    }
}
