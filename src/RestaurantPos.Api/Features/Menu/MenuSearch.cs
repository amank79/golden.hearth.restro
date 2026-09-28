using RestaurantPos.Api.Data;

namespace RestaurantPos.Api.Features.Menu;

/// <summary>
/// Finds dishes by name or short code (MENU-6). "PBM" finds Paneer Butter Masala by its short code or by the
/// first letters of its words; "paneer" finds every dish with paneer in the name. Best matches come first.
/// The billing screen uses the same rules (src/web/src/lib/menuSearch.ts) so the counter can search without a round trip.
/// </summary>
public static class MenuSearch
{
    /// <summary>Lower is better; null means no match.</summary>
    public static int? Rank(MenuItem item, string query) => Rank(item.Name, item.ShortCode, query);

    public static int? Rank(string name, string? shortCode, string query)
    {
        var q = query.Trim();
        if (q.Length == 0) return 0;
        var code = shortCode ?? "";

        if (code.Equals(q, StringComparison.OrdinalIgnoreCase)) return 0;
        if (code.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 2;

        var words = name.Split([' ', '-', '(', ')', '&', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(q, StringComparison.OrdinalIgnoreCase))) return 3;

        var initials = string.Concat(words.Select(w => w[0]));
        if (q.Length >= 2 && initials.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 4;

        if (name.Contains(q, StringComparison.OrdinalIgnoreCase)) return 5;
        return null;
    }
}
