 INCLUDE globals.ink
 
 #portrait: caliste_ardoin_neutral #layout: left #background: merchant
 {caliste_known == "":-> main |-> knownName}
 
=== knownName ===
#speaker: Caliste
<b>DON'T...</b> Oh it's you.
You want to see my wares?
-> questions

=== main ===
<b>DON'T COME ANY CLOSER!!!</b>
-> calm

=== calm ===
+[I just want to talk.]
-> speak
+[I'll give you your space.]->END

=== speak ===
Talk...
You seem... 
We can talk.
My wares may be of interest.
-> questions

=== questions ===
+[Who are you?]
 {caliste_known == "":-> introduction("Caliste") | -> repeatName} 
+[Wares?]
 -> shop
+[Chat Later.]
 -> goodbye
 
 === introduction(name) ===
 ~ caliste_known = name
 <color=\#F8FF30>Caliste.</color> #speaker: Caliste
 +[Ah...]
 -> questions
 
 === repeatName ===
 Tsk...
 ->questions
 
 === shop ===
 (CALL FUNCTION TO OPEN SHOP HERE!!!)
 (AFTER EXITING SHOP RETURN HERE!!!)
 -> questions
 === goodbye ===
 I'll be seeing you.
 -> END
 
 