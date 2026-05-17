 INCLUDE globals.ink
 
 #portrait: father_landry_neutral #layout: left
 {priest_name == "":-> main |-> knownName}
 
=== knownName ===
#speaker: Father Landry
It's good to see you again.
-> questions

=== main ===
It's a pleasure to make your acquaintance.
-> questions

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
 -> main
 
 === repeatName ===
 Call me Landry... 
 ->main
 
 === location ===
 The Bayou... You must have hit your head hard.
 
 -> main
 === goodbye ===
 until next time my friend.
 -> END
 
 
