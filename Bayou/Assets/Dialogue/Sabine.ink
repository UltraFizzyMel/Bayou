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
-> questions

=== main ===
-> questions

 //=== EmergencyDinnerStart ===

//{ EmergencyDinnerQuestState :

  //  - "IN_PROGRESS": -> EmergencyDinnerInProgress

   // - "CAN_FINISH": -> EmergencyDinnerInProgress

   // - "FINISHED": -> LostAccordionStart

   // - else: -> firstmeeting

// } 
 
//=== LostAccordionStart ===

 //{ LostAccordionQuestState :

   // - "IN_PROGRESS": -> LostAccordionInProgress

   // - "CAN_FINISH": -> LostAccordionInProgress

   // - "FINISHED": -> AMealToDieForOlivierStart

    //- else: -> OlivierStandardIntro

 } 

//=== AMealToDieForOlivierStart ===
 
//{ AMealToDieForOlivierQuestState :

  //  - "IN_PROGRESS": -> AccordianDisposalInProgress

    //- "CAN_FINISH": -> AccordianDisposalInProgress

    //- "FINISHED": -> AMealToDieForStart

   // - else: -> OlivierStandardIntro

// } 


=== questions ===
+[Who are you?]
 {olivier_known == "":-> introduction("Olivier Baptiste") | -> repeatName}
+{not knownName}[Tunes?]
 -> music
 +{knownName}[Let me hear your Tunes!]
 -> music
+[Chat Later]
 -> goodbye
 
 === introduction(name) ===
 ~ olivier_known = name
 I'm the one and only <color=\#F8FF30>Olivier Baptiste!</color> 
 However, that'll be <color=\#F8FF30>Mr.Baptiste</color> to you.#speaker: Sabine
 -> questions
 
 === repeatName ===
 Forget me already?
 ->questions
 
 === music ===
 Well, ain't it kind of you to ask.
 Listen closely youngun.(INSERT MUSIC PLAYING HERE)
 +[I should get going.]
 -> questions
 === goodbye ===
 until next time my friend.
 -> END