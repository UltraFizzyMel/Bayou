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
...
~sabine_known = "Sabine"
You must be yearning to find her.
+[Find who...?]
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
{SpecialDeliveryQuestState == "FINISHED" && AGraveRevelationQuestState == "FINISHED" && ACatfishAndAGrarQuestState == "FINISHED":->APleadForHelp}
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

 *{flowerPlaced == true}[I placed the flower.]
 ->PlacedFlower
 *{ HasItem("Item_Catfish", 1) && HasItem("Item_Grar", 1) && BaptistesFued && !ACatfishAndAGrar}[I have the Catfish and the Grar.]->ACatfishAndAGrar
 
 *{HasHerbalistFlower == true && !DeliverFlower}[I have a flower for you.]
 -> DeliverFlower 
 
+[Stay safe Sabine.]
 -> goodbye
 
 ===ACatfishAndAGrar
 Hopefully this offering will lead to a new spark.
 Doesn't it feel good to share your flame with another?
 +[I don't remember.]
 ->ForgottenFlame
 
 ===ForgottenFlame
 Well...That warmth has the potential to create a cozy cove that keeps your heart safe.
 It once did for me...
 ...
 Thank you for your assistance.
 Is there anything you still need from me?
 ~FinishQuest(ACatfishAndAGrarQuestId)
 ->StandardSabineBranching
 
 ===SabineQuestions
 What question {shuffle:eats away at you?|plagues your mind?}
 
 *[Do you know Caliste?]
 ~sabinequestionsasked = sabinequestionsasked + 1
 ->SabineCaliste
 +{SabineCaliste}[How are things with Caliste?]
 ->SabineCalisteRelationship
 *{marie_known && olivier_known}[Do you know the Baptistes?]
 ->BaptistesExplanation
 
 *{BaptistesExplanation && marie_known && olivier_known && sabinequestionsasked >=2}[What would you do about the Baptistes?]
 ~sabinequestionsasked = sabinequestionsasked + 1
 ->BaptistesFued
 
 *{PlacedFlower}[You called me Mr. Lafleur?]
 ~sabinequestionsasked = sabinequestionsasked + 1
 ->MrLafleur
 
 +[That's all for now.]
 ->goodbye
 
=== BaptistesExplanation
->END
 
===BaptistesFued
What do you mean?
+[They seem to want to kill one another...]
->SimpleBaptisteSolution

===SimpleBaptisteSolution
Nothing a heartfelt meal can't fix. The way to someone's heart can often be through their stomach afterall.
Bring me a catfish and a grar and don't tell them anything. 
 ~StartQuest(ACatfishAndAGrarQuestId)
->END

===SabineCaliste
I do...
I haven't seen them in the flesh for quite the long time.
Should you have a moment to spare could you please get me a Red Snapper and a Sailfin Molly?
Without them they will refuse to see me for who I am.
~StartQuest(SpecialDeliveryQuestId)
->END

===SabineCalisteRelationship
->END

===MrLafleur
->END


 ===DeliverFlower
 ~deliveredFlower = true
 ~StartQuest(AGraveRevelationQuestId)
oh my flower.
I cannot take.
Please place.
+[I can do so]
->DeliverFlowerToGrave

===DeliverFlowerToGrave
 Grave in the Graveyard. Caliste grave
 ->END

 
 ===PlacedFlower
 //On placing the flower the player should get key for deeper in the graveyard not on returning to Sabine.
I trust you found a missing thread on the tapestry you're walking?
Keep a gentle touch lest the thread dissolves your painted silk into a river of loose fibres.
That key you found leads deeper into the graveyards depths.
...
Thank you Mr. Lafleur.
 ~FinishQuest(AGraveRevelationQuestId)
 ->END
 
 === goodbye ===
 {goodbye <=16:Stay safe. -> END}
 {goodbye >= 16 && !TheseRitualComponentsSeemSlipperyStart &&!ASlipUp: Stay safe.->END}
 {goodbye >= 16 && TheseRitualComponentsSeemSlipperyStart &&!ASlipUp: Stay safe. Fabian.->ASlipUp}
 {goodbye >= 16 && ASlipUp: Stay safe...->END}

===ASlipUp
*[Who is Fabian?]
 ->Fabian
 
 ===Fabian
 ...
 You should know better than me.
->END
 
 ===SpecialDeliveryInProgress
 ~ASpecialMollyDelivery = true
 Hello there.
 Have you found a Snapper and a Molly?
 +{HasItem("Item_Snapper", 1) && HasItem("Item_Molly", 1)}[I have.]->SpecialDeliveryDelivered
 +[I haven't.]
 ->needhelpYourself
 
 ===needhelpYourself
 Do you need any help yourself?
 ->StandardSabineBranching
 
 ===SpecialDeliveryDelivered
 Your actions have not gone unnoticed.
 Let me give that lantern of yours a little tap.
 ...
 +[Alright?]
 ->ALittleTap
 
 ===ALittleTap
 ...
 Your light should reach further... Scaring away the shadows better than before.
 ->goodbye
 
 ===APleadForHelp
 please helkp ritual
 +[yes]
  ~StartQuest(TheseRitualComponentsSeemSlipperyQuestId)
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
 ~FinishQuest(TheseRitualComponentsSeemSlipperyQuestId)
 ->END
 ===UtmostImportance
 ->END
 
 ===ASecondWayOutInProgress
 ->END