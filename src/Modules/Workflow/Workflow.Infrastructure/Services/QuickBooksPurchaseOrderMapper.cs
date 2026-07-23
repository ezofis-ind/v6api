using System.Text.Json;
using SaaSApp.Workflow.Application.Connectors;

namespace SaaSApp.Workflow.Infrastructure.Services;

internal static class QuickBooksPurchaseOrderMapper
{
    public static ConnectorQuickBooksPurchaseOrderDto FromRawJson(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var el = doc.RootElement;

        var id = el.TryGetProperty("Id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
        string? docNumber = el.TryGetProperty("DocNumber", out var dn) ? dn.GetString() : null;
        string? txnDate = el.TryGetProperty("TxnDate", out var td) ? td.GetString() : null;
        string? dueDate = el.TryGetProperty("DueDate", out var dd) ? dd.GetString() : null;

        string? vendorId = null;
        string? vendorName = null;
        if (el.TryGetProperty("VendorRef", out var vr))
        {
            if (vr.TryGetProperty("value", out var v)) vendorId = v.GetString();
            if (vr.TryGetProperty("name", out var n)) vendorName = n.GetString();
        }

        decimal? total = el.TryGetProperty("TotalAmt", out var ta) && ta.TryGetDecimal(out var amt) ? amt : null;
        string? currency = null;
        if (el.TryGetProperty("CurrencyRef", out var cr) && cr.TryGetProperty("value", out var cv))
            currency = cv.GetString();

        string? poStatus = el.TryGetProperty("POStatus", out var ps) ? ps.GetString() : null;
        string? emailStatus = el.TryGetProperty("EmailStatus", out var es) ? es.GetString() : null;
        string? memo = el.TryGetProperty("PrivateNote", out var pn) ? pn.GetString() : null;
        if (string.IsNullOrWhiteSpace(memo) && el.TryGetProperty("Memo", out var memoEl))
            memo = memoEl.GetString();

        var lines = new List<ConnectorQuickBooksPoLineDto>();
        if (el.TryGetProperty("Line", out var lineArr) && lineArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in lineArr.EnumerateArray())
            {
                var detailType = line.TryGetProperty("DetailType", out var dt) ? dt.GetString() : null;
                // Skip subtotals / descriptions-only rows with no amount detail when DetailType is blank.
                if (string.Equals(detailType, "SubTotalLineDetail", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? lineId = line.TryGetProperty("Id", out var lid) ? lid.GetString() : null;
                int? lineNum = line.TryGetProperty("LineNum", out var ln) && ln.TryGetInt32(out var n) ? n : null;
                string? description = line.TryGetProperty("Description", out var desc) ? desc.GetString() : null;
                decimal? amount = line.TryGetProperty("Amount", out var a) && a.TryGetDecimal(out var am) ? am : null;

                string? itemId = null, itemName = null, accountId = null, accountName = null;
                decimal? qty = null, unitPrice = null;

                if (line.TryGetProperty("ItemBasedExpenseLineDetail", out var itemDetail))
                {
                    if (itemDetail.TryGetProperty("ItemRef", out var ir))
                    {
                        if (ir.TryGetProperty("value", out var iv)) itemId = iv.GetString();
                        if (ir.TryGetProperty("name", out var iname)) itemName = iname.GetString();
                    }
                    if (itemDetail.TryGetProperty("Qty", out var q) && q.TryGetDecimal(out var qd)) qty = qd;
                    if (itemDetail.TryGetProperty("UnitPrice", out var up) && up.TryGetDecimal(out var upd)) unitPrice = upd;
                    if (itemDetail.TryGetProperty("ItemAccountRef", out var iar))
                    {
                        if (iar.TryGetProperty("value", out var iav)) accountId = iav.GetString();
                        if (iar.TryGetProperty("name", out var ian)) accountName = ian.GetString();
                    }
                }
                else if (line.TryGetProperty("AccountBasedExpenseLineDetail", out var acctDetail))
                {
                    if (acctDetail.TryGetProperty("AccountRef", out var ar))
                    {
                        if (ar.TryGetProperty("value", out var av)) accountId = av.GetString();
                        if (ar.TryGetProperty("name", out var an)) accountName = an.GetString();
                    }
                }

                lines.Add(new ConnectorQuickBooksPoLineDto(
                    lineId, lineNum, detailType, description, amount,
                    itemId, itemName, qty, unitPrice, accountId, accountName));
            }
        }

        object? rawObj = JsonSerializer.Deserialize<object>(rawJson);

        return new ConnectorQuickBooksPurchaseOrderDto(
            id,
            docNumber,
            txnDate,
            dueDate,
            vendorId,
            vendorName,
            total,
            currency,
            poStatus,
            emailStatus,
            memo,
            lines,
            rawObj);
    }
}
