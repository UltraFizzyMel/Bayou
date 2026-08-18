using UnityEngine;

/// <summary>
/// Optional world beacon: place on NPCs, pickups, fishing spots, etc.
/// The quest marker prefers matching beacons for the active quest.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestMarkerTarget : MonoBehaviour
{
    public enum MarkerRole
    {
        Objective,
        TurnIn
    }

    [Tooltip("Quest ids this beacon serves (e.g. CollectPondItemQuest). Empty = any quest.")]
    [SerializeField] private string[] questIds;

    [SerializeField] private MarkerRole role = MarkerRole.Objective;

    [Tooltip("Optional item id filter (e.g. Item_Lantern, Item_ShinyPond).")]
    [SerializeField] private string itemId;

    [SerializeField] private string labelOverride;

    [SerializeField] private Vector3 markerOffset = new(0f, 1.6f, 0f);

    private static readonly System.Collections.Generic.List<QuestMarkerTarget> All = new();

    public Vector3 MarkerWorldPosition => transform.position + markerOffset;
    public string Label => string.IsNullOrWhiteSpace(labelOverride) ? name : labelOverride;
    public MarkerRole Role => role;
    public string ItemId => itemId;

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable() => All.Remove(this);

    public bool MatchesQuest(string questId, bool turnIn)
    {
        if (turnIn && role != MarkerRole.TurnIn) return false;
        if (!turnIn && role != MarkerRole.Objective) return false;

        if (questIds == null || questIds.Length == 0)
            return true;

        if (string.IsNullOrWhiteSpace(questId)) return false;
        for (var i = 0; i < questIds.Length; i++)
        {
            if (string.Equals(questIds[i], questId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool MatchesItem(string requiredItemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return true;
        if (string.IsNullOrWhiteSpace(requiredItemId)) return true;
        return string.Equals(itemId, requiredItemId, System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryFind(
        string questId,
        bool turnIn,
        string preferredItemId,
        Vector3 near,
        out QuestMarkerTarget best)
    {
        best = null;
        var bestSq = float.MaxValue;
        for (var i = 0; i < All.Count; i++)
        {
            var t = All[i];
            if (t == null || !t.isActiveAndEnabled) continue;
            if (!t.MatchesQuest(questId, turnIn)) continue;
            if (!t.MatchesItem(preferredItemId)) continue;

            var d = t.MarkerWorldPosition - near;
            d.y = 0f;
            var sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = t;
            }
        }

        return best != null;
    }
}
