(template Robot ✂️
	(cylinder 0.5 0.3)
	(color "ff6c24")

	(functions tile raydist)

	(onFrame (if (changeTileTo "E") (reward 1.0)))

	;(go (dir self))
	(if (tile (maxDirection (raydist #0))) (go (left (- (maxDirection (* (raydist #0) (alignment #0 (left (dir self))))) (dir self)))) (if (tile (dir self)) (go (dir self)) (go (maxDirection (raydist #0)))))
	;(if (tile (dir self)) (go (maxDirection (* (raydist #0) (alignment #0 (dir self))))) (go (- (maxDirection (raydist (left (- (dir self) #0)))) (dir self))))
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
