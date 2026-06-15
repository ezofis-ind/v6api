using Microsoft.Data.SqlClient;

namespace SaaSApp.Api.Services;

/// <summary>Applies DMS schema to tenant database. Same pattern as WorkflowSchemaService.</summary>
public sealed class DmsSchemaService : IDmsSchemaService
{
    public async Task ApplySchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var script = await LoadScriptAsync(cancellationToken);
        script = System.Text.RegularExpressions.Regex.Replace(script, @"USE\s+\[.*?\]\s*GO", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

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
                await using var command = new SqlCommand(batch, connection);
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2714 || ex.Number == 1913)
                    continue;
                throw new InvalidOperationException($"DMS schema batch failed: {ex.Message}", ex);
            }
        }
    }

    private static async Task<string> LoadScriptAsync(CancellationToken cancellationToken)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("CreateDmsSchema.sql"));
        if (resourceName != null)
        {
            await using var stream = asm.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "CreateDmsSchema.sql");
        if (File.Exists(scriptPath))
            return await File.ReadAllTextAsync(scriptPath, cancellationToken);

        scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "CreateDmsSchema.sql");
        if (File.Exists(scriptPath))
            return await File.ReadAllTextAsync(scriptPath, cancellationToken);

        throw new FileNotFoundException("CreateDmsSchema.sql not found as embedded resource or in scripts/.");
    }
}
