using System.Collections.Generic;

/// <summary>
/// Ink still uses a few quest ids that don't match the QuestInfoSO assets.
/// Canonical names are what <see cref="QuestManager"/> stores.
/// Dialogue state variables stay on the ink names (questId + "State").
/// </summary>
public static class QuestIdMap
{
    public static string Canonical(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId)) return questId;
        switch (questId.Trim())
        {
            case "AMealToDieForOlivierQuest":
                return QuestIds.MealToDieForOlivier;
            case "AMealToDieForQuest":
                return QuestIds.MealToDieForMarie;
            case "AccordianDisposalQuest":
                return QuestIds.AccordionDisposal;
            case "ASearchForAHerbalistQuest":
                return QuestIds.SearchForHerbalist;
            case "TheseRitualComponentsSeemSlipperyQuest":
                return QuestIds.RitualComponents;
            case "AGraveRevelationQuest":
                return QuestIds.VisitGraves;
            case "ACatfishAndAGrarQuest":
                return QuestIds.CatfishAndGar;
            case "ASecondWayOutQuest":
                return QuestIds.SecondWayOut;
            default:
                return questId.Trim();
        }
    }

    public static IEnumerable<string> StateVariableNames(string questId)
    {
        var canonical = Canonical(questId);
        if (string.IsNullOrWhiteSpace(canonical))
            yield break;

        yield return canonical + "State";

        if (canonical == QuestIds.MealToDieForOlivier)
            yield return "AMealToDieForOlivierQuestState";
        else if (canonical == QuestIds.MealToDieForMarie)
            yield return "AMealToDieForQuestState";
        else if (canonical == QuestIds.AccordionDisposal)
            yield return "AccordianDisposalQuestState";
        else if (canonical == QuestIds.SearchForHerbalist)
            yield return "ASearchForAHerbalistQuestState";
        else if (canonical == QuestIds.RitualComponents)
            yield return "TheseRitualComponentsSeemSlipperyQuestState";
        else if (canonical == QuestIds.VisitGraves)
            yield return "AGraveRevelationQuestState";
        else if (canonical == QuestIds.CatfishAndGar)
            yield return "ACatfishAndAGrarQuestState";
        else if (canonical == QuestIds.SecondWayOut)
            yield return "ASecondWayOutQuestState";
    }
}
