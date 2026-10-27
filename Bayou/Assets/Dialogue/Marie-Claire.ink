INCLUDE globals.ink
  
 #portrait: marie_claire_baptiste_neutral #layout: left #background: houseboatBG
 {olivier_known == "":-> main |-> knownName}
 
=== knownName ===
#speaker: Mrs. Baptiste
You look in need of a touch of musical delight, alright!
-> questions

=== main ===
Who might you be?
Don't gotta answer, wanna hear some of my tunes?
-> questions

=== questions ===
+[Who are you?]
 {olivier_known == "":-> introduction("Olivier Baptiste") | -> repeatName}
+{not knownName}[Tunes?]
 -> music
 +{knownName}[Let me hear your Tunes!]
 -> music
+[Chat Later]
 -> goodbye
 
 === introduction(name) ===
 ~ olivier_known = name
 I'm the one and only <color=\#F8FF30>Olivier Baptiste!</color> 
 However, that'll be <color=\#F8FF30>Mr.Baptiste</color> to you.#speaker: Mrs. Baptiste
 -> questions
 
 === repeatName ===
 Forget me already?
 ->questions
 
 === music ===
 Well, ain't it kind of you to ask.
 Listen closely youngun.(INSERT MUSIC PLAYING HERE)
 +[I should get going.]
 -> questions
 === goodbye ===
 until next time my friend.
 -> END