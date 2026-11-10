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

    - "FINISHED": -> LostAccordionStart

    - else: -> SabineFirstmeeting

 }
 
//=== ACatfishAndAGrarStart ===

 //{ LostAccordionQuestState :

   // - "IN_PROGRESS": -> ACatfishAndAGrarInProgress

   // - "CAN_FINISH": -> ACatfishAndAGrarInProgress

   // - "FINISHED": -> AMealToDieForOlivierStart

    //- else: -> OlivierStandardIntro

// } 

//=== TheseRitualComponentsSeemSlipperyStart ===
 
//{ TheseRitualComponentsSeemSlipperyQuestState :

  //  - "IN_PROGRESS": -> TheseRitualComponentsSeemSlipperyInProgress

    //- "CAN_FINISH": -> TheseRitualComponentsSeemSlipperyInProgress

    //- "FINISHED": -> AMealToDieForStart

   // - else: -> OlivierStandardIntro

// } 
===SabineFirstmeeting
->END

===StandardSabineIntro
->StandardSabineBranching

=== StandardSabineBranching ===
+[Can I ask you Something?]
->SabineQuestions
+[Who are you?]
 {sabine_known == "":-> introduction("Sabine") | -> repeatName}
+{not knownName}[Tunes?]
 -> music
 
 +[I have something for you.]
 -> music
+[Chat Later]
 -> goodbye
 
 ===SabineQuestions
 ->END
 
 === introduction(name) ===
 ~ olivier_known = name
 I'm the one and only <color=\#F8FF30>Olivier Baptiste!</color> 
 However, that'll be <color=\#F8FF30>Mr.Baptiste</color> to you.#speaker: Sabine
 -> StandardSabineBranching
 
 === repeatName ===
 Forget me already?
 ->StandardSabineBranching
 
 === music ===
 Well, ain't it kind of you to ask.
 Listen closely youngun.(INSERT MUSIC PLAYING HERE)
 +[I should get going.]
 ->StandardSabineBranching
 
 
 
 === goodbye ===
 until next time my friend.
 -> END
 
 ===SpecialDeliveryInProgress
 ->END