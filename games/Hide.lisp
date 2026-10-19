(template Citizen 🧑
	(capsule 0.45 1.5)
	(color "ab5236")
	(speed 0.6)

	(sensor ghost (closest self Ghost all))

	(if (sees self ghost) (goto ghost) (go (- (maxDirection (* (gap #0) (* (alignment #0 (dir self)) (alignment #0 (to ghost))))) (dir self))))
)

(template Ghost 👻
	(capsule 0.45 1.2)
	(color "fff1e8")

	(sensor citizen (closest self Citizen allVisible))
	(functions raydist gap turn)

	(patterns (dir citizen))

	;(go (dir self))
	(if (< constant (x2 (raydist (left (- (dir self) (to citizen)))))) (go (- (maxDirection (* (gap #0) (raydist (left (- #0 (dir self)))))) (from citizen))) (go (- (maxDirection (* (gap #0) (raydist (left (- (dir self) #0))))) (from citizen))))
	;(if (!= citizen none) (go (maxDirection (* (gap #0) (alignment (from citizen) #0)))) (go dir))
	;(go (maxDirection (* (raydist #0) (* (gap #0) (* (alignment #0 (dir self)) (alignment #0 (- (dir self) (to citizen))))))))
	;(if (< f075 (distance self citizen)) (go (maxDirection (* (gap #0) (alignment #0 (dir self))))) (go (left (dir self))))
)

(onFrame (foreach Citizen (foreach Ghost (if (not (sees #0 #1)) (reward 1.0)))))

(player Ghost)
(setsize 16 16)

(for 12 (apply "AAAAA/AAAAA" "AAAAA/WWWWW"))
(for 5 (apply "WWW" "WAW"))
(markov (apply "AW/WA" "AA/WA"))
(apply "AA/AA" "EE/EE")
(markov
	(apply "EA" "EE")
	(apply "EWA" "EEE")
)
(par "A" "S")
(par "E" "A")

(create Citizen)
(create Ghost)
