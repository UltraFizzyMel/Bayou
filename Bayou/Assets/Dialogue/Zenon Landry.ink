 INCLUDE globals.ink

 EXTERNAL StartQuest(questId)

 EXTERNAL AdvanceQuest(questId)

 EXTERNAL FinishQuest(questId)

 EXTERNAL HasItem(itemId, count)

 EXTERNAL HandOverItem(itemId, count)

 EXTERNAL GiveItem(itemId, count)

 EXTERNAL GrantKey(flagName)



 #portrait: father_landry_neutral #layout: left #background: church #border:Border

->collectPondItemStart



=== collectPondItemStart ===

#portrait: father_landry_neutral #layout: left #background: church

{ CollectPondItemQuestState :

    - "IN_PROGRESS": -> DeliverItem

    - "CAN_FINISH": -> DeliverItem

    - "FINISHED":  ->CollectLanternStart

    - else: -> RosaryOffer

 }



=== RosaryOffer ===

{ first_meeting:

    ~ first_meeting = false

    Well now… I didn't expect to find a single soul out here after sundown.

    Perhaps you are looking for something or someone long lost?

    +[I can't leave...]

        -> RosaryAsk

    +[I can't remember...]

        -> RosaryAsk

- else:

    -> RosaryAsk

}



=== RosaryAsk ===

Oh my how... unfortunate. It seems the bayou has taken its toll.

No-one can walk into these waters unscathed.

I might be able to help you if you return the favour.

Find the rosary in the church pond. You'll know it when you see it.

+ {HasItem("Item_RosaryNecklace", 1)} [Do you mean this?]

    ~ StartQuest(CollectPondItemQuestId)

    ->DeliveredItem

+[Right away.]

    ~StartQuest(CollectPondItemQuestId)

    ->END



=== CollectLanternStart ===

{ CollectLanternQuestState :

    - "IN_PROGRESS": -> LanternReminder

    - "CAN_FINISH": -> LanternFound

    - "FINISHED": -> BreakTheSealsStart

    - else: -> Agree

 }
 
 === BreakTheSealsStart ===

{ BreakTheSealsQuestState :

    - "IN_PROGRESS": -> BreakTheSealsInProgress

    - "CAN_FINISH": -> BreakTheSealsInProgress

    - "FINISHED": -> PurgeTheWitchStart

    - else: -> AfterLantern

 }
 
  === PurgeTheWitchStart ===

{ DestroyTheWitchQuestState :

    - "IN_PROGRESS": -> PurgeTheWitchInProgress

    - "CAN_FINISH": -> PurgeTheWitchInProgress

    - "FINISHED": -> AfterTheWitch

    - else: -> OfferPurge

 }

=== OfferPurge ===
The seals are open. What hunted this bayou is not gone.
The last of it waits at the altar in the Sunken Grove.
+[I will purge the witch.]
~ StartQuest(DestroyTheWitchQuestId)
Go to that altar. When the hunt is done, come back to me.
-> END
+[Not yet.]
Come back when you are ready.
-> END



=== LanternReminder ===

The lantern waits past Caliste's maze gate — north-east through the foggy graves.

My graveyard key will not open her gate.

Caliste sells the key to that maze once she trusts you.

Buy the Foggy Marsh Key from Caliste once she trusts you.

Be careful out there.

-> END


=== LanternFound ===

{ HasItem("Item_Lantern", 1):

    ~ FinishQuest(CollectLanternQuestId)
    ~ GiveItem("Item_ChurchFoggyMarshKey", 1)
    ~ GrantKey("hasKeyChurchToFoggyMarsh")

    The lantern... you found it.

    Well done, my child!
    Now you are ready to brave the fog beyond the next church gate.
    Oh! Before you go. I have an important task to entrust you with.
    ~ StartQuest(BreakTheSealsQuestId)
    There are three altars scattered around the Bayou.
    Each needs to be burned to help you reclaim what you have lost.
    Once you burn them all return to me and I can give you the power you need to reach the Sunken Grove.

    -> END

- else:

    -> LanternReminder

}

=== AfterLantern ===

#speaker: Father Landry

It's good to see you again.

-> service


===DeliverItem

Welcome back!

Did you find it?

    +[Not Yet…] -> UnDeliveredItem

    +{HasItem("Item_RosaryNecklace", 1)}[I have.] ->DeliveredItem


===UnDeliveredItem

Come back as soon as you have it. ->END


=== DeliveredItem ===

~ temp handed = HandOverItem("Item_RosaryNecklace", 1)

{ handed:

    ~ FinishQuest(CollectPondItemQuestId)

    ~ GiveItem("Item_Cross", 1)

    Excellent! Teach a man to fish and they'll fish indeed.

    Take this cross.

    As long as you wear it, you'll be protected from that which lurks in the Bayou's shadows.

    ->Agree

- else:

    Hmm… I don't see the rosary on you. Come back when you have it.

    -> END

}


=== Agree ===

Agree to do a little favor for me and it's all yours.

+[Of Course!]

    ->Agreed

+[Maybe another time?]

    Come back when you're ready.

    -> END


===Agreed ===

Excellent! To venture into the bayou's depths, you'll need to first fetch a lantern for me.

~StartQuest(CollectLanternQuestId)

Take this key, it will unlock the NorthWest gate to the graveyard. What better place to do some soul searching?

~ GiveItem("Item_ChurchGraveyardKey", 1)

~ GrantKey("hasKeyChurchToGraveyard")

Be careful out there. Not everything is as it seems… Your eyes and ears will be your greatest deceivers.

->END


=== knownName ===

#speaker: Father Landry

It's good to see you again.
-> service


=== service ===

How might I be of service?
->questions


=== questions ===

+{!repeatName}[Who are you?]
 { priest_name == "":-> introduction("Landry") | -> repeatName}

+{!location}[Where am I?]
 -> location
 
*{caliste_known != ""}[Do you know Caliste?]
->ZenonCaliste
*{olivier_known != "" && marie_known != ""}[Thoughts on the Baptistes?]
->ZenonBaptistes
*{landryquestionsasked >=8}[You have a grave?]
->ZenonGrave

+[Chat Later]
 -> goodbye


 === introduction(name) ===

 ~ priest_name = name
 The name is <color=\#F8FF30>Father Landry.</color> #speaker: Father Landry
 -> questions



 === repeatName ===
 Call me Landry...
 ~landryquestionsasked = landryquestionsasked + 1
 ->questions

 === location ===
 The Bayou... You must have hit your head hard.
 ~landryquestionsasked = landryquestionsasked + 1
 -> questions

===ZenonCaliste
~landryquestionsasked = landryquestionsasked + 1
She chose to make a trade in order to become the best trader...
However she lost something which could never be replaced.
->END

===ZenonBaptistes
~landryquestionsasked = landryquestionsasked + 1
An unlikely pair you'd think. They used to be much happier.
How long until Marie's deal for eternity becomes her own worst nightmare?
How long will Olivier continue to play music that is the best in the eyes of everyone except her?
->END

===ZenonGrave
~landryquestionsasked = landryquestionsasked + 1
...
You best forget you saw it.
->END

 === goodbye ===
 until next time my friend.

 -> END
 
 === BreakTheSealsInProgress
 { BreakTheSealsQuestState == "CAN_FINISH":
    The altars are burned. This light is the power you need to reach the Sunken Grove.
    ~ FinishQuest(BreakTheSealsQuestId)
    -> END
 - else:
    Three altars. Burn each one, then return to me.
    -> END
 }
 
 === PurgeTheWitchInProgress
 { DestroyTheWitchQuestState == "CAN_FINISH":
    You faced what waited at the final altar.
    ~ FinishQuest(DestroyTheWitchQuestId)
    The witch's hold on this bayou is broken.
    -> END
 - else:
    The final altar is in the Sunken Grove. Return to me after you have been there.
    -> END
 }
 
 ===AfterTheWitch 
 The shadow will not rule this bayou the way it did.
 -> service
 
 ===AfterTheSeals
 -> OfferPurge
