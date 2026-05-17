 INCLUDE globals.ink
 
 #portrait: father_landry_neutral #layout: left #background: church
 {priest_name == "":-> main |-> knownName}
 
=== knownName ===
#speaker: Father Landry
It's good to see you again.
-> service

=== main ===
It's a pleasure to make your acquaintance.
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
 
 
