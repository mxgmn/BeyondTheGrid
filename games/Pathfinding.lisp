(template Cheese 🧀 (cylinder 0.5 0.4) (color "ffec27"))

(template Robot 🤖
	(cylinder 0.5 0.3)
	(color "442434")

	(onCollision (if (Cheese #0) (seq (destroy #0) (reward 1.0) (create Cheese))))

	(sensor goal (closest self Cheese all))
	(sensor s (sees self goal))
	(functions raydist gap)

	(patterns
		(== none goal)
		(dir goal)
	)

	(go (dir self))
	;(if (< constant (x2 (raydist (dir self)))) (goto goal) (if (< (distance self goal) (x2 (raydist (to goal)))) (goto goal) (go (maxDirection (* (raydist #0) (* (alignment #0 (to goal)) (alignment #0 (- (right (to goal)) (from goal)))))))))
)

(player Robot)
(setsize 16 16)

(for 12 (apply "AAAA" "WWWW"))
(markov (apply "AW/WA" "AA/WA"))
(apply "AA/AA" "EE/EE")
(markov
	(apply "EA" "EE")
	(apply "EWA" "EEE")
)
(par "A" "S")
(par "E" "A")

(create Cheese)
(create Robot)
