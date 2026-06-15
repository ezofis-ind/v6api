using Microsoft.Data.SqlClient;

namespace SaaSApp.Api.Services;

/// <summary>Applies workflow schema (workflow.Workflows, etc.) to a tenant database.</summary>
public interface IWorkflowSchemaService
{
    /// <summary>Apply workflow schema to the given connection string. Idempotent.</summary>
    Task ApplySchemaAsync(string connectionString, CancellationToken cancellationToken = default);
}

public sealed class WorkflowSchemaService : IWorkflowSchemaService
{
    public async Task ApplySchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var script = await LoadScriptAsync(cancellationToken);
        script = System.Text.RegularExpressions.Regex.Replace(script, @"USE\s+\[.*?\]\s*GO", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Split on GO (line containing only GO) - multiline regex
        // Do NOT filter by StartsWith("--") - batches often start with comments but contain CREATE statements
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"(?m)^\s*GO\s*$")
            .Select(b => b.Trim())
            .Where(b => b.Length > 10) // Skip empty or trivial batches
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
                // 2714/1913 = object already exists (idempotent)
                if (ex.Number == 2714 || ex.Number == 1913)
                    continue;
                throw new InvalidOperationException($"Workflow schema batch failed: {ex.Message}", ex);
            }
        }
    }

    private static async Task<string> LoadScriptAsync(CancellationToken cancellationToken)
    {
        // 1. Embedded resource (most reliable)
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("CreateWorkflowSchemaComplete.sql"));
        if (resourceName != null)
        {
            await using var stream = asm.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        // 2. Output directory
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "CreateWorkflowSchemaComplete.sql");
        if (File.Exists(scriptPath))
            return await File.ReadAllTextAsync(scriptPath, cancellationToken);

        // 3. Current directory (e.g. solution root)
        scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "CreateWorkflowSchemaComplete.sql");
        if (File.Exists(scriptPath))
            return await File.ReadAllTextAsync(scriptPath, cancellationToken);

        throw new FileNotFoundException("CreateWorkflowSchemaComplete.sql not found as embedded resource or in scripts/.");
    }
}
