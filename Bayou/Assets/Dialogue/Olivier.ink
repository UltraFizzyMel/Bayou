 INCLUDE globals.ink
  
 #portrait: olivier_baptiste_neutral #layout: left #background: olivier
 {olivier_known == "":-> main |-> knownName}
 
 === knownName ===
#speaker: Mr. Baptiste
-> EmergencyDinnerStart

=== main ===
-> EmergencyDinnerStart
 
 === EmergencyDinnerStart ===

{ EmergencyDinnerQuestState :

    - "IN_PROGRESS": -> EmergencyDinnerInProgress

    - "CAN_FINISH": -> EmergencyDinnerInProgress

    - "FINISHED": -> LostAccordionStart

    - else: -> firstmeeting

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

    - "FINISHED": -> AMealToDieForStart

    - else: -> OlivierStandardIntro

 } 
 
=== firstmeeting
{ olivier_firstmeeting:
~olivier_firstmeeting = false
Look at what the wind's blown in. Come on in, dry yourself off a bit!
Who might you be?
+[I don't know]
->Philosophical
 -else:
 Hello again!
 Have you decided to help me friend?!
 +[I will help.]
 -> AgreeToCatch
+[I haven't]
 -> Refusal
 +{HasItem("Item_Grar", 2)}[I have the Grar.]
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
+{HasItem("Item_Grar", 2)}[I have some right here.]
->HandOverGrar

=== AgreeToCatch ===
Thank you friend!
Please hurry, I cannot keep Marie waiting.
~StartQuest(EmergencyDinnerQuestId)
->END

=== Refusal ===
Ah... That's a shame, please reconsider...
If you ever change your mind please let me know.
->END

=== EmergencyDinnerInProgress
Hello again!
My, my, have you caught the Grar already?
+{HasItem("Item_Grar", 2)}[I have them right here.]
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
Stay safe out in the fog my friend.
~EndQuest(EmergencyDinnerQuestId)
->END

=== OlivierStandardIntro
Good Evening my friend!
You look in need of a touch of musical delight, alright!
How can I help ya?
->OlivierOptions

->LostAccordion



===OlivierOptions
+[Can I ask you something?]
->questions
+{is_marie_alive = false}[Could you give me a ride?]
->OlivierBoatLocations
+{HasItem("Item_Accordion", 1)}[I have your accordion...]
+[It was great seeing you!]
->goodbye


=== questions ===
+[Who are you?]
 {olivier_known == "":-> introduction("Olivier Baptiste") | -> repeatName}
+[Chat Later]
 -> goodbye
 
 === introduction(name) ===
 ~ olivier_known = name
 Oh right!! How rude of me, I'm the one and only <color=\#F8FF30>Olivier Baptiste!</color> 
 However, that'll be <color=\#F8FF30>Mr.Baptiste</color> to you.#speaker: Mr. Baptiste
 -> questions
 
 === repeatName ===
 Forget me already?
 ->questions
 
 
 === OlivierBoatLocations
Where you needa go?
{ is_in_foggy_marsh: 
+[Near the Church]
-> OlivierChurchTrip
+[Near the Sunken Grove]
-> OlivierGroveTrip
-else: 
-> OliverBoatLocations2
}

=== OlivierBoatLocations2
{is_at_first_brackishshore:
+[To the Foggy Marsh]
-> OlivierMarshTrip
+[Near the Sunken Grove]
-> GroveTrip
-else:
+[Near the Church]
-> ChurchTrip
+[To the Foggy Marsh]
-> MarshTrip
}

=== OlivierChurchTrip
->END

//Takes player to brackish shore location closest to the Sunken Grove
=== OlivierGroveTrip
->END

//Takes player to foggy Marsh Location
===OlivierMarshTrip
->END
 
=== goodbye
Likewise my friend!
Don't let the bayou take ya, I still have some tunes to share with you!
->END
