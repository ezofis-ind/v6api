using Microsoft.Data.SqlClient;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure.Services;

public sealed class RepositorySchemaService : IRepositorySchemaService
{
    public async Task ApplyBaseSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var script = await LoadScriptAsync(cancellationToken);
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"(?m)^\s*GO\s*$")
            .Select(b => b.Trim())
            .Where(b => b.Length > 10)
            .ToList();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var batch in batches)
        {
            try
            {
                await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex) when (ex.Number is 2714 or 1913 or 2705)
            {
                // idempotent
            }
        }
    }

    private static async Task<string> LoadScriptAsync(CancellationToken cancellationToken)
    {
        var asm = typeof(RepositorySchemaService).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("CreateRepositorySchema.sql", StringComparison.OrdinalIgnoreCase));
        if (resourceName != null)
        {
            await using var stream = asm.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", "CreateRepositorySchema.sql"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "CreateRepositorySchema.sql"))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return await File.ReadAllTextAsync(path, cancellationToken);
        }

        throw new FileNotFoundException("CreateRepositorySchema.sql not found. Rebuild SaaSApp.Api.");
    }
}
