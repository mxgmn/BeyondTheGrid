(template Cheese 🧀 (cylinder 0.3 0.2) (color "ffec27"))
(template Button 🔘 (cylinder 0.4 0.25) (color "ff004d")
	(onCollision (create Cheese))
)

(template Mouse 🐭
	(sphere 0.3)
	(color "fff1e8")

	(onCollision (if
		(Cheese #0) (seq (destroy #0) (reward 1.0))
		(Cat #0) (damage self 1.0 #0)
	))

	(sensor cheese (closest self Cheese all))
	(sensor button (closest self Button all))
	(sensor cat (closest self Cat all))
	(sensor n (near self button))
	(functions raydist turn rundist)

	(patterns
		(== none button)
		(== none cat)
		(== cheese button)
		(== button cat)
		(== cheese cat)
		(dir button)
		(dir cheese)
	)

	(go (dir self))
	(if (< constant (distance self cheese)) (if n (go (dir cat)) (go (maxDirection (rundist #0 Cat)))) (goto cheese))
	;(if (== none cheese) (go (- (maxDirection (rundist #0 Cat)) (to cat))) (if (< (rundist (from cheese) Cheese) (x2 (raydist (left (- (dir self) (to cheese)))))) (if (< (x2 constant) (raydist (right (to cheese)))) (go (right (to cheese))) (goto cheese)) (if (< (distance self button) (raydist (from cheese))) (goto cheese) (go (right (dir cat))))))
	;(if (< (raydist (dir self)) (x2 (distance self cat))) (if n (goto cat) (goto button)) (go (from cat)))
	;(if (== none cheese) (go (- (maxDirection (rundist #0 Cat)) (to cat))) (if (< (rundist (from cheese) Cheese) (x2 (raydist (left (- (dir self) (to cheese)))))) (if (< (x2 constant) (raydist (right (to cheese)))) (go (right (to cheese))) (goto cheese)) (if (< (distance self button) (raydist (from cheese))) (goto cheese) (go (right (dir cat))))))
)

(template Cat 🐱
	(sphere 0.4)
	(color "ff6c24")
	(speed 0.8)

	(sensor prey (closest self Mouse all))

	(let timer (uniform 0 600))
	(let chase true)
	(seq
		(-= timer 1.0)
		(if (< timer 0.0) (seq (= timer 300.0) (= chase (not chase))))
		(if
			(and chase (!= none prey)) (goto prey)
			(go randomDirection)
		)
	)
)

(player Mouse)
(setsize 9 9)

(apply "GG/GG" "EG/GE")
(markov (apply "EG/GG" "EG/GE"))

(position (create Button) 0 0)
(position (create Mouse) 1 0)
(for 2 (position (create Cat) (* 4 randomDirection)))
