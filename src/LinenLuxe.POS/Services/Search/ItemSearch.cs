using LinenLuxe.POS.Models;

namespace LinenLuxe.POS.Services.Search;

/// <summary>
/// Relevance-aware search over the price list.
///
/// A plain "contains" match is useless at a busy counter: typing "bra" would drag
/// in "Shoe Cleaning - Branded". Instead we match the way a person expects —
/// on the START of the name, then on the start of any word inside it, and only
/// fall back to a loose match when nothing better exists.
/// </summary>
public static class ItemSearch
{
    private const StringComparison Cmp = StringComparison.OrdinalIgnoreCase;

    // Characters that separate words in item names, e.g. "Kandura / Dishdasha".
    private static readonly char[] WordSeparators = [' ', '/', '-', '(', ')', '+', ',', '.'];

    /// <summary>Filter the price list by what the staff member typed.</summary>
    public static List<PosItem> Filter(IEnumerable<PosItem> items, string query)
    {
        var list = items as IList<PosItem> ?? items.ToList();
        var q = query.Trim();
        if (q.Length == 0) return [.. list];

        // Tier 1 — the name begins with what was typed.
        //   "shirt" -> "Shirt", "Shirt - Security Uniform"      "bra" -> "Bra"
        // Shortest first, so the closest match is always on top.
        var nameStarts = list
            .Where(i => i.Name.StartsWith(q, Cmp))
            .OrderBy(i => i.Name.Length)
            .ThenBy(i => i.SortOrder)
            .ToList();

        // Tier 2 — a WHOLE word inside the name is what was typed.
        //   "shirt" -> "Inner Shirt", "Baby Shirt"
        // Whole-word is the key: it keeps every shirt together, while "bra" still
        // won't drag in "Shoe Cleaning - Branded" (that's only a part of a word).
        var wordExact = list
            .Where(i => !nameStarts.Contains(i) && HasWord(i.Name, q, exact: true))
            .OrderBy(i => i.SortOrder)
            .ToList();

        if (nameStarts.Count > 0 || wordExact.Count > 0)
            return [.. nameStarts, .. wordExact];

        // Tier 3 — nothing solid matched, so help a half-typed word along:
        //   "kandu" -> "Baby Kandura"
        var wordStarts = list.Where(i => HasWord(i.Name, q, exact: false)).ToList();
        if (wordStarts.Count > 0) return wordStarts;

        // Tier 4 — last resort: the letters appear somewhere in the name.
        return list.Where(i => i.Name.Contains(q, Cmp)).ToList();
    }

    // Does any word of the name equal (or begin with) the query?
    private static bool HasWord(string name, string q, bool exact)
    {
        foreach (var word in name.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (exact ? string.Equals(word, q, Cmp) : word.StartsWith(q, Cmp))
                return true;
        }
        return false;
    }
}
