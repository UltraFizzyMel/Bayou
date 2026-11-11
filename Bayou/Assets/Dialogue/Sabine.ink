INCLUDE globals.ink


EXTERNAL StartQuest(questId)

EXTERNAL AdvanceQuest(questId)

EXTERNAL FinishQuest(questId)

EXTERNAL HasItem(itemId, count)

EXTERNAL HandOverItem(itemId, count)
  
 #portrait: sabine_sylve_neutral #layout: left #background: bayouBG
 {sabine_known == "":-> main |-> knownName}
 
=== knownName ===
#speaker: Sabine
-> SpecialDeliveryStart

=== main ===
-> SpecialDeliveryStart

 === SpecialDeliveryStart ===

{ SpecialDeliveryQuestState :

    - "IN_PROGRESS": -> SpecialDeliveryInProgress

    - "CAN_FINISH": -> SpecialDeliveryInProgress

    - "FINISHED": -> TheseRitualComponentsSeemSlipperyStart

    - else: -> SabineFirstmeeting

 }
 
//=== ACatfishAndAGrarStart ===

 //{ LostAccordionQuestState :

   // - "IN_PROGRESS": -> ACatfishAndAGrarInProgress

   // - "CAN_FINISH": -> ACatfishAndAGrarInProgress

   // - "FINISHED": -> AMealToDieForOlivierStart

    //- else: -> OlivierStandardIntro

// } 

=== TheseRitualComponentsSeemSlipperyStart ===
 
{ TheseRitualComponentsSeemSlipperyQuestState :

    - "IN_PROGRESS": -> TheseRitualComponentsSeemSlipperyInProgress

    - "CAN_FINISH": -> TheseRitualComponentsSeemSlipperyInProgress

    - "FINISHED": -> ASecondWayOutStart

    - else: -> StandardSabineIntro

 } 
 
 === ASecondWayOutStart ===
 
 { TheseRitualComponentsSeemSlipperyQuestState :

    - "IN_PROGRESS": -> ASecondWayOutInProgress

    - "CAN_FINISH": -> ASecondWayOutInProgress

  //  - "FINISHED": -> ASecondWayOutStart

    - else: -> StandardSabineIntro

 } 
 
===SabineFirstmeeting
Oh... Hello there.
We meet again.
+[again?]
->Again

===Again
...
The bayou waters have eroded who you once were.
#speaker: Sabine
You once knew me as a shape across the pond, Sabine.
~sabine_known = "Sabine"
You must be yearning to find her.
+[Who...?]
->FindHer

===FindHer
Ah... The winds have taken her from you too?
Do not look for her face amoungst the trees for she is...
I cannot say more, they're here.
+[Who is here??]->TheyAreHere

===TheyAreHere
You should know as well as I do.
Dwell no further on what is past. Do you have anything you need from me?
->StandardSabineBranching

===StandardSabineIntro
Hello there.
{shuffle:
- The grasses are still whispering your name. 
- The moon watches you fondly from amidst the stars. A speck blowing in the wind.
- The trees bristle in apprehension. They sense your unease.
}
What help are you seeking?
->StandardSabineBranching

//if finished all quests then please i need ritiual components
->APleadForHelp

=== StandardSabineBranching ===
+[Can I ask you Something?]
->SabineQuestions

 +[I placed the flower]
 ->PlacedFlower
 +[I have something for you.]
 -> HaveSomething 
+[Chat Later]
 -> goodbye
 
 ===HaveSomething
 +[Deliver 2 Fish]
 ->ACatfishAndAGrar
 +[deliver Flower]
 ->DeliverFlower
 
 ===ACatfishAndAGrar
 ~FinishQuest(ACatfishAndAGrarQuestId)
 ->END
 
 ===SabineQuestions
 
 +[Baptistes]
 ->BaptistesFued
 +[Caliste]
 ->END
 
===BaptistesFued
Simple solution
Bring me 2 fish and they can make both happy.
 ~StartQuest(ACatfishAndAGrarQuestId)
->END

 ===DeliverFlower
 ~deliveredFlower = true
 ~StartQuest(AGraveRevelationQuestId)
 oh my flower.
 I cannot take.
Please place.
[I can do so]
->DeliverFlowerToGrave

===DeliverFlowerToGrave
 Grave in the Graveyard. Caliste grave
 ->END

 
 ===PlacedFlower
 Thank you.
 ~FinishQuest(AGraveRevelationQuestId)
 ->END
 
 === goodbye ===
 until next time my friend.
 -> END
 
 ===SpecialDeliveryInProgress
 ->END
 
 ===APleadForHelp
 please helkp ritual
 +[yes]
  ~StartQuest(AGraveRevelationQuestId)
  ->Helpritual
  +[No.]
  ->NoHelpritual
  +[I came for something else.]
  ->StandardSabineBranching
  
 === Helpritual
 thank you get 3 fish
 ->END
 
 ===NoHelpritual
 ->END
 
 === TheseRitualComponentsSeemSlipperyInProgress
 Have you the fish?
 +[Yes]
 ->Ritualcomplete
 +[No]
 ->UtmostImportance
 +[Here for something else]
 ->StandardSabineBranching
 
 ===Ritualcomplete
 ~FinishQuest(AGraveRevelationQuestId)
 ->END
 ===UtmostImportance
 ->END
 
 ===ASecondWayOutInProgress
 ->END