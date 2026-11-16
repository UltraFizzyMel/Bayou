using System.Text.RegularExpressions;
using TMPro;

/// <summary>
/// Colors Red Snapper and Sailfin Molly names in quest UI for that quest only.
/// Hex values are sampled from the item icons, not invented.
/// Snapper body red-orange #A84030 (Art/Fish/red_snapper_-_Copy 1.png).
/// Molly blue stripe #6070A8 (Art/Fish/sailfin_molly 1.png). The body is a lighter gray-blue #8890A0.
/// </summary>
public static class QuestFishNames
{
    public const string SnapperHex = "#A84030";
    public const string MollyHex = "#6070A8";

    private static readonly Regex Names = new(
        @"\b(red\s+snapper|sailfin\s+molly|snapper|molly)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsSnapperQuest(string questId) =>
        string.Equals(questId, QuestIds.SnapperAndMolly, System.StringComparison.OrdinalIgnoreCase);

    public static string Tint(string questId, string text)
    {
        if (string.IsNullOrEmpty(text) || !IsSnapperQuest(questId))
            return text ?? "";
        return Names.Replace(text, MatchColor);
    }

    public static void Apply(TextMeshProUGUI label, string questId, string text)
    {
        if (label == null) return;
        label.richText = true;
        label.text = Tint(questId, text);
    }

    private static string MatchColor(Match match)
    {
        var word = match.Value;
        var molly = word.IndexOf("molly", System.StringComparison.OrdinalIgnoreCase) >= 0;
        return "<color=" + (molly ? MollyHex : SnapperHex) + ">" + word + "</color>";
    }
}
