(template Cheese 🧀 (cylinder 0.5 0.4) (color "ffec27"))

(template Robot 🤖
	(cylinder 0.5 0.3)
	(color "442434")

	(onFrame (changeTileTo "E"))
	(onCollisionWith (if (Cheese #0) (seq (destroy #0) (reward 1.0) (create Cheese) (par "E" "G"))))

	(sensor goal (closest self Cheese allVisible) visual)
	(functions tile raydist gap)

	(patterns (dir goal))

	;(go (dir self))
	(if (tile (to goal)) (go (- (maxDirection (gap #0)) (maxDirection (* (raydist #0) (gap (left (- (to goal) #0))))))) (goto goal))
	;(if (== none goal) (go (maxDirection (gap #0))) (go (to goal)))
)

(player Robot)
(setsize 16 16)

;(for 8 (apply "AAAAA" "WWWWW"))
(for 18 (apply "AAAA/AAAA" "AAAA/WWWW"))
(for 7 (apply "WWW" "WAW"))

(markov (apply "AW/WA" "AA/WA"))
(apply "AA" "EE")
(markov
	(apply "EA" "EE")
	(apply "EWA" "EEE")
)
(par "A" "S")
(par "E" "A")

(create Cheese)
(create Robot)
