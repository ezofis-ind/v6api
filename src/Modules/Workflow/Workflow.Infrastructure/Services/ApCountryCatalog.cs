using System.Globalization;

namespace SaaSApp.Workflow.Infrastructure.Services;

/// <summary>
/// Resolves any ISO country from name/code using .NET <see cref="RegionInfo"/> (no extra NuGet/DLL).
/// </summary>
internal static class ApCountryCatalog
{
    private static readonly Lazy<Catalog> Lazy = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    private static Catalog Data => Lazy.Value;

    /// <summary>Map free-text / ISO code to 2-letter ISO country code (e.g. Canada → CA).</summary>
    public static string? MapToCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var token = raw.Trim();
        if (token.Equals("UN", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            return "UN";

        if (token.Length == 2)
        {
            var code = token.ToUpperInvariant();
            if (code == "UK")
                return "GB";
            return Data.Codes.Contains(code) ? code : null;
        }

        if (token.Length == 3
            && Data.ThreeLetterToTwo.TryGetValue(token, out var fromThree))
            return fromThree;

        if (Data.NameToCode.TryGetValue(token, out var fromName))
            return fromName;

        return null;
    }

    /// <summary>English display name for a 2-letter code.</summary>
    public static string DisplayName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "Unknown";

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized is "UN" or "XX")
            return "Unknown";
        if (normalized == "UK")
            normalized = "GB";

        return Data.CodeToName.TryGetValue(normalized, out var name) ? name : normalized;
    }

    /// <summary>
    /// Country names sorted longest-first for address substring scan
    /// (avoids matching short names before longer ones).
    /// </summary>
    public static IReadOnlyList<(string Token, string Code)> NameTokensLongestFirst => Data.NameTokensLongestFirst;

    private static Catalog Build()
    {
        var nameToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var codeToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var threeToTwo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var code = region.TwoLetterISORegionName?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code) || code.Length != 2)
                continue;

            codes.Add(code);
            codeToName.TryAdd(code, region.EnglishName);

            if (!string.IsNullOrWhiteSpace(region.ThreeLetterISORegionName))
                threeToTwo.TryAdd(region.ThreeLetterISORegionName, code);

            AddName(nameToCode, region.EnglishName, code);
            AddName(nameToCode, region.NativeName, code);
            AddName(nameToCode, region.DisplayName, code);
            AddName(nameToCode, code, code);
        }

        // Common aliases / invoice spellings not always present on RegionInfo.
        foreach (var (alias, code) in Aliases)
            AddName(nameToCode, alias, code);

        // Prefer English names we already have; ensure every code has a display name.
        foreach (var code in codes)
            codeToName.TryAdd(code, code);

        var tokens = nameToCode
            .Where(kv => kv.Key.Length > 2) // skip ISO codes in substring scan
            .Select(kv => (Token: kv.Key, Code: kv.Value))
            .OrderByDescending(x => x.Token.Length)
            .ThenBy(x => x.Token, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Catalog(nameToCode, codeToName, threeToTwo, codes, tokens);
    }

    private static void AddName(IDictionary<string, string> map, string? name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var key = name.Trim();
        if (key.Length < 2)
            return;
        map.TryAdd(key, code);
    }

    private static readonly (string Alias, string Code)[] Aliases =
    [
        ("USA", "US"),
        ("U.S.A.", "US"),
        ("U.S.A", "US"),
        ("U.S.", "US"),
        ("US of America", "US"),
        ("United States of America", "US"),
        ("America", "US"),
        ("UK", "GB"),
        ("U.K.", "GB"),
        ("Great Britain", "GB"),
        ("England", "GB"),
        ("Scotland", "GB"),
        ("Wales", "GB"),
        ("Holland", "NL"),
        ("Deutschland", "DE"),
        ("UAE", "AE"),
        ("Emirates", "AE"),
        ("South Korea", "KR"),
        ("Korea", "KR"),
        ("North Korea", "KP"),
        ("Russia", "RU"),
        ("Russian Federation", "RU"),
        ("Vietnam", "VN"),
        ("Viet Nam", "VN"),
        ("Czech Republic", "CZ"),
        ("Czechia", "CZ"),
        ("Türkiye", "TR"),
        ("Turkey", "TR"),
        ("Ivory Coast", "CI"),
        ("Cote d'Ivoire", "CI"),
        ("Côte d'Ivoire", "CI"),
        ("Swaziland", "SZ"),
        ("Eswatini", "SZ"),
        ("Burma", "MM"),
        ("Myanmar", "MM"),
        ("Persia", "IR"),
        ("Iran", "IR"),
        ("Palestine", "PS"),
        ("Taiwan", "TW"),
        ("Hong Kong", "HK"),
        ("Macau", "MO"),
        ("Macao", "MO"),
        ("Brasil", "BR"),
        ("España", "ES"),
        ("Italia", "IT"),
        ("Bharat", "IN"),
        ("Hindustan", "IN")
    ];

    private sealed record Catalog(
        IReadOnlyDictionary<string, string> NameToCode,
        IReadOnlyDictionary<string, string> CodeToName,
        IReadOnlyDictionary<string, string> ThreeLetterToTwo,
        HashSet<string> Codes,
        IReadOnlyList<(string Token, string Code)> NameTokensLongestFirst);
}
