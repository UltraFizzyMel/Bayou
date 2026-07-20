 INCLUDE globals.ink
 
 EXTERNAL OpenShop()
 EXTERNAL StartQuest(questId)
 EXTERNAL AdvanceQuest(questId)
 EXTERNAL FinishQuest(questId)
 EXTERNAL HasItem(itemId, count)
 EXTERNAL HandOverItem(itemId, count)
 EXTERNAL GiveMoney(amount)
 
 
#portrait: caliste_ardoin_neutral #layout: left #background: merchant 
->SnapperAndMollyQuestStart

=== SnapperAndMollyQuestStart ===
#portrait: caliste_ardoin_neutral #layout: left #background: merchant 
{ SnapperAndMollyQuestState :
    - "REQUIREMENTS_NOT_MET": -> main
    - "CAN_START": -> main
    - "IN_PROGRESS": -> main
    - "CAN_FINISH": -> main
    - "FINISHED":  {caliste_known == "":-> notKnown |-> knownName}
    - else: -> main
}

=== notKnown ===
<b>DON'T...</b> Oh it's you...
You want to see my wares?
-> questions

=== knownName ===
#speaker: Caliste
<b>DON'T...</b> Oh it's you.
You want to see my wares?
-> questions

=== main ===
<b>DON'T COME ANY CLOSER!!!</b>
-> calm

=== calm ===
+ {HasItem("Item_RedSnapper", 1) && HasItem("Item_SailfinMolly", 1)} [I have a Snapper and a Molly.] -> DeliveredItem
+[I just want to talk.]
-> speak
+[I'll give you your space.]->END

=== speak ===
// Start as soon as this knot runs (same Continue as the first line),
// so the quest log updates even if the player skips the last line.
~ StartQuest("SnapperAndMollyQuest")
Talk...
You seem...
Fetch me a Snapper and a Molly, then I'll talk.
My wares may be of interest...
-> END

=== DeliveredItem ===
~ temp handedSnapper = HandOverItem("Item_RedSnapper", 1)
~ temp handedMolly = HandOverItem("Item_SailfinMolly", 1)
{ handedSnapper && handedMolly:
    ~ FinishQuest(SnapperAndMollyQuestId)
    ~ GiveMoney(150)
    You might just be the real deal sonny.
    I'll let you peek at my wares.
    -> questions
- else:
    Hmm. Bring me one Snapper and one Molly.
    -> END
}

=== questions ===
+[Who are you?]
 {caliste_known == "":-> introduction("Caliste") | -> repeatName}
+[Wares?]
 -> shop
+[Chat Later.]
 -> goodbye

=== introduction(name) ===
 ~ caliste_known = name
 <color=\#F8FF30>Caliste.</color> #speaker: Caliste
 +[Ah...]
 -> questions

=== repeatName ===
#speaker: Caliste
 Tsk...
 ->questions

=== shop ===
 Take a look, but don't touch what you can't pay for.
 I've got a proper rod… and a key for the maze gate beside me.
 That path runs north-east through the fog — toward the lantern.
 ~ OpenShop()
 -> END

=== goodbye ===
#speaker: Caliste
 I'll be seeing you.
 -> END
