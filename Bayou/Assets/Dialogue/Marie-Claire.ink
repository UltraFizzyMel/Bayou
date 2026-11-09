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

    - "IN_PROGRESS": -> ExpandedHorizonsInProgress

    - "CAN_FINISH": -> ExpandedHorizonsInProgress

    - "FINISHED": -> AccordianDisposalStart

    - else: -> MarieStandardIntro

 } 

=== AccordianDisposalStart ===
 
{ AccordianDisposalQuestState :

    - "IN_PROGRESS": -> AccordianDisposalInProgress

    - "CAN_FINISH": -> AccordianDisposalInProgress

    - "FINISHED": -> AMealToDieForStart

    - else: -> MarieStandardIntro

 } 
 
 
 
 === AMealToDieForStart ===
 { AMealToDieForQuestState :

    - "IN_PROGRESS": -> AMealToDieForInProgress

    - "CAN_FINISH": -> AMealToDieForInProgress

    - "FINISHED": -> DeadAliveOrHappy

    - else: -> MarieStandardIntro

 } 

 
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
Fabulous! Look at you go. As promised you will be rewarded.
Should you ever need passage down to the Brackish Shore Church Entrance then we shall take you all the way there for the price of a single fish.

~ FinishQuest(PaymentForARideQuestId)

->MarieStandardBranching


=== MarieStandardIntro
Well, look who it is! Good Evening dear.
->MarieStandardBranching

->ExpandedHorizons

->AccordianDisposal


=== MarieStandardBranching ===
What can I do for you?
+[I have a question.]
->MarieQuestions
+[I need a Ride.]
->BoatLocations
+[Nothing at this moment.]
->WastedTime

===MarieQuestions
Ask away, just make it quick.
->MarieBranchingQuestions

===MoreQuestions
Anything else dear?
->MarieBranchingQuestions

===MarieBranchingQuestions
*[Who are you?]
{mariequestionsasked + 1}
->Name
+ [How are things with Mr. Baptiste.]
->MrBaptiste
*[Have you seen the roaming shadow?]
{mariequestionsasked + 1}
->Shadow

*{mariequestionsasked >= 2}[Do you know Father Landry?]
{mariequestionsasked + 1}
->Landry

+[That'll be all]
->RunAlong

===MrBaptiste
...
->MoreQuestions

===RunAlong
Run along then dear.
->END

=== Name 
Well if you must know, I'm Mrs Baptiste. 
~ marie_known = "true"
->MarieStandardBranching

===Shadow
If you know what's good for you you won't ask me about them again.
*[but...]
->DONT

=== Landry
->END

=== DONT
Don't.
->MoreQuestions


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
+[Someone like me?]
->SomeoneLikeWho
+[What’s the task?]
->Crustaceons

=== SomeoneLikeWho ===
Why yes! You with your big arms and those trusty fishing doodads of yours. 
I have not met anyone more capable of providing exactly what I am asking for.
+[What would that be?]
->Crustaceons

=== Crustaceons
I just need you to retrieve two crabs and two crawfish. They’d be enough for a wonderful crab pot pie or crustacean stew! 
Don’t worry, I’ll make it worth your while. I can add an extra stop closer to the Sunken Grove entrance after you’ve helped me out.
~StartQuest(ExpandedHorizonsQuestId)
+[I'll help you]
->QuickGoodbye

===QuickGoodbye
Thank you dear! I’ll be seeing you.
->END

=== ExpandedHorizonsInProgress
Have you netted the crustaceons I asked for yet?!
+[Not yet...]
->WastedTime
+[I came for something else]
->Fine
+{HasItem("Item_Crab", 1) && HasItem("Item_Crawfish", 1)}[I have.]
-> HandOverCrustaceons


=== Fine ===
...
Alright...
->MarieStandardBranching


===HandOverCrustaceons
Bless your hands and your heart. Thank you dear. 
In future, just say the word and I will drop you off closer to the Sunken Grove.
Just know... I will not take any responsibility for what happens to you should you venture too deep.
~FinishQuest(ExpandedHorizonsQuestId)
I'll be seeing you dear.
->END

===AccordianDisposal
~ StartQuest(AccordianDisposalQuestId)
Since you’re in the area, I have another request for you.
I have not been able to to get a good nights rest ever since you returned his accordian... 
Olivier's incesant playing sounds to me like grinding gears, that was why I got rid of it in the first place you see. 
//player prompted to put accordian in inventory
All I ask is that you dispose of it, so that I never have to hear its awful noises ever again.
Come back once you're rid of it.
->END

===AccordianDisposalInProgress
Have you disposed of the squeezebox?!
+[Not yet...]
->WastedTime
+[I came for something else.]
->Fine
+{accordian_disposed == true}[I have.]
-> DisposedAccordion
+{accordian_returned == true}[I returned it.]
->HowDisappointing

===DisposedAccordion ===
~ FinishQuest(AccordianDisposalQuestId)
->END

===HowDisappointing ===
How... Disappointing...
I suppose it cannot be helped now...
Since you failed my last request, the least you could do is complete my next one. 
I assure you this will be the last thing I ask of you. 
Please bring me three pufferfish. I have an enticing dinner plan that requires them.
+[I don't know...]
->UncertainPuffer
+[Sure thing]
->AcceptedPuffer
+[I have some right here]
->HandOverPuffer

=== UncertainPuffer
Well I do know. 
You're taking this quest dear.
->AcceptedPuffer

=== AcceptedPuffer
~ StartQuest(AMealToDieForQuestId)
Return with the three puffers and you will never need to pay fish for a boat trip again. 
Goodluck.
->END

=== HandOverPuffer
{ HasItem("Item_Pufferfish", 3):
    ~ StartQuest(AMealToDieForQuestId)
    ~ FinishQuest(AMealToDieForQuestId)
    ~ is_olivier_alive = false
    ->END
- else:
    Three pufferfish, dear. Come back when you have them.
    ->END
}

===AMealToDieForInProgress
Do you have all three pufferfish?
+[Not yet...]
->WastedTime
+[I came for something else.]
->Fine
+{HasItem("Item_Pufferfish", 3)}[I have.]
-> HandOverPuffer


===DeadAliveOrHappy ===
->END
 
