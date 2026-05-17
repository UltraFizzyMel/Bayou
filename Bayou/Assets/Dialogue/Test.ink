EXTERNAL StartQuest(questId)

This is a test!
Here's another line!

//quest ids(questID + "Id" for variable name
VAR CollectCoinsQuestId = "CollectFishQuest"

//quest states (questId + "State" for variable name)
VAR CollectFishQuestState = "REQUIREMENTS_NOT_MET"

=== collectFishStart ===
{ CollectFishQuestState :
    - "REQUIREMENTS_NOT_MET": -> requirementsNotMet
    - "CAN_START": -> canStart
    - "IN_PROGRESS": -> inProgress
    - "CAN_FINISH": -> canFinish
    - "FINISHED": -> finished
    - else: -> END
}
= requirementsNotMet
-> END

= canStart
~  StartQuest("CollectFishQuest")
-> END

= inProgress
-> END

= canFinish
-> END

= finished
-> END
