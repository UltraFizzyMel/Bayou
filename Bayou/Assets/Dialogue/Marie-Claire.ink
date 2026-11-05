INCLUDE globals.ink


EXTERNAL StartQuest(questId)

EXTERNAL AdvanceQuest(questId)

EXTERNAL FinishQuest(questId)

EXTERNAL HasItem(itemId, count)

EXTERNAL HandOverItem(itemId, count)
  
 #portrait: marie_claire_baptiste_neutral #layout: left #background: houseboatBG
 {marie_known == "":-> main |-> knownName}
 
 === knownName ===
#speaker: Mrs. Baptiste
-> PaymentForARideStart

=== main ===
Who might you be?
Don't gotta answer, wanna hear some of my tunes?
-> PaymentForARideStart
 
=== PaymentForARideStart ===

{ PaymentForARideQuestState :

    - "IN_PROGRESS": -> A

    - "CAN_FINISH": -> LanternFound

    - "FINISHED": -> ExpandedHorizonsStart

    - else: -> firstmeeting

 } 
 
=== ExpandedHorizonsStart ===

 { ExpandedHorizonsQuestState :

    - "IN_PROGRESS": -> A

    - "CAN_FINISH": -> LanternFound

    - "FINISHED": -> AcordianDisposalStart

    - else: -> firstmeeting

 } 

=== AcordianDisposalStart ===
 
{ AcordianDisposalQuestState :

    - "IN_PROGRESS": -> A

    - "CAN_FINISH": -> LanternFound

    - "FINISHED": -> AMealToDieForStart

    - else: -> firstmeeting

 } 
 
 === AMealToDieForStart ===
 { AcordianDisposalQuestState :

    - "IN_PROGRESS": -> A

    - "CAN_FINISH": -> LanternFound

    - "FINISHED": -> AfterLantern

    - else: -> firstmeeting

 } 

=== questions ===
+[Who are you?]
 -> goodbye
+[Chat Later]
 -> goodbye
 
 === firstmeeting ===
{ marie_firstmeetingfirstmeeting:

    ~ marie_firstmeeting = false
My, my, who do we have here?
You look capable enough, at least more capable than him.
Care to do me a favour?
+[I don’t remember my name.]
 -> Rhetorical
+[Sure, what’s the favour?]
 -> ASimpleFavour
 -else:
 You had a change of heart?
}

=== Rhetorical
Oh... that first question was rhetorical dear, I do not care about something as mundane as a name. 
All I care about is if you can do something for me. I promise it will be worth the effort.
+[What’s the favour?]
 -> ASimpleFavour


=== ChangedMind ===
How... unfortunate...
Well, should you ever change your mind you know where to find me dear.
-> END

=== ASimpleFavour ===

Well you see, I have been living in squalor for far too long. 
Mr Baptiste simply refuses to put in the effort of securing and preparing one of my favourite dishes. 
Instead, he opts to catch his own preferences. Thus, all I ask is that you catch me two flounders. Simple Really.
I trust I can count on you?
+[Sure thing.]
~StartQuest(CollectPondItemQuestId)
 -> ASimpleFavour
 +[I can't help right now.]
 -> ChangedMind
+{HasItem("Item_Flounder", 2)}[I have some right here.]
-> HandOverFlounders
~StartQuest(CollectPondItemQuestId)

Run along now, don’t keep me waiting

I’ll be back
Wait, Who are you?



Expanded Horizons:

I have another task for you. It should not be too hard for someone like you.
Someone like me?
What’s the task?

Why yes! You with your big arms and those trusty fishing doodads of yours. 
I have not met anyone more capable of providing exactly what I am asking for?
What would that be?
Only two crabs and two crawfish. They’d be enough for a wonderful crab pot pie or crustacean stew! Don’t worry I’ll make it worth your while. I can add an extra stop closer to the sunken grove entrance after you’ve helped me out.
Thank you dear! I’ll be seeing you.

Accordian Disposal:


Since you’re in the area, I have another request for you




 
 === goodbye ===
 until next time my friend.
 -> END