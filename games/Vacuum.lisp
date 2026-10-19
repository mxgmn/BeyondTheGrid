(template Robot 💨
	(cylinder 0.5 0.3)
	(color "ff6c24")

	(functions raydist)

	(onFrame (if (changeTileTo "E") (reward Robot 1.0)))

	;(go (dir self))
	(if (< (x2 constant) (raydist (dir self))) (go (- (left (dir self)) (maxDirection (* (raydist (left (- #0 (dir self)))) (raydist (left (- (dir self) #0))))))) (go (- (maxDirection (* (raydist #0) (* (alignment #0 (left (dir self))) (raydist (left (- (dir self) #0)))))) (dir self))))
)

(setsize 12 12)
(player Robot)

(for 7 (apply "AAAA/AAAA" "AAAA/WWWW"))
(for 4 (apply "WWW" "WAW"))
(markov (apply "AW/WA" "AA/WA"))
(apply "AA/AA" "EE/EE")
(markov
	(apply "EA" "EE")
	(apply "EWA" "EEE")
)
(par "A" "S")
(par "E" "A")

(create Robot)
