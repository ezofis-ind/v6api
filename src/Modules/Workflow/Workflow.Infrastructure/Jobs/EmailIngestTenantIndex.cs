using System.Collections.Concurrent;

namespace SaaSApp.Workflow.Infrastructure.Jobs;

/// <summary>
/// In-memory index of tenants that have at least one enabled email-ingest mailbox.
/// Hangfire only polls tenants in this index — it does not scan every tenant every minute.
/// </summary>
public static class EmailIngestTenantIndex
{
    private static readonly ConcurrentDictionary<Guid, string> Tenants = new();
    private static long _lastFullScanTicks;
    private static readonly object ScanGate = new();

    /// <summary>How often a full catalog scan is allowed (to discover newly created mailboxes).</summary>
    public static TimeSpan FullScanInterval { get; set; } = TimeSpan.FromMinutes(30);

    public static IReadOnlyCollection<KeyValuePair<Guid, string>> Snapshot() =>
        Tenants.ToArray();

    public static int Count => Tenants.Count;

    public static void Register(Guid tenantId, string? tenantName)
    {
        if (tenantId == Guid.Empty)
            return;
        var label = string.IsNullOrWhiteSpace(tenantName) ? tenantId.ToString("D") : tenantName.Trim();
        Tenants.AddOrUpdate(tenantId, label, (_, _) => label);
    }

    public static void Unregister(Guid tenantId) =>
        Tenants.TryRemove(tenantId, out _);

    public static void ReplaceAll(IEnumerable<(Guid TenantId, string Name)> tenants)
    {
        var keep = new HashSet<Guid>();
        foreach (var (id, name) in tenants)
        {
            keep.Add(id);
            Register(id, name);
        }

        foreach (var existing in Tenants.Keys)
        {
            if (!keep.Contains(existing))
                Tenants.TryRemove(existing, out _);
        }

        Volatile.Write(ref _lastFullScanTicks, DateTime.UtcNow.Ticks);
    }

    public static bool NeedsFullScan()
    {
        var last = Interlocked.Read(ref _lastFullScanTicks);
        if (last == 0)
            return true;
        return DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc) >= FullScanInterval;
    }

    public static void MarkFullScanDone() =>
        Volatile.Write(ref _lastFullScanTicks, DateTime.UtcNow.Ticks);
}
