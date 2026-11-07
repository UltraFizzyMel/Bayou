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

    - "IN_PROGRESS": -> PendingFlounder

    - "CAN_FINISH": -> PendingFlounder

    - "FINISHED": -> ExpandedHorizonsStart

    - else: -> firstmeeting

 } 
 
=== ExpandedHorizonsStart ===

 { ExpandedHorizonsQuestState :

 //   - "IN_PROGRESS": -> A

  //  - "CAN_FINISH": -> LanternFound

    - "FINISHED": -> AcordianDisposalStart

    - else: -> MarieStandardIntro

 } 

=== AcordianDisposalStart ===
 
{ AcordianDisposalQuestState :

 //   - "IN_PROGRESS": -> A

  //  - "CAN_FINISH": -> LanternFound

    - "FINISHED": -> AMealToDieForStart

    - else: -> MarieStandardIntro

 } 
 
 === AMealToDieForStart ===
 { AMealToDieForQuestState :

  //  - "IN_PROGRESS": -> A

  //  - "CAN_FINISH": -> LanternFound

    - "FINISHED": -> DeadAliveOrHappy

    - else: -> MarieStandardIntro

 } 

=== questions ===
+[Who are you?]
 -> goodbye
+[Chat Later]
 -> goodbye
 
 === firstmeeting ===
{ marie_firstmeeting:

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
 +[I have.]
 -> AFavourIntroduction
+[I haven't]
 -> ChangedMind
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

===AFavourIntroduction ===
Excellent dear! 
->ASimpleFavour

=== ASimpleFavour ===
Now you see, I have been living in squalor for far too long. 
Mr Baptiste simply refuses to put in the effort of securing and preparing one of my favourite dishes. 
Instead, he opts to catch his own preferences. Thus, all I ask is that you catch me two flounders. Simple Really.
I trust I can count on you?
+[Sure thing.]
~StartQuest(PaymentForARideQuestId)
 -> AcceptedFirstFavour
 +[I can't help right now.]
 -> ChangedMind
+{HasItem("Item_Flounder", 2)}[I have some right here.]
-> HandOverFlounders
~StartQuest(PaymentForARideQuestId)


=== PendingFlounder
Do you have the flounders for me yet???
+[Not Yet.]
->WastedTime
+{HasItem("Item_Flounder", 2)}[I have them right here.]
-> HandOverFlounders

=== WastedTime
Do not waste my time then dear.
->END

=== AcceptedFirstFavour
Thank you dear! 
...
What are you waiting for? Run along now, don’t keep me waiting.
->END

=== HandOverFlounders
Fabulous! Look at you go. As Promised you will be rewarded.
Should you ever need passage down to the Brackish Shore Church Entrance then we shall take you all the way there for the price of a single fish.

~ FinishQuest(PaymentForARideQuestId)

->MarieStandardBranching


=== MarieStandardIntro
Well, look who it is! Good Evening dear.
->MarieStandardBranching

=== MarieStandardBranching ===
What can I do for you?
+[I have a question.]
->MarieQuestions
+[I need a Ride.]
->BoatLocations
+[Not at this moment.]
->WastedTime

===MarieQuestions
Ask away, just make it quick
+[Who are you?]
->Name

=== Name 
Well if you must know, I'm Mrs Baptiste. 
->MarieStandardBranching

=== BoatLocations
Well... spit it out, where to?
{ is_in_foggy_marsh: 
+[Near the Church]
-> ChurchTrip
+[Near the Sunken Grove]
-> GroveTrip
-else: 
-> BoatLocations2
}

=== BoatLocations2
{is_at_first_brackishshore:
+[To the Foggy Marsh]
-> MarshTrip
+[Near the Sunken Grove]
-> GroveTrip
-else:
+[Near the Church]
-> ChurchTrip
+[To the Foggy Marsh]
-> MarshTrip
}

=== ChurchTrip
->END

//Takes player to brackish shore location closest to the Sunken Grove
=== GroveTrip
->END

//Takes player to foggy Marsh Location
===MarshTrip
->END

=== ExpandedHorizons ===

I have another task for you. It should not be too hard for someone like you.
Someone like me?
What’s the task?

Why yes! You with your big arms and those trusty fishing doodads of yours. 
I have not met anyone more capable of providing exactly what I am asking for?
What would that be?
Only two crabs and two crawfish. They’d be enough for a wonderful crab pot pie or crustacean stew! Don’t worry I’ll make it worth your while. I can add an extra stop closer to the sunken grove entrance after you’ve helped me out.
Thank you dear! I’ll be seeing you.
->END

=== ExpandedHorizonsInProgress
Have you procured the crustaceons yet?!
+[Not yet...]
->WastedTime
+[I came for something else]
->Fine
+{HasItem("Item_Crab", 1) && HasItem("Item_Crawfish", 1)}[I have.]
-> HandOverCrustaceons
~StartQuest(ExpandedHorizonsQuestId)

=== Fine ===
...
Alright...
->MarieStandardBranching


===HandOverCrustaceons

~FinishQuest(ExpandedHorizonsQuestId)


Accordian Disposal:


Since you’re in the area, I have another request for you
->END


===DeadAliveOrHappy ===
->END
 
 === goodbye ===
 until next time my friend.
 -> END