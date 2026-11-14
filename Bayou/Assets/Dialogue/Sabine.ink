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
 
 { ASecondWayOutQuestState :

    - "IN_PROGRESS": -> ASecondWayOutInProgress

    - "CAN_FINISH": -> ASecondWayOutInProgress

    - "FINISHED": -> StandardSabineIntro

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
You once knew me as a shape across the pond, Sabine.
#speaker: Sabine
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
{SpecialDeliveryQuestState == "FINISHED" && AGraveRevelationQuestState == "FINISHED" && ACatfishAndAGrarQuestState == "FINISHED" && BreakTheSealsQuestState == "FINISHED":
->APleadForHelp 
-else:
Hello there.
{shuffle:
- The grasses are still whispering your name. 
- The moon watches you fondly from amidst the stars. A speck blowing in the wind.
- The trees bristle in apprehension. They sense your unease.
}
What help are you seeking?
->StandardSabineBranching
}



=== StandardSabineBranching ===
+[Can I ask you Something?]
->SabineQuestions

 *{flowerPlaced == true}[I placed the flower.]
 ->PlacedFlower
 *{ HasItem("Item_ChannelCatfish", 1) && HasItem("Item_AlligatorGar", 1) && BaptistesFued && !ACatfishAndAGrar}[I have the Catfish and the Grar.]->ACatfishAndAGrar
 
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
 *{marie_known != "" && olivier_known != ""}[Do you know the Baptistes?]
 ->BaptistesExplanation
 
 *{BaptistesExplanation && marie_known != "" && olivier_known != "" && sabinequestionsasked >=2}[What would you do about the Baptistes?]
 ~sabinequestionsasked = sabinequestionsasked + 1
 ->BaptistesFued
 
 *{PlacedFlower}[You called me Mr. Lafleur?]
 ~sabinequestionsasked = sabinequestionsasked + 1
 ->MrLafleur
 *{sabinequestionsasked >=3}[What have you lost?]
 ~sabinequestionsasked = sabinequestionsasked + 1
 ->SabineLost
 
 +{sabinequestionsasked >=4 && TheseRitualComponentsSeemSlipperyInProgress}[What have I lost?]
 ->Who
 
 
 +[That's all for now.]
 ->goodbye
 
=== BaptistesExplanation
A pair destined for eternity but is eternity destined for them?
They need to listen to the unspoken storm brewing between them before it consumes them both.
...
Don't let yourself be consumed by their storm.
->END
 
===BaptistesFued
What do you mean?
+[They're at their wits end...]
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
...
Stay safe out there.
~StartQuest(SpecialDeliveryQuestId)
->END

===SabineCalisteRelationship
{SabineCalisteRelationship == 9: You really are a curious one, aren't you?}
{SpecialDeliveryQuestState != "FINISHED": Honestly, I do not know.}
{SpecialDeliveryQuestState != "FINISHED": Would you be able to understand a riddle spoken by the wind if that wind no longer blows?}
{SpecialDeliveryQuestState != "FINISHED": Stay Safe...}
{SpecialDeliveryQuestState == "FINISHED" && SharedDinnerQuestState != "FINISHED": Thanks to you, I saw them again and they saw me too... they saw me too.}
{SpecialDeliveryQuestState == "FINISHED" && SharedDinnerQuestState != "FINISHED": It's as if the tree we grew together so long ago has dropped a new seed.}
{SpecialDeliveryQuestState == "FINISHED" && SharedDinnerQuestState != "FINISHED": I only hope this seed can grow to rival what came before.}
{SharedDinnerQuestState == "FINISHED": They're better than they have been for three years.}
{SharedDinnerQuestState == "FINISHED": Little by little her acts of kindness will help set up new roots for us.}
{SharedDinnerQuestState == "FINISHED": While these roots cannot anchor them they will have to do. }
...
Thank you, stay safe out in the maze of trees.
->END

===SabineLost
What haven't I lost? 
They slipped away into the mist after I warned them against the silver tongued dec...
...
Seeing them again was worth my binding...
...
Don't let the mist take you too.
->END

===Who
Is that the right question to be asking?
Perhaps asking 'who' would be a better place to start.
Not that I can tell you more about them...
->END

===MrLafleur
...
Please stay safe out in the swirling waters.
->END


 ===DeliverFlower
 ~deliveredFlower = true
 ~StartQuest(AGraveRevelationQuestId)
oh my...
I cannot take a flower as delicate as that from her...
Please place it in a place more suited.
+[I can do so]
->DeliverFlowerToGrave

===DeliverFlowerToGrave
 Back in the dead's final home, you will find a grave near Lover's Nook with a cross full of beads.
 That would be a much better place to lay that flower to rest.
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
 +{HasItem("Item_RedSnapper", 1) && HasItem("Item_SailfinMolly", 1)}[I have.]->SpecialDeliveryDelivered
 +[I haven't.]
 ->needhelpYourself
 
 ===needhelpYourself
 Do you need anything else?
 ->StandardSabineBranching
 
 ===SpecialDeliveryDelivered
 ~FinishQuest(SpecialDeliveryQuestId)
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
 The moon sits high above the mortal realm. Yet its silver light still struggles to reach the Bayou's recesses. 
 Please, there is another way, all I need is a word of confirmation and the ritual can begin.
 ->PleadChoices
 ===PleadChoices
 +[I will help.]
  ~StartQuest(TheseRitualComponentsSeemSlipperyQuestId)
  ->Helpritual
  +[I don't want to help.]
  ->NoHelpritual
  *[What Ritual?]
  ->RitualExplanation
  +[I came for something else.]
  ->StandardSabineBranching
  
  ===RitualExplanation
  The seals are broken. We must show you the truth, for my tongue cannot utter it without...
  I cannot say more...
  So... what say you?
  ->PleadChoices
  
 === Helpritual
 I will need you to find three fish from across the Bayou.
 An Alligator Grar, the living nightmare from the Sunken Grove.
 A Largemouth Bass from the secluded graveyard pond. Look beyond that which fences you in by the Landry Tomb.
 A Blue Catfish from the Brackish Shore. Closer to the start of your journey than you may ever have realised.
Retrieve these three for me and you might just find what you've lost.
Your remaining light is but a flicker, I hope it will be enough...
 ->END
 
 ===NoHelpritual
If you do not join me on this path then everything you've yet to find will be suffocated by the dark.
I hope you change your mind.
 ->END
 
 === TheseRitualComponentsSeemSlipperyInProgress
 Hello there.
 Have you found the three scaley wonders? Or has the cold air frozen your progress?
 +{HasItem("Item_AlligatorGar", 1) && HasItem("Item_BlueCatfish", 1) && HasItem("Item_LargemouthBass", 1)}[I have all three.]
 ->Ritualcomplete
 +[I've forgot them.]
 ->UtmostImportance
 +[I'm here for something else.]
 ->StandardSabineBranching
 +[Stay safe Sabine.]
 ->goodbye
 
 ===Ritualcomplete
 Ah! The final ember will not need to disolve into a thread of smoke!
 Hand over the Three. 
 Oh and your Rosary too.
 ~FinishQuest(TheseRitualComponentsSeemSlipperyQuestId)
 +[My rosary?]->HandOverTheRosary
 
 ===HandOverTheRosary
 Yes, clensing it will remove that which has been clouding your mind in the moment it matters most. 
 ~StartQuest(ASecondWayOutQuestId)
 +[HandOverTheRosary]->FinishedRitual
 
 ===FinishedRitual
 ....
 Tshhhhhhhhh//Play a sound hear tied to ritual?
 ...
 Go now Fabian into the core of the Sunken Grove. There you will find what you have been searching for.
 ->END
 
 ===UtmostImportance
 Their importance dwarfs either one of us. Let me remind you of the three.
 An Alligator Grar, the living nightmare from the Sunken Grove.
 A Largemouth Bass from the secluded graveyard pond. Look beyond that which fences you in by the Landry Tomb.
 A Blue Catfish from the Brackish Shore. Closer to the start of your journey than you may ever have realised.
 Make haste but be careful, the Bayou is always listening.
 ->END
 
 ===ASecondWayOutInProgress
 {ASecondWayOutInProgress >= 5: ...->END}
 Every second you stay is a second it can draw in more strength.
 ...
 Go.
 ->END