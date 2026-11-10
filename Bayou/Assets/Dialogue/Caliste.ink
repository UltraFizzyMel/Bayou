 INCLUDE globals.ink



 EXTERNAL OpenShop()

 EXTERNAL StartQuest(questId)

 EXTERNAL AdvanceQuest(questId)

 EXTERNAL FinishQuest(questId)

 EXTERNAL HasItem(itemId, count)

 EXTERNAL HandOverItem(itemId, count)

 EXTERNAL GiveMoney(amount)



#portrait: caliste_ardoin_neutral #layout: left #background: merchant
{caliste_known == "":-> notKnown |-> knownName}


=== notKnown ===
->SnapperAndMollyQuestStart

=== knownName ===
#speaker: Caliste
->SnapperAndMollyQuestStart



=== SnapperAndMollyQuestStart ===

#portrait: caliste_ardoin_neutral #layout: left #background: merchant

{ SnapperAndMollyQuestState :

    - "FINISHED":  {caliste_known == "":-> notKnown |-> knownName}

    - "IN_PROGRESS": -> waiting

    - "CAN_FINISH": -> waiting

    - else: -> calisteFirstMeeting

}

===ASearchForAHerbalistQuestStart
{ ASearchForAHerbalistQuestState :

    - "FINISHED":  ->SharedDinnerQuestStart

    - "IN_PROGRESS": -> ASearchForAHerbalistInProgress

    - "CAN_FINISH": -> ASearchForAHerbalistInProgress

    - else: -> CalisteStandardIntroduction

}

===SharedDinnerQuestStart
{ SharedDinnerQuestState:

    - "FINISHED":  {caliste_known == "":-> notKnown |-> knownName}

    - "IN_PROGRESS": -> waiting

    - "CAN_FINISH": -> waiting

    - else: -> CalisteStandardIntroduction

}

===LostCargoQuestStart
{ LostCargoQuestState:

    - "FINISHED":  {caliste_known == "":-> notKnown |-> knownName}

    - "IN_PROGRESS": -> waiting

    - "CAN_FINISH": -> waiting

    - else: -> CalisteStandardIntroduction

}

===CalisteStandardIntroduction
<b>DON'T...</b> Oh it's you.
-> BasicWaresIntro
-> ASearchForAHerbalist


===BasicWaresIntro
You want to see my wares?
->questions


=== calisteFirstMeeting ===

<b>DON'T COME ANY CLOSER!!!</b>

-> calm



=== calm ===

+ {HasItem("Item_RedSnapper", 1) && HasItem("Item_SailfinMolly", 1)&& speak} [I have a Snapper and a Molly.] -> DeliveredItem

+[I just want to talk.]

-> speak

+[I'll give you your space.]->END



=== speak ===

~ StartQuest("SnapperAndMollyQuest")

Talk...

You seem...

Fetch me a Snapper and a Molly, then I'll talk.

My wares may be of interest...

-> END



=== waiting ===

{ HasItem("Item_RedSnapper", 1) && HasItem("Item_SailfinMolly", 1):

    You're back. Did you bring them?

    + [I have a Snapper and a Molly.] -> DeliveredItem

    + [Not yet.]

        Fetch me a Snapper and a Molly, then I'll talk.

        -> END

- else:

    Fetch me a Snapper and a Molly, then I'll talk.

    My wares may be of interest...

    -> END

}



=== DeliveredItem ===

~ StartQuest(SnapperAndMollyQuestId)

~ temp handedSnapper = HandOverItem("Item_RedSnapper", 1)

~ temp handedMolly = HandOverItem("Item_SailfinMolly", 1)

{ handedSnapper && handedMolly:

    ~ FinishQuest(SnapperAndMollyQuestId)

    ~ GiveMoney(150)

    You might just be the real deal sonny.

    I'll let you peek at my wares.

    -> questions

- else:

    Hmm. Bring me one Snapper and one Molly.

    -> END

}



=== questions ===

+[I have a question for you.]
->BranchingQuestions

+[Wares?]
 -> shop

+[Chat Later.]

 -> goodbye

===BranchingQuestions

+[Who are you?]
{caliste_known == "":-> introduction("Caliste") | -> repeatName}

=== introduction(name) ===

 ~ caliste_known = name

 <color=\#F8FF30>Caliste.</color> #speaker: Caliste

 +[Ah...]

 -> questions



=== repeatName ===

#speaker: Caliste

 Tsk...

 ->questions



=== shop ===

 Take a look, but don't touch what you can't pay for.
//Maybe have a variable that checks what items Calsite can still sell?
 I've got a proper rod... and a key for the maze gate beside me.

 ~ OpenShop()

 -> END


===ASearchForAHerbalist
Sorry sonny, have you a minute to spare?
+[I do.]
->ASearchForAHerbalistStart
+[I don't.]
->refusetohelp


===ASearchForAHerbalistStart
A memory came to me.
Green flames amidst the trees, leading deeper into the fog.
A Herbalist , missing a flower needed to keep it at bay.
A flower...
Please Sonny, take this and find them for me.
+[I will find them.]
->AcceptedHerbalistQuest
+[Keep what at bay?]
->KeptAtBay

===AcceptedHerbalistQuest
Thank you, Please keep this flower safe.
Anything you need before you go?
~ StartQuest(ASearchForAHerbalistQuestId)
->questions

=== KeptAtBay
An evil older than you, casting its shadow over this here Bayou...
So please, this feels important.
+[I will find the Herbalist.]
->AcceptedHerbalistQuest

===refusetohelp
Ah... that's alright.
->BasicWaresIntro

===ASearchForAHerbalistInProgress
<b>DON'T...</b> Oh it's you.
Did you find them?
+{deliveredFlower}[I did.]
->SatchelReward
+[I have not.]
->StillSearching

===SatchelReward
Well, look at that. You were trustworthy afterall.
Hand over that patchy rucksack of yours. We gonna fix it up some extra space.
~ FinishQuest(ASearchForAHerbalistQuestId)
There you go sonny boy!
Since you're here...
->BasicWaresIntro

===StillSearching
I have a feeling you'll find them sooner than you know.
->BasicWaresIntro

=== goodbye ===
#speaker: Caliste

 I'll be seeing you.

 -> END
