using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SaaSApp.MultiTenancy;
using SaaSApp.Repository.Application.Contracts;

namespace SaaSApp.Repository.Infrastructure.Services;

/// <summary>
/// Related documents across all tenant repositories.
/// Match uses the source repository's folder-structure fields
/// (<c>IncludeInFolderStructure</c>), i.e. the same virtual folder path.
/// FE only passes repositoryId + itemId.
/// </summary>
public sealed class RepositoryRelatedDocumentsService : IRepositoryRelatedDocumentsService
{
    private static readonly ConcurrentDictionary<string, byte> SchemaEnsured = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] SupplierAliases = ["Supplier", "VendorName", "Vendor", "Vendor Name"];
    private static readonly string[] PoAliases = ["PONumber", "PoNumber", "PO Number"];
    private static readonly string[] InvoiceAliases = ["InvoiceNo", "InvoiceNumber", "Invoice No", "Invoice Number"];
    private static readonly string[] DocumentTypeAliases = ["DocumentType", "Document Type"];

    private const int PerRepoLimit = 50;
    private const int MaxParallelRepos = 8;

    private readonly ITenantConnectionProvider _connectionProvider;
    private readonly IStaticRepositoryProvisioner _provisioner;
    private readonly IRepositoryItemQueryService _items;
    private readonly ILogger<RepositoryRelatedDocumentsService> _logger;

    public RepositoryRelatedDocumentsService(
        ITenantConnectionProvider connectionProvider,
        IStaticRepositoryProvisioner provisioner,
        IRepositoryItemQueryService items,
        ILogger<RepositoryRelatedDocumentsService> logger)
    {
        _connectionProvider = connectionProvider;
        _provisioner = provisioner;
        _items = items;
        _logger = logger;
    }

    public Task<RepositoryRelatedDocumentsResultDto?> GetRelatedAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        GetRelatedCoreAsync(
            repositoryId,
            tenantId,
            itemId,
            requireAllFields: false,
            useAllRepositoryFields: false,
            page,
            pageSize,
            fields: null,
            value: null,
            cancellationToken);

    public Task<RepositoryRelatedDocumentsResultDto?> GetRelatedExactAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        int page = 1,
        int pageSize = 50,
        IReadOnlyList<string>? fields = null,
        string? value = null,
        CancellationToken cancellationToken = default) =>
        GetRelatedCoreAsync(
            repositoryId,
            tenantId,
            itemId,
            requireAllFields: true,
            useAllRepositoryFields: true,
            page,
            pageSize,
            fields,
            value,
            cancellationToken);

    public async Task<RepositorySavedRelatedDocumentsResultDto?> GetSavedRelatedAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var sourceRepo = await _provisioner.GetRepositoryAsync(repositoryId, tenantId, cancellationToken);
        if (sourceRepo == null)
            return null;

        var source = await _items.GetItemAsync(repositoryId, tenantId, itemId, cancellationToken);
        if (source == null)
            return null;

        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        await EnsureRelatedSchemaAsync(connectionString, cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Family of saved links: this item as source, OR any source that saved this item as related.
        // Opening any related file then returns the same set (source + siblings), excluding the open item.
        const string sourcesSql = """
            SELECT DISTINCT RepositoryId, ItemId, MatchField, MatchValue
            FROM repository.ItemRelatedDocuments
            WHERE TenantId = @TenantId
              AND IsDeleted = 0
              AND (
                    (RepositoryId = @RepositoryId AND ItemId = @ItemId)
                 OR (RelatedRepositoryId = @RepositoryId AND RelatedItemId = @ItemId)
              );
            """;

        var sources = new List<(Guid SrcRepoId, Guid SrcItemId, string? MatchField, string? MatchValue)>();
        await using (var srcCmd = new SqlCommand(sourcesSql, connection))
        {
            srcCmd.Parameters.AddWithValue("@TenantId", tenantId);
            srcCmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
            srcCmd.Parameters.AddWithValue("@ItemId", itemId);
            await using var srcReader = await srcCmd.ExecuteReaderAsync(cancellationToken);
            while (await srcReader.ReadAsync(cancellationToken))
            {
                sources.Add((
                    srcReader.GetGuid(0),
                    srcReader.GetGuid(1),
                    srcReader.IsDBNull(2) ? null : srcReader.GetString(2),
                    srcReader.IsDBNull(3) ? null : srcReader.GetString(3)));
            }
        }

        string? matchField = null;
        string? matchValue = null;
        var links = new List<(
            Guid LinkId,
            Guid RelRepoId,
            Guid RelItemId,
            string? MatchField,
            string? MatchValue,
            int? MatchScore,
            DateTime CreatedAtUtc,
            string? SnapshotRepoName,
            string? SnapshotFileName,
            string? SnapshotFileType,
            string? SnapshotFilePath)>();
        var seen = new HashSet<(Guid RepoId, Guid ItemId)>();

        foreach (var src in sources)
        {
            matchField ??= src.MatchField;
            matchValue ??= src.MatchValue;

            // Include the save-source document itself when viewing from a related file.
            if (!(src.SrcRepoId == repositoryId && src.SrcItemId == itemId)
                && seen.Add((src.SrcRepoId, src.SrcItemId)))
            {
                links.Add((
                    Guid.Empty,
                    src.SrcRepoId,
                    src.SrcItemId,
                    src.MatchField,
                    src.MatchValue,
                    null,
                    DateTime.UtcNow,
                    null,
                    null,
                    null,
                    null));
            }

            const string relatedSql = """
                SELECT Id, RelatedRepositoryId, RelatedItemId, MatchField, MatchValue, MatchScore, CreatedAtUtc,
                       RelatedRepositoryName, FileName, FileType, FilePath
                FROM repository.ItemRelatedDocuments
                WHERE TenantId = @TenantId
                  AND RepositoryId = @SourceRepositoryId
                  AND ItemId = @SourceItemId
                  AND IsDeleted = 0
                ORDER BY CreatedAtUtc DESC, Id DESC;
                """;

            await using var relCmd = new SqlCommand(relatedSql, connection);
            relCmd.Parameters.AddWithValue("@TenantId", tenantId);
            relCmd.Parameters.AddWithValue("@SourceRepositoryId", src.SrcRepoId);
            relCmd.Parameters.AddWithValue("@SourceItemId", src.SrcItemId);
            await using var relReader = await relCmd.ExecuteReaderAsync(cancellationToken);
            while (await relReader.ReadAsync(cancellationToken))
            {
                var relRepoId = relReader.GetGuid(1);
                var relItemId = relReader.GetGuid(2);
                if (relRepoId == repositoryId && relItemId == itemId)
                    continue;
                if (!seen.Add((relRepoId, relItemId)))
                    continue;

                links.Add((
                    relReader.GetGuid(0),
                    relRepoId,
                    relItemId,
                    relReader.IsDBNull(3) ? null : relReader.GetString(3),
                    relReader.IsDBNull(4) ? null : relReader.GetString(4),
                    relReader.IsDBNull(5) ? null : Convert.ToInt32(relReader.GetValue(5)),
                    relReader.GetDateTime(6),
                    relReader.FieldCount > 7 && !relReader.IsDBNull(7) ? relReader.GetString(7) : null,
                    relReader.FieldCount > 8 && !relReader.IsDBNull(8) ? relReader.GetString(8) : null,
                    relReader.FieldCount > 9 && !relReader.IsDBNull(9) ? relReader.GetString(9) : null,
                    relReader.FieldCount > 10 && !relReader.IsDBNull(10) ? relReader.GetString(10) : null));
            }
        }

        var data = new List<RepositorySavedRelatedDocumentDto>();
        foreach (var link in links)
        {
            var detail = await TryLoadRelatedItemAsync(
                connectionString,
                tenantId,
                link.RelRepoId,
                link.RelItemId,
                cancellationToken);

            data.Add(new RepositorySavedRelatedDocumentDto(
                link.LinkId == Guid.Empty ? Guid.NewGuid() : link.LinkId,
                link.RelRepoId,
                FirstNonEmpty(link.SnapshotRepoName, detail?.RepositoryName),
                link.RelItemId,
                FirstNonEmpty(link.SnapshotFileName, detail?.FileName),
                FirstNonEmpty(link.SnapshotFileType, detail?.FileType),
                FirstNonEmpty(link.SnapshotFilePath, detail?.FilePath),
                detail?.FileSize,
                detail?.DocumentType,
                detail?.Supplier,
                detail?.PoNumber,
                detail?.InvoiceNumber,
                link.MatchScore,
                link.MatchField,
                link.MatchValue,
                link.CreatedAtUtc));
        }

        return new RepositorySavedRelatedDocumentsResultDto(
            repositoryId,
            itemId,
            matchField,
            matchValue,
            data.Count,
            data);
    }

    public async Task<RepositorySavedRelatedDocumentsResultDto?> SaveRelatedAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        SaveRepositoryRelatedDocumentsRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceRepo = await _provisioner.GetRepositoryAsync(repositoryId, tenantId, cancellationToken);
        if (sourceRepo == null)
            return null;

        var source = await _items.GetItemAsync(repositoryId, tenantId, itemId, cancellationToken);
        if (source == null)
            return null;

        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        await EnsureRelatedSchemaAsync(connectionString, cancellationToken);

        var items = (request.Items ?? Array.Empty<SaveRepositoryRelatedDocumentRef>())
            .Where(i => i.RepositoryId != Guid.Empty && i.ItemId != Guid.Empty)
            .Where(i => !(i.RepositoryId == repositoryId && i.ItemId == itemId))
            .GroupBy(i => (i.RepositoryId, i.ItemId))
            .Select(g => g.First())
            .ToList();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        // Replace semantics: soft-delete previous set for this source item.
        await using (var clear = new SqlCommand(
                         """
                         UPDATE repository.ItemRelatedDocuments
                         SET IsDeleted = 1
                         WHERE TenantId = @TenantId
                           AND RepositoryId = @RepositoryId
                           AND ItemId = @ItemId
                           AND IsDeleted = 0;
                         """,
                         connection,
                         tx))
        {
            clear.Parameters.AddWithValue("@TenantId", tenantId);
            clear.Parameters.AddWithValue("@RepositoryId", repositoryId);
            clear.Parameters.AddWithValue("@ItemId", itemId);
            await clear.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var item in items)
        {
            var snapshotRepoName = TrimOrNull(item.RepositoryName);
            var snapshotFileName = TrimOrNull(item.FileName);
            var snapshotFileType = TrimOrNull(item.FileType);
            var snapshotFilePath = TrimOrNull(item.FilePath);

            // Fill missing snapshot fields from the live related item (needed for Python / reopen).
            if (snapshotRepoName == null || snapshotFileName == null || snapshotFileType == null || snapshotFilePath == null)
            {
                var detail = await TryLoadRelatedItemAsync(
                    connectionString,
                    tenantId,
                    item.RepositoryId,
                    item.ItemId,
                    cancellationToken);
                if (detail != null)
                {
                    snapshotRepoName ??= detail.Value.RepositoryName;
                    snapshotFileName ??= detail.Value.FileName;
                    snapshotFileType ??= detail.Value.FileType;
                    snapshotFilePath ??= detail.Value.FilePath;
                }
            }

            await using var insert = new SqlCommand(
                """
                INSERT INTO repository.ItemRelatedDocuments
                    (Id, TenantId, RepositoryId, ItemId, RelatedRepositoryId, RelatedItemId,
                     MatchField, MatchValue, MatchScore,
                     RelatedRepositoryName, FileName, FileType, FilePath,
                     CreatedBy, CreatedAtUtc, IsDeleted)
                VALUES
                    (@Id, @TenantId, @RepositoryId, @ItemId, @RelatedRepositoryId, @RelatedItemId,
                     @MatchField, @MatchValue, @MatchScore,
                     @RelatedRepositoryName, @FileName, @FileType, @FilePath,
                     @CreatedBy, SYSUTCDATETIME(), 0);
                """,
                connection,
                tx);
            insert.Parameters.AddWithValue("@Id", Guid.NewGuid());
            insert.Parameters.AddWithValue("@TenantId", tenantId);
            insert.Parameters.AddWithValue("@RepositoryId", repositoryId);
            insert.Parameters.AddWithValue("@ItemId", itemId);
            insert.Parameters.AddWithValue("@RelatedRepositoryId", item.RepositoryId);
            insert.Parameters.AddWithValue("@RelatedItemId", item.ItemId);
            insert.Parameters.AddWithValue("@MatchField", (object?)TrimOrNull(request.MatchField) ?? DBNull.Value);
            insert.Parameters.AddWithValue("@MatchValue", (object?)TrimOrNull(request.MatchValue) ?? DBNull.Value);
            insert.Parameters.AddWithValue("@MatchScore", (object?)item.MatchScore ?? DBNull.Value);
            insert.Parameters.AddWithValue("@RelatedRepositoryName", (object?)snapshotRepoName ?? DBNull.Value);
            insert.Parameters.AddWithValue("@FileName", (object?)snapshotFileName ?? DBNull.Value);
            insert.Parameters.AddWithValue("@FileType", (object?)snapshotFileType ?? DBNull.Value);
            insert.Parameters.AddWithValue("@FilePath", (object?)snapshotFilePath ?? DBNull.Value);
            insert.Parameters.AddWithValue("@CreatedBy", (object?)userId ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return await GetSavedRelatedAsync(repositoryId, tenantId, itemId, cancellationToken);
    }

    public async Task<RepositorySavedRelatedDocumentsResultDto?> AddSavedRelatedAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        SaveRepositoryRelatedDocumentsRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceRepo = await _provisioner.GetRepositoryAsync(repositoryId, tenantId, cancellationToken);
        if (sourceRepo == null)
            return null;

        var source = await _items.GetItemAsync(repositoryId, tenantId, itemId, cancellationToken);
        if (source == null)
            return null;

        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        await EnsureRelatedSchemaAsync(connectionString, cancellationToken);

        var items = (request.Items ?? Array.Empty<SaveRepositoryRelatedDocumentRef>())
            .Where(i => i.RepositoryId != Guid.Empty && i.ItemId != Guid.Empty)
            .Where(i => !(i.RepositoryId == repositoryId && i.ItemId == itemId))
            .GroupBy(i => (i.RepositoryId, i.ItemId))
            .Select(g => g.First())
            .ToList();

        if (items.Count == 0)
            return await GetSavedRelatedAsync(repositoryId, tenantId, itemId, cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var item in items)
        {
            // Skip if already linked (active) for this source item.
            await using (var existsCmd = new SqlCommand(
                             """
                             SELECT TOP (1) 1
                             FROM repository.ItemRelatedDocuments
                             WHERE TenantId = @TenantId
                               AND RepositoryId = @RepositoryId
                               AND ItemId = @ItemId
                               AND RelatedRepositoryId = @RelatedRepositoryId
                               AND RelatedItemId = @RelatedItemId
                               AND IsDeleted = 0;
                             """,
                             connection))
            {
                existsCmd.Parameters.AddWithValue("@TenantId", tenantId);
                existsCmd.Parameters.AddWithValue("@RepositoryId", repositoryId);
                existsCmd.Parameters.AddWithValue("@ItemId", itemId);
                existsCmd.Parameters.AddWithValue("@RelatedRepositoryId", item.RepositoryId);
                existsCmd.Parameters.AddWithValue("@RelatedItemId", item.ItemId);
                var exists = await existsCmd.ExecuteScalarAsync(cancellationToken);
                if (exists != null && exists != DBNull.Value)
                    continue;
            }

            var snapshotRepoName = TrimOrNull(item.RepositoryName);
            var snapshotFileName = TrimOrNull(item.FileName);
            var snapshotFileType = TrimOrNull(item.FileType);
            var snapshotFilePath = TrimOrNull(item.FilePath);

            if (snapshotRepoName == null || snapshotFileName == null || snapshotFileType == null || snapshotFilePath == null)
            {
                var detail = await TryLoadRelatedItemAsync(
                    connectionString,
                    tenantId,
                    item.RepositoryId,
                    item.ItemId,
                    cancellationToken);
                if (detail != null)
                {
                    snapshotRepoName ??= detail.Value.RepositoryName;
                    snapshotFileName ??= detail.Value.FileName;
                    snapshotFileType ??= detail.Value.FileType;
                    snapshotFilePath ??= detail.Value.FilePath;
                }
            }

            await using var insert = new SqlCommand(
                """
                INSERT INTO repository.ItemRelatedDocuments
                    (Id, TenantId, RepositoryId, ItemId, RelatedRepositoryId, RelatedItemId,
                     MatchField, MatchValue, MatchScore,
                     RelatedRepositoryName, FileName, FileType, FilePath,
                     CreatedBy, CreatedAtUtc, IsDeleted)
                VALUES
                    (@Id, @TenantId, @RepositoryId, @ItemId, @RelatedRepositoryId, @RelatedItemId,
                     @MatchField, @MatchValue, @MatchScore,
                     @RelatedRepositoryName, @FileName, @FileType, @FilePath,
                     @CreatedBy, SYSUTCDATETIME(), 0);
                """,
                connection);
            insert.Parameters.AddWithValue("@Id", Guid.NewGuid());
            insert.Parameters.AddWithValue("@TenantId", tenantId);
            insert.Parameters.AddWithValue("@RepositoryId", repositoryId);
            insert.Parameters.AddWithValue("@ItemId", itemId);
            insert.Parameters.AddWithValue("@RelatedRepositoryId", item.RepositoryId);
            insert.Parameters.AddWithValue("@RelatedItemId", item.ItemId);
            insert.Parameters.AddWithValue("@MatchField", (object?)TrimOrNull(request.MatchField) ?? DBNull.Value);
            insert.Parameters.AddWithValue("@MatchValue", (object?)TrimOrNull(request.MatchValue) ?? DBNull.Value);
            insert.Parameters.AddWithValue("@MatchScore", (object?)item.MatchScore ?? DBNull.Value);
            insert.Parameters.AddWithValue("@RelatedRepositoryName", (object?)snapshotRepoName ?? DBNull.Value);
            insert.Parameters.AddWithValue("@FileName", (object?)snapshotFileName ?? DBNull.Value);
            insert.Parameters.AddWithValue("@FileType", (object?)snapshotFileType ?? DBNull.Value);
            insert.Parameters.AddWithValue("@FilePath", (object?)snapshotFilePath ?? DBNull.Value);
            insert.Parameters.AddWithValue("@CreatedBy", (object?)userId ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return await GetSavedRelatedAsync(repositoryId, tenantId, itemId, cancellationToken);
    }

    public async Task<RepositorySavedRelatedDocumentsResultDto?> DeleteSavedRelatedAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        Guid? linkId = null,
        Guid? relatedRepositoryId = null,
        Guid? relatedItemId = null,
        CancellationToken cancellationToken = default)
    {
        var hasLinkId = linkId is { } lid && lid != Guid.Empty;
        var hasRelatedPair = relatedRepositoryId is { } rr && rr != Guid.Empty
                             && relatedItemId is { } ri && ri != Guid.Empty;

        if (!hasLinkId && !hasRelatedPair)
            throw new ArgumentException("Provide linkId, or relatedRepositoryId + relatedItemId.");

        var sourceRepo = await _provisioner.GetRepositoryAsync(repositoryId, tenantId, cancellationToken);
        if (sourceRepo == null)
            return null;

        var source = await _items.GetItemAsync(repositoryId, tenantId, itemId, cancellationToken);
        if (source == null)
            return null;

        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        await EnsureRelatedSchemaAsync(connectionString, cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Soft-delete by link row id (when FE has a real saved link id).
        if (hasLinkId)
        {
            await using var byId = new SqlCommand(
                """
                UPDATE repository.ItemRelatedDocuments
                SET IsDeleted = 1
                WHERE TenantId = @TenantId
                  AND Id = @LinkId
                  AND IsDeleted = 0
                  AND (
                        (RepositoryId = @RepositoryId AND ItemId = @ItemId)
                     OR (RelatedRepositoryId = @RepositoryId AND RelatedItemId = @ItemId)
                  );
                """,
                connection);
            byId.Parameters.AddWithValue("@TenantId", tenantId);
            byId.Parameters.AddWithValue("@LinkId", linkId!.Value);
            byId.Parameters.AddWithValue("@RepositoryId", repositoryId);
            byId.Parameters.AddWithValue("@ItemId", itemId);
            await byId.ExecuteNonQueryAsync(cancellationToken);
        }

        // Soft-delete by related file pair across the open item's saved family.
        if (hasRelatedPair)
        {
            await using var byPair = new SqlCommand(
                """
                UPDATE l
                SET l.IsDeleted = 1
                FROM repository.ItemRelatedDocuments l
                WHERE l.TenantId = @TenantId
                  AND l.IsDeleted = 0
                  AND l.RelatedRepositoryId = @RelatedRepositoryId
                  AND l.RelatedItemId = @RelatedItemId
                  AND (
                        (l.RepositoryId = @RepositoryId AND l.ItemId = @ItemId)
                     OR EXISTS (
                            SELECT 1
                            FROM repository.ItemRelatedDocuments s
                            WHERE s.TenantId = @TenantId
                              AND s.IsDeleted = 0
                              AND s.RelatedRepositoryId = @RepositoryId
                              AND s.RelatedItemId = @ItemId
                              AND s.RepositoryId = l.RepositoryId
                              AND s.ItemId = l.ItemId
                        )
                  );
                """,
                connection);
            byPair.Parameters.AddWithValue("@TenantId", tenantId);
            byPair.Parameters.AddWithValue("@RepositoryId", repositoryId);
            byPair.Parameters.AddWithValue("@ItemId", itemId);
            byPair.Parameters.AddWithValue("@RelatedRepositoryId", relatedRepositoryId!.Value);
            byPair.Parameters.AddWithValue("@RelatedItemId", relatedItemId!.Value);
            await byPair.ExecuteNonQueryAsync(cancellationToken);

            // If FE deletes the synthetic "source" row while on a related file, unlink this open item
            // from that source's saved set (source = relatedRepositoryId/relatedItemId pair).
            await using var unlinkSelf = new SqlCommand(
                """
                UPDATE repository.ItemRelatedDocuments
                SET IsDeleted = 1
                WHERE TenantId = @TenantId
                  AND IsDeleted = 0
                  AND RepositoryId = @RelatedRepositoryId
                  AND ItemId = @RelatedItemId
                  AND RelatedRepositoryId = @RepositoryId
                  AND RelatedItemId = @ItemId;
                """,
                connection);
            unlinkSelf.Parameters.AddWithValue("@TenantId", tenantId);
            unlinkSelf.Parameters.AddWithValue("@RepositoryId", repositoryId);
            unlinkSelf.Parameters.AddWithValue("@ItemId", itemId);
            unlinkSelf.Parameters.AddWithValue("@RelatedRepositoryId", relatedRepositoryId!.Value);
            unlinkSelf.Parameters.AddWithValue("@RelatedItemId", relatedItemId!.Value);
            await unlinkSelf.ExecuteNonQueryAsync(cancellationToken);
        }

        return await GetSavedRelatedAsync(repositoryId, tenantId, itemId, cancellationToken);
    }

    private async Task<RepositoryRelatedDocumentsResultDto?> GetRelatedCoreAsync(
        Guid repositoryId,
        Guid tenantId,
        Guid itemId,
        bool requireAllFields,
        bool useAllRepositoryFields,
        int page,
        int pageSize,
        IReadOnlyList<string>? fields = null,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var sourceRepo = await _provisioner.GetRepositoryAsync(repositoryId, tenantId, cancellationToken);
        if (sourceRepo == null)
            return null;

        var source = await _items.GetItemAsync(repositoryId, tenantId, itemId, cancellationToken);
        if (source == null)
            return null;

        var matchCriteria = BuildMatchCriteria(
            sourceRepo,
            source.Fields,
            useAllRepositoryFields,
            fields,
            value);
        if (matchCriteria.Count == 0)
        {
            return new RepositoryRelatedDocumentsResultDto(
                repositoryId,
                itemId,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>(),
                page,
                pageSize,
                0,
                Array.Empty<RepositoryRelatedDocumentDto>());
        }

        var match = matchCriteria.ToDictionary(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);
        var matchFields = matchCriteria.Select(c => c.Key).ToList();
        // Loose: any 2 of N folder fields.
        // Exact/score: only return rows with matchScore >= 50 (e.g. 8/15 → 53, 7/14 → 50).
        var minRequired = requireAllFields
            ? Math.Max(1, (int)Math.Ceiling(matchCriteria.Count * 0.5))
            : Math.Min(2, matchCriteria.Count);
        const int exactMinScore = 50;

        var connectionString = _connectionProvider.ConnectionString
            ?? throw new InvalidOperationException("Tenant connection string not resolved.");

        var repos = await ListActiveRepositoriesAsync(connectionString, tenantId, cancellationToken);
        var bag = new ConcurrentBag<RepositoryRelatedDocumentDto>();
        using var gate = new SemaphoreSlim(MaxParallelRepos, MaxParallelRepos);

        var tasks = repos.Select(async repo =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var rows = await QueryRepoAsync(
                    connectionString,
                    repo,
                    matchCriteria,
                    minRequired,
                    sourceRepositoryId: repositoryId,
                    sourceItemId: itemId,
                    cancellationToken);
                foreach (var row in rows)
                    bag.Add(row);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Related docs skipped repository {RepositoryId} ({RepositoryName}).",
                    repo.Id,
                    repo.Name);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        IEnumerable<RepositoryRelatedDocumentDto> candidates = bag;
        if (requireAllFields)
            candidates = candidates.Where(x => x.MatchScore >= exactMinScore);

        var ordered = candidates
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.MatchCount)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = ordered.Count;
        var pageData = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new RepositoryRelatedDocumentsResultDto(
            repositoryId,
            itemId,
            match,
            matchFields,
            page,
            pageSize,
            total,
            pageData);
    }

    /// <summary>
    /// Loose: folder-structure fields (fallback Supplier/PO/Invoice).
    /// Exact: all repository-defined fields that have a value on the source item
    /// (skips DYNAMIC_TABLE / line-item payloads), or only the requested field(s).
    /// </summary>
    private static List<MatchCriterion> BuildMatchCriteria(
        RepositoryDetailDto sourceRepo,
        IReadOnlyDictionary<string, object?> fields,
        bool useAllRepositoryFields,
        IReadOnlyList<string>? requestedFields = null,
        string? overrideValue = null)
    {
        var requested = NormalizeRequestedFields(requestedFields);

        IEnumerable<RepositoryFieldDto> sourceFields = useAllRepositoryFields
            ? sourceRepo.Fields
                .Where(f => !IsExcludedFromExactMatch(f.DataType))
                .OrderBy(f => f.OrderId ?? int.MaxValue)
                .ThenBy(f => f.Level)
                .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            : RepositoryFolderStructureHelper.OrderFolderFields(
                sourceRepo.Fields.Where(f => f.IncludeInFolderStructure));

        if (requested.Count > 0)
        {
            sourceFields = sourceFields.Where(f =>
                requested.Any(r =>
                    string.Equals(r, f.SqlColumnName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r, f.Name, StringComparison.OrdinalIgnoreCase)
                    || AliasesFor(f.SqlColumnName, f.Name).Any(a => string.Equals(a, r, StringComparison.OrdinalIgnoreCase))));
        }

        var criteria = new List<MatchCriterion>();
        foreach (var field in sourceFields)
        {
            var aliases = AliasesFor(field.SqlColumnName, field.Name);
            string? value;
            if (!string.IsNullOrWhiteSpace(overrideValue) && requested.Count == 1)
            {
                value = overrideValue.Trim();
            }
            else
            {
                value = ResolveFieldValue(fields, aliases);
            }

            if (string.IsNullOrWhiteSpace(value))
                continue;

            criteria.Add(new MatchCriterion(field.SqlColumnName, NormalizeMatchValue(value, field.DataType), aliases));
        }

        // Particular field + override value, but field not in repo definitions — still search aliases.
        if (criteria.Count == 0
            && useAllRepositoryFields
            && requested.Count == 1
            && !string.IsNullOrWhiteSpace(overrideValue))
        {
            var key = requested[0];
            var aliases = AliasesFor(key, key);
            criteria.Add(new MatchCriterion(key, NormalizeMatchValue(overrideValue.Trim(), null), aliases));
            return criteria;
        }

        if (criteria.Count > 0 || useAllRepositoryFields)
            return criteria;

        // Fallback when repository has no folder-structure fields (loose mode only).
        var supplier = ResolveFieldValue(fields, SupplierAliases);
        var poNumber = ResolveFieldValue(fields, PoAliases);
        var invoiceNo = ResolveFieldValue(fields, InvoiceAliases);

        if (!string.IsNullOrWhiteSpace(supplier) && !string.IsNullOrWhiteSpace(poNumber))
        {
            return
            [
                new MatchCriterion("Supplier", supplier!.Trim(), SupplierAliases),
                new MatchCriterion("PONumber", poNumber!.Trim(), PoAliases)
            ];
        }

        if (!string.IsNullOrWhiteSpace(supplier) && !string.IsNullOrWhiteSpace(invoiceNo))
        {
            return
            [
                new MatchCriterion("Supplier", supplier!.Trim(), SupplierAliases),
                new MatchCriterion("InvoiceNo", invoiceNo!.Trim(), InvoiceAliases)
            ];
        }

        if (!string.IsNullOrWhiteSpace(supplier))
            return [new MatchCriterion("Supplier", supplier!.Trim(), SupplierAliases)];

        return criteria;
    }

    private static List<string> NormalizeRequestedFields(IReadOnlyList<string>? requestedFields)
    {
        if (requestedFields == null || requestedFields.Count == 0)
            return [];

        return requestedFields
            .SelectMany(f => (f ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsExcludedFromExactMatch(string? dataType)
    {
        var dt = (dataType ?? string.Empty).Trim().ToUpperInvariant();
        return dt is "DYNAMIC_TABLE" or "TABLE" or "JSON" or "FILE" or "ATTACHMENT";
    }

    private static string NormalizeMatchValue(string value, string? dataType)
    {
        var trimmed = value.Trim();
        var dt = (dataType ?? string.Empty).Trim().ToUpperInvariant();
        if (dt is "DATE" or "DATETIME")
        {
            if (DateTime.TryParse(trimmed, out var date))
                return date.ToString("yyyy-MM-dd");
        }

        if (dt is "CURRENCY_AMOUNT" or "AMOUNT" or "NUMBER" or "DECIMAL")
        {
            if (decimal.TryParse(trimmed, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var amount)
                || decimal.TryParse(trimmed, out amount))
            {
                return amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return trimmed;
    }

    private static string[] AliasesFor(string sqlColumnName, string name)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sqlColumnName, name };

        void Expand(string[] group)
        {
            if (group.Any(a => set.Contains(a)))
            {
                foreach (var a in group)
                    set.Add(a);
            }
        }

        Expand(SupplierAliases);
        Expand(PoAliases);
        Expand(InvoiceAliases);
        Expand(DocumentTypeAliases);
        return set.ToArray();
    }

    private static async Task<List<(Guid Id, string Name, string ItemsTableName)>> ListActiveRepositoriesAsync(
        string connectionString,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, Name, ItemsTableName
            FROM repository.Repositories
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY Name;
            """;

        var list = new List<(Guid, string, string)>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@TenantId", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(2);
            if (!RepositorySqlHelper.IsValidItemsTableName(table))
                continue;
            list.Add((reader.GetGuid(0), reader.GetString(1), table));
        }

        return list;
    }

    private static async Task<List<RepositoryRelatedDocumentDto>> QueryRepoAsync(
        string connectionString,
        (Guid Id, string Name, string ItemsTableName) repo,
        IReadOnlyList<MatchCriterion> matchCriteria,
        int minRequired,
        Guid sourceRepositoryId,
        Guid sourceItemId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var columns = await RepositoryItemTableColumns.LoadAsync(connection, repo.ItemsTableName, cancellationToken);
        if (minRequired <= 0 || matchCriteria.Count == 0)
            return [];

        var where = new List<string> { "i.RepositoryId = @RepositoryId", "i.IsDeleted = 0" };
        var parameters = new List<SqlParameter> { new("@RepositoryId", repo.Id) };
        var matchScoreParts = new List<string>();
        var resolvedKeys = new List<string>();

        for (var i = 0; i < matchCriteria.Count; i++)
        {
            var criterion = matchCriteria[i];
            var col = ResolveCanonical(columns, criterion.Aliases);
            if (col == null)
                continue; // missing column = unmatched toward score denominator

            var paramName = $"@m{i}";
            parameters.Add(new SqlParameter(paramName, criterion.Value));
            matchScoreParts.Add($"(CASE WHEN i.[{col}] = {paramName} THEN 1 ELSE 0 END)");
            resolvedKeys.Add(criterion.Key);
        }

        if (matchScoreParts.Count == 0 || matchScoreParts.Count < minRequired)
            return [];

        var matchCountExpr = $"({string.Join(" + ", matchScoreParts)})";
        where.Add($"{matchCountExpr} >= @MinRequired");
        parameters.Add(new SqlParameter("@MinRequired", minRequired));

        if (repo.Id == sourceRepositoryId)
        {
            where.Add("i.Id <> @SourceItemId");
            parameters.Add(new SqlParameter("@SourceItemId", sourceItemId));
        }

        var supplierCol = ResolveCanonical(columns, SupplierAliases);
        var poCol = ResolveCanonical(columns, PoAliases);
        var invoiceCol = ResolveCanonical(columns, InvoiceAliases);
        var docTypeCol = ResolveCanonical(columns, DocumentTypeAliases);
        var createdCol = RepositoryItemTableColumns.Has(columns, "CreatedAtUtc") ? "CreatedAtUtc" : null;

        var selectSupplier = supplierCol != null ? $"i.[{supplierCol}]" : "CAST(NULL AS nvarchar(1))";
        var selectPo = poCol != null ? $"i.[{poCol}]" : "CAST(NULL AS nvarchar(1))";
        var selectInvoice = invoiceCol != null ? $"i.[{invoiceCol}]" : "CAST(NULL AS nvarchar(1))";
        var selectDocType = docTypeCol != null ? $"i.[{docTypeCol}]" : "CAST(NULL AS nvarchar(1))";
        var selectCreated = createdCol != null ? $"i.[{createdCol}]" : "CAST(NULL AS datetime2)";
        var orderBy = createdCol != null
            ? $"{matchCountExpr} DESC, i.[{createdCol}] DESC, i.Id DESC"
            : $"{matchCountExpr} DESC, i.Id DESC";

        var fieldFlagSelects = new List<string>();
        for (var i = 0; i < matchScoreParts.Count; i++)
            fieldFlagSelects.Add($"{matchScoreParts[i]} AS F{i}");

        // Denominator: all source criteria (e.g. 14). Numerator: how many matched (e.g. 10 → 71).
        var totalFields = Math.Max(1, matchCriteria.Count);
        var table = RepositorySqlHelper.QualifiedItemsTable(repo.ItemsTableName);
        var sql = $"""
            SELECT TOP (@Limit)
                i.Id,
                i.FileName,
                i.FileType,
                i.FileSize,
                {selectDocType} AS DocumentType,
                {selectSupplier} AS Supplier,
                {selectPo} AS PoNumber,
                {selectInvoice} AS InvoiceNumber,
                {selectCreated} AS CreatedAtUtc,
                {matchCountExpr} AS MatchCount,
                {string.Join(",\n                ", fieldFlagSelects)}
            FROM {table} i
            WHERE {string.Join(" AND ", where)}
            ORDER BY {orderBy};
            """;

        var list = new List<RepositoryRelatedDocumentDto>();
        await using var cmd = new SqlCommand(sql, connection);
        RepositorySqlHelper.AddParameters(cmd, parameters);
        cmd.Parameters.AddWithValue("@Limit", PerRepoLimit);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var matchCount = reader.IsDBNull(9) ? 0 : Convert.ToInt32(reader.GetValue(9));
            var matchedFields = new List<string>();
            for (var i = 0; i < resolvedKeys.Count; i++)
            {
                var flagOrdinal = 10 + i;
                if (!reader.IsDBNull(flagOrdinal) && Convert.ToInt32(reader.GetValue(flagOrdinal)) == 1)
                    matchedFields.Add(resolvedKeys[i]);
            }

            // score = matched / total * 100  (10/14 → 71, 14/14 → 100)
            var score = (int)Math.Round(matchCount * 100.0 / totalFields);
            if (score > 100)
                score = 100;

            list.Add(new RepositoryRelatedDocumentDto(
                repo.Id,
                repo.Name,
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                score,
                matchCount,
                matchedFields));
        }

        return list;
    }

    private static string? ResolveCanonical(HashSet<string> columns, IReadOnlyList<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (RepositoryItemTableColumns.TryGetCanonicalName(columns, alias, out var canonical))
                return canonical;
        }

        return null;
    }

    private static string? ResolveFieldValue(
        IReadOnlyDictionary<string, object?> fields,
        IReadOnlyList<string> aliases)
    {
        foreach (var alias in aliases)
        {
            foreach (var kv in fields)
            {
                if (!string.Equals(kv.Key, alias, StringComparison.OrdinalIgnoreCase))
                    continue;
                var text = kv.Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return null;
    }

    private static async Task EnsureRelatedSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        if (SchemaEnsured.ContainsKey(connectionString))
            return;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(EnsureRelatedSchemaSql, connection) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        SchemaEnsured.TryAdd(connectionString, 0);
    }

    private async Task<(string? RepositoryName, string? FileName, string? FileType, string? FilePath, int? FileSize, string? DocumentType, string? Supplier, string? PoNumber, string? InvoiceNumber)?> TryLoadRelatedItemAsync(
        string connectionString,
        Guid tenantId,
        Guid relatedRepositoryId,
        Guid relatedItemId,
        CancellationToken cancellationToken)
    {
        _ = connectionString;
        try
        {
            var repo = await _provisioner.GetRepositoryAsync(relatedRepositoryId, tenantId, cancellationToken);
            var item = await _items.GetItemAsync(relatedRepositoryId, tenantId, relatedItemId, cancellationToken);
            if (item == null)
                return null;

            return (
                repo?.Name,
                item.FileName,
                item.FileType,
                item.FilePath,
                item.FileSize,
                ResolveFieldValue(item.Fields, DocumentTypeAliases),
                ResolveFieldValue(item.Fields, SupplierAliases),
                ResolveFieldValue(item.Fields, PoAliases),
                ResolveFieldValue(item.Fields, InvoiceAliases));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to load related item {RelatedItemId} in repository {RelatedRepositoryId}.",
                relatedItemId,
                relatedRepositoryId);
            return null;
        }
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : TrimOrNull(fallback);

    private const string EnsureRelatedSchemaSql = """
        IF NOT EXISTS (
            SELECT 1 FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE s.name = N'repository' AND t.name = N'ItemRelatedDocuments')
        BEGIN
            CREATE TABLE repository.ItemRelatedDocuments (
                Id                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ItemRelatedDocuments PRIMARY KEY DEFAULT NEWID(),
                TenantId                UNIQUEIDENTIFIER NOT NULL,
                RepositoryId            UNIQUEIDENTIFIER NOT NULL,
                ItemId                  UNIQUEIDENTIFIER NOT NULL,
                RelatedRepositoryId     UNIQUEIDENTIFIER NOT NULL,
                RelatedItemId           UNIQUEIDENTIFIER NOT NULL,
                MatchField              NVARCHAR(128) NULL,
                MatchValue              NVARCHAR(450) NULL,
                MatchScore              INT NULL,
                RelatedRepositoryName   NVARCHAR(256) NULL,
                FileName                NVARCHAR(512) NULL,
                FileType                NVARCHAR(128) NULL,
                FilePath                NVARCHAR(1024) NULL,
                CreatedBy               UNIQUEIDENTIFIER NULL,
                CreatedAtUtc            DATETIME2(3) NOT NULL CONSTRAINT DF_ItemRelatedDocuments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                IsDeleted               BIT NOT NULL CONSTRAINT DF_ItemRelatedDocuments_IsDeleted DEFAULT (0)
            );
            CREATE INDEX IX_ItemRelatedDocuments_Source
                ON repository.ItemRelatedDocuments (TenantId, RepositoryId, ItemId, IsDeleted, CreatedAtUtc);
        END

        IF COL_LENGTH('repository.ItemRelatedDocuments', 'RelatedRepositoryName') IS NULL
            ALTER TABLE repository.ItemRelatedDocuments ADD RelatedRepositoryName NVARCHAR(256) NULL;
        IF COL_LENGTH('repository.ItemRelatedDocuments', 'FileName') IS NULL
            ALTER TABLE repository.ItemRelatedDocuments ADD FileName NVARCHAR(512) NULL;
        IF COL_LENGTH('repository.ItemRelatedDocuments', 'FileType') IS NULL
            ALTER TABLE repository.ItemRelatedDocuments ADD FileType NVARCHAR(128) NULL;
        IF COL_LENGTH('repository.ItemRelatedDocuments', 'FilePath') IS NULL
            ALTER TABLE repository.ItemRelatedDocuments ADD FilePath NVARCHAR(1024) NULL;
        """;

    private sealed record MatchCriterion(string Key, string Value, string[] Aliases);
}
