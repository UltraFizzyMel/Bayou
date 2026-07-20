 INCLUDE globals.ink
 
 EXTERNAL OpenShop()
 EXTERNAL StartQuest(questId)
 EXTERNAL AdvanceQuest(questId)
 EXTERNAL FinishQuest(questId)
 
 
#portrait: caliste_ardoin_neutral #layout: left #background: merchant
->SnapperAndMollyQuestStart

=== SnapperAndMollyQuestStart ===
#portrait: caliste_ardoin_neutral #layout: left #background: merchant
{ SnapperAndMollyQuestState :
    - "CAN_START": -> main
    
    - "IN_PROGRESS": -> main
    - "CAN_FINISH": -> main
    - "FINISHED":  {caliste_known == "":-> notKnown |-> knownName}
    - else: -> END
 }


 
=== notKnown ===
<b>DON'T...</b> Oh it's you.
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
+ {HasItem("Item_ShinyPond", 1)} [I have the fish!] ->DeliveredItem 
   ~FinishQuest(SnapperAndMollyQuestId)
+[I just want to talk.]
-> speak
+[I'll give you your space.]->END

=== speak ===
Talk...
You seem... 
Fetch me a Snapper and a Molly, then I'll talk.
My wares may be of interest...
   ~StartQuest(SnapperAndMollyQuestId)
-> END

===DeliveredItem
You might just be the real deal sonny.
I'll let you peek at my wares.
-> questions

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
 Tsk...
 ->questions
 
 === shop ===
 #speaker: Caliste
 Take a look, but don't touch what you can't pay for.
 ~ OpenShop()
 -> END

 === goodbye ===
 I'll be seeing you.
 -> END
