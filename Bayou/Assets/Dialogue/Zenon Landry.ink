 INCLUDE globals.ink
 EXTERNAL StartQuest(questId)
 EXTERNAL AdvanceQuest(questId)
 EXTERNAL FinishQuest(questId)
 
 
 
 #portrait: father_landry_neutral #layout: left #background: church
->collectPondItemStart

=== collectPondItemStart ===
#portrait: father_landry_neutral #layout: left #background: church
{ CollectPondItemQuestState :
    - "CAN_START": 
     {first_meeting == true: ->FirstMeeting.Intro}
    
    - "IN_PROGRESS": -> DeliverItem
    - "CAN_FINISH": -> DeliverItem
    - "FINISHED":  {priest_name == "":-> service|-> knownName}
    - else: -> END
 }
 
 
 === FirstMeeting ===
 =Intro
 ~first_meeting = false
 Well now… I didn't expect to find a single soul out here after sundown.
 Perhaps you are looking for something or someone long lost?
 +[I can't leave...]
 -> FirstMeeting.Answer
 +[I can't remember...]
 -> FirstMeeting.Answer
 
 =Answer
 Oh my how... unfortunate. It seems the bayou has taken its toll.
 No-one can walk into these waters unscathed.
 I might be able to help you if you return the favour.
 Find me something shiny from the church pond. 
 You'll know it when you see it.
 + {CollectPondItemQuestState == "CAN_FINISH"} [Do you mean this?] ->DeliveredItem 
     ~FinishQuest("CollectPondItemQuest")
     ~CollectPondItemQuestState = "FINISHED"
 +[Right away.]
     ~StartQuest("CollectPondItemQuest")
     ~CollectPondItemQuestState = "IN_PROGRESS"
     ->END
 
 ===DeliverItem
 Welcome back!
 Did you find it?
    +[Not Yet…] -> UnDeliveredItem
    +{CollectPondItemQuestState == "CAN_FINISH"}[I have.] ->DeliveredItem
    ~FinishQuest("CollectPondItemQuest")
    ~CollectPondItemQuestState = "FINISHED"
 
 ===UnDeliveredItem
 Come back as soon as you have it. ->END
 
 === DeliveredItem ===
 Excellent! Teach a man to fish and they'll fish indeed.
 Take this cross. As long as you wear it, you'll be protected from that which lurks in the Bayou's shadows.
 ->Agree
 
 
 === Agree ===
 Agree to do a little favor for me and it's all yours.
 +[Of Course!]
 ->Agreed
 +{Agree}[Of Course?]
 ->Agreed
 +{Agree >= 2}[Of Course...]
 ->Agreed
 +{Agree <= 2}[Maybe another time?]
 ->Agree
 
 ===Agreed ===
 Excellent! To venture deeper into the bayou you'll need to first fetch a lantern for me…
 You'll find the gate to the NorthWest Graveyard unlocked. What better place to do some soul searching.
 Be careful out there. Not everthing is as it seems… Your eyes and ears will be your greatest deceivers.
 ->END
 
=== knownName ===
#speaker: Father Landry
It's good to see you again.
-> service

=== service ===
How might I be of service?
->questions

=== questions ===
+[Who are you?]
 { priest_name == "":-> introduction("Landry") | -> repeatName}
+[Where am I?]
 -> location
+[Chat Later]
 -> goodbye
 
 === introduction(name) ===
 ~ priest_name = name
 The name is <color=\#F8FF30>Father Landry.</color> #speaker: Father Landry
 -> questions
 
 === repeatName ===
 Call me Landry... 
 ->questions
 
 === location ===
 The Bayou... You must have hit your head hard.
 -> questions
 
 === goodbye ===
 until next time my friend.
 -> END
 
 
