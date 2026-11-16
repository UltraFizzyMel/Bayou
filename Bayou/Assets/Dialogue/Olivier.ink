 INCLUDE globals.ink
 
 EXTERNAL StartQuest(questId)

 EXTERNAL AdvanceQuest(questId)

 EXTERNAL FinishQuest(questId)

 EXTERNAL HasItem(itemId, count)

 EXTERNAL HandOverItem(itemId, count)

 EXTERNAL CanTakeBoat(destination)

 EXTERNAL TakeBoat(destination)
 

  
 #portrait: olivier_baptiste_neutral #layout: left #background: olivier
 {olivier_known == "":-> main |-> knownName}
 
 === knownName ===
#speaker: Mr. Baptiste
~Sad = false
-> EmergencyDinnerStart

=== main ===
~Sad = false
-> EmergencyDinnerStart
 
 === EmergencyDinnerStart ===

{ EmergencyDinnerQuestState :

    - "IN_PROGRESS": -> EmergencyDinnerInProgress

    - "CAN_FINISH": -> EmergencyDinnerInProgress

    - "FINISHED": -> LostAccordionStart

    - else: -> firstmeetingolivier

 } 
 
=== LostAccordionStart ===

 { LostAccordionQuestState :

    - "IN_PROGRESS": -> LostAccordionInProgress

    - "CAN_FINISH": -> LostAccordionInProgress

    - "FINISHED": -> AMealToDieForOlivierStart

    - else: -> OlivierStandardIntro

 } 

=== AMealToDieForOlivierStart ===
 
{ AMealToDieForOlivierQuestState :

    - "IN_PROGRESS": -> AMealToDieForOlivierInProgress

    - "CAN_FINISH": -> AMealToDieForOlivierInProgress

    - "FINISHED": -> OlivierDeadHappyOrAlive

    - else: -> OlivierStandardIntro

 } 
 
=== firstmeetingolivier
{ olivier_firstmeeting:
~olivier_firstmeeting = false
Look at what the wind's blown in. Come on in, dry yourself off a bit!
Who might you be?
+[I don't know.]
->Philosophical
 -else:
 Hello again!
 Have you decided to help me friend?!
 +[I will help.]
 -> AgreeToCatch
+[I haven't.]
 -> Refusal
 +{HasItem("Item_SpottedGar", 2)}[I have the Grar.]
->HandOverGrar
 }

=== Philosophical
Getting Philosophical are we?
Heh, I think I might like you yet.
Wanna be friends?
Don't answer that, I know you do.
Now, amoungst friends, I have a favour to ask...
You see, I may have forgotten to catch dinner and my wife will not be too pleased when they find out.
Could you catch me two Spotted Grar real quick?!
I have a key for any gates linking the Foggy Marsh and the Graveyard I could give ya in return?
+[Sure thing!]
->AgreeToCatch
+[I'd rather not.]
->Refusal
+{HasItem("Item_SpottedGar", 2)}[I have some right here.]
->HandOverGrar

=== AgreeToCatch ===
Thank you friend!
~StartQuest(EmergencyDinnerQuestId)
Please hurry, I cannot keep Marie waiting.
->END

=== Refusal ===
Please reconsider...
If you ever change your mind please let me know.
->END

=== EmergencyDinnerInProgress
Hello again!
My, my, have you caught the Grar already?
+{HasItem("Item_SpottedGar", 2)}[I have them right here.]
->HandOverGrar
+[I have not]
-> MissingGrar

===MissingGrar
That's not good for me...
Please hurry, I fear that these fish will be my last chance.
Stay safe out there friend.
->END

=== HandOverGrar
Thank you so much my friend!
Here's that key I promised ya!
Please visit whenever you wish to, I just need to prepare dinner real quick.
~FinishQuest(EmergencyDinnerQuestId)
Stay safe out in the fog my friend.
->END

=== OlivierStandardIntro
{!SeenAccordionExplanation && EmergencyDinnerQuestState == "FINISHED" && inBrackishShore == true: ->LostAccordion |-> ContinueIntro}

===ContinueIntro

{Sad:-> SadIntro | ->HappyIntro}
->OlivierOptions

=== SadIntro
Evening friend.
I'm afraid I don't have any tunes for you tonight, my accordion is lost yet again.
Still, how could I help you?
->OlivierOptions

===HappyIntro
Good Evening my friend!
You look in need of a touch of musical delight, alright!
How can I help ya?
->OlivierOptions

=== OlivierAfterEelIntro
->END

===OlivierOptions
+[Can I ask you something?]
->questions
+{!is_marie_alive}[Could you give me a ride?]
->OlivierBoatLocations
*{HasItem("Item_Accordion", 1) && OtherThing && !FoundAccordion}[I have your Accordion.]
->AMealToDieForOlivier
*{accordian_returned && !MarieYouSay}[About your accordion...]
->MarieYouSay
+[It was great seeing you!]
->goodbye


=== questions ===
+{!repeatNameVisited >= 1 && olivierquestionsasked <=3}[Who are you?]
 {olivier_known == "":-> introduction("Olivier Baptiste") | -> repeatName}

+{marie_known != ""}[How are things with Marie?]
->OlivierMarie

*{olivierquestionsasked >=2 && !SeenOlivierCaliste}[Do you know Caliste?]
->OlivierCaliste

*{!SeenOlivierLandry}[Do you know Father Landry?]
->OlivierLandry

*{sabine_known != "" && olivierquestionsasked >=3 && !SeenOlivierSabine}[What do you think of Sabine?]
->OlivierSabine

+{olivierquestionsasked >=4 && LostAccordionQuestState == "FINISHED"}[What have you lost?]
->OlivierLost
+[Chat Later.]
 -> goodbye
 

 
 ===OlivierMarie
 {not LostAccordionStart:I hope they're getting better but I have a feeling I have done something to annoy them...}
{not LostAccordionStart:I don't even have my music to help clear the tension between us.}
{LostAccordionQuestState == "FINISHED" && !AMealToDieForOlivierStart: Definitely on the upswing! We've had many big dinners thanks to you!}
{LostAccordionQuestState == "FINISHED" && !AMealToDieForOlivierStart: I'm going to serenade them more going forward to show them my love still reigns true.}
{AMealToDieForOlivierStart &&  not OlivierDeadHappyOrAlive:They're been... better but soon they won't have a lot to worry them anymore.}
{OlivierHappy:Ever since you worked your magic they've never been better. }
{OlivierHappy:Well... maybe on our wedding night...}
{OlivierHappy:Other than that night, never better!}
{OlivierAlive:They're... they've never been better...}
{OlivierAlive:I ran a bubble bath for them that definitly shocked them.}
{OlivierAlive:They've decided to step back from the limelight for the moment. Best leave them be.}
 {OlivierAlive:Wouldn't want to find yourself on their badside. Um...Anything else I could help you with?->OlivierOptions }
 ->EnoughOnMarie
 
 
 ===EnoughOnMarie
 Don't worry yourself too much about our situation, I have it handled from here my friend. 
 What can I help you with?
 ->OlivierOptions
 
 ===OlivierCaliste
 ~ olivierquestionsasked = olivierquestionsasked +1
 ~ SeenOlivierCaliste = true
 Hmmm... not well, it was a shame what happened to them. 
 They were definitly the best salesperson in these waters.
 -> AnythingElseOlivier
 
 ===OlivierLandry
 ~ olivierquestionsasked = olivierquestionsasked +1
 ~ SeenOlivierLandry = true
 Oh that Priest felt like trouble to me. I didn't like their face.
 To be fair it could just be the whole religion thing.
 Don't tell Marie I said that.
 -> AnythingElseOlivier
 
 ===OlivierSabine
 ~ olivierquestionsasked = olivierquestionsasked +1
 ~ SeenOlivierSabine = true
 Um... next question?
 ...
 Ok fine...
 I don't know them well.
 I occasionally see them through the fog. 
 You should be wary of anyone who has survived in the fog as long as her.
 I can't say more.
 -> OlivierOptions
 
 ===OlivierLost
 ~ olivierquestionsasked = olivierquestionsasked +1
 Wow... that's certainly a question friend.
 ...
 I've lost a bunch but gained a fortune in return.
 Being bound to this boat was a sacrifice worth making.
 ...
 I've...
 I've said too much.
 Come back when they're not listening.
 ->END
 
 ===AnythingElseOlivier
 Anything after that question?
 ->OlivierOptions
 
 === introduction(name) ===
 ~ olivier_known = name
 ~ olivierquestionsasked = olivierquestionsasked +1
 Oh right!! How rude of me, I'm the one and only <color=\#F8FF30>Olivier Baptiste!</color> 
 However, that'll be <color=\#F8FF30>Mr.Baptiste</color> to you.#speaker: Mr. Baptiste
 -> questions
 
 === repeatName ===
 ~ repeatNameVisited = true
 ~ olivierquestionsasked = olivierquestionsasked +1
 Forget me already?

 ->questions
 
 
 === OlivierBoatLocations
Where you needa go?
{ is_in_foggy_marsh: 
+[Near the Church]
-> OlivierChurchTrip
+{ExpandedHorizonsQuestState == "FINISHED"}[Near the Sunken Grove]
-> OlivierGroveTrip
-else: 
-> OlivierBoatLocations2
}

=== OlivierBoatLocations2
{is_at_first_brackishshore:
+[To the Foggy Marsh]
-> OlivierMarshTrip
+{ExpandedHorizonsQuestState == "FINISHED"}[Near the Sunken Grove]
-> OlivierGroveTrip
-else:
+[Near the Church]
-> OlivierChurchTrip
+[To the Foggy Marsh]
-> OlivierMarshTrip
}

=== OlivierChurchTrip
{CanTakeBoat("church"):
~ TakeBoat("church")
Enjoy your dip in the waters.
- else:
That passage is not open, or you are short the fish it costs.
}
->END

//Takes player to brackish shore location closest to the Sunken Grove
=== OlivierGroveTrip
{CanTakeBoat("grove"):
~ TakeBoat("grove")
Don't stray too far...
- else:
That passage is not open, or you are short the fish it costs.
}
->END

//Takes player to foggy Marsh Location
===OlivierMarshTrip
{CanTakeBoat("foggy"):
~ TakeBoat("foggy")
Watch out for the Gators!
- else:
That passage is not open, or you are short the fish it costs.
}
->END
 
=== goodbye
Likewise my friend!
Don't let the bayou take ya while I still have tunes to share. 
->END

===LostAccordion
Ah... Hello friend.
Sorry, I'm a little low spirits today.
How can I help ya?
+[What's wrong?]
->AccordionExplanation

=== AccordionExplanation
~SeenAccordionExplanation = true
Oh, well I seem to have misplaced my Accordion. 
I last remember having it while we were in the Foggy Marsh.
If you have spare time on your hands please it would mean the world to me if you returned it.
+[I will do my best]
-> ThankYouForAccepting
+{HasItem("Item_Accordion", 1)}[I have your Accordion.]
->FoundAccordion

=== ThankYouForAccepting
~StartQuest(LostAccordionQuestId)
Thank you my friend. 
...
Could I have a moment alone?
+[You may.]->END

===LostAccordionInProgress
Ah... Hello friend.
Sorry to bother but have you managed to find my Accordion?
+{HasItem("Item_Accordion", 1)}[Yes.]
->FoundAccordion
+[Not, yet.]
->NoAccordion
+[I came for something else.]
->OtherThing

=== FoundAccordion
I extend my deepest gratitude towards you. Thank you.
I have an extra satchel I can give you as thanks.
If you ever need anything, don't hesitate to ask.
~FinishQuest(LostAccordionQuestId)
Stay safe and don't swim too deep my friend.
->END

=== OtherThing
~Sad = true
Oh...
Um, How can I help you?
->OlivierOptions

=== NoAccordion
Let me know if you do find it please, stay safe out there friend.
->END


===AMealToDieForOlivier
Oh my! Where? where did you find my accordion?
~ accordian_returned = true
+[Mrs Baptiste gave it to me.]
->MarieYouSay
+[It was in the swamp again.]
->HowClumsy


===MarieYouSay
~SeenMarieYouSay = true
Oh... Marie gave the accordion to you...
That's...
That's good to know.
...
I have a request for you. If you're up for it. 
I can take over the workings of the boat to give you free rides if you complete it.
+[What's the request?]
->EelRequest

===EelRequest
I'm in need of three eels. That's all.
+[I'll help you get them.]
->AcceptedEels
+[What for?]
->AvoidingExplanation
+{HasItem("Item_ElectricEel", 3)}[I have three!]
->EelHandOver

===AvoidingExplanation
A little... project of mine.
I cannot say more yet my friend.
+[I'll Help you]
->AcceptedEels

=== AcceptedEels
Thank you friend...
~StartQuest(AMealToDieForOlivierQuestId)
Please return as soon as you have them.
->END

===HowClumsy
How clumsy of me, thank you again!
I'll make sure to keep my accordion by my side going forward.
Best of luck out in the Bayou youngun!
->END

=== AMealToDieForOlivierInProgress
Good Evening. Have you managed to get those eels?
+[I'll help you get them.]
->AcceptedEels
+[What for?]
->AvoidingExplanation
+{HasItem("Item_ElectricEel", 3)}[I have three!]
->EelHandOver
->END

===EelHandOver
Thank you. Should you need a ride in future, make sure to come to me.
Wouldn't want to burden Marie.
~is_marie_alive = false
~FinishQuest(AMealToDieForOlivierQuestId)
//Cancel the catfish and grar quest for sabine
->END

===OlivierDeadHappyOrAlive
{is_marie_alive == false}->OlivierAlive
{is_marie_alive == true}->OlivierHappy

===OlivierAlive
Evening my friend.
How can I help ya?
->OlivierOptions

===OlivierHappy
Good evening my friend!
~OlivierHappyVisited = OlivierHappyVisited +1
{OlivierHappyVisited ==1}I don't know what you did but things are looking up for me and Marie!
{OlivierHappyVisited ==1} If we're to spend eternity together, I can spend a little less time on my music...
{OlivierHappyVisited ==1} Anything I can do for ya?
{OlivierHappyVisited >1} You look in need of a touch of musical delight, alright!
{OlivierHappyVisited >1} How can I help ya?
->OlivierOptions


