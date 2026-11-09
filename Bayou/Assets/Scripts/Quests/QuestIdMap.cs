using System.Collections.Generic;

/// <summary>
/// Ink still uses a few quest ids that don't match the QuestInfoSO assets.
/// Canonical names are what <see cref="QuestManager"/> stores.
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
    }
}
