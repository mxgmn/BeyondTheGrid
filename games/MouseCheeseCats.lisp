(template Cheese 🧀 (cylinder 0.3 0.2) (color "ffec27"))

(template Mouse 🐭
	(sphere 0.3)
	(color "fff1e8")

	(sensor cheese (closest self Cheese all))
	(sensor cat (closest self Cat all))
	(functions turn raydist rundist)

	(patterns
		(== none cat)
		(== none cheese)
		(== cheese cat)
		(dir cheese)
	)

	;(go (dir self))
	(if (< (rundist (dir self) Cat) (rundist (to cheese) Cheese)) (if (< constant (x2 (raydist (to cat)))) (go (maxDirection (rundist #0 Cat))) (go (from cheese))) (if (< constant (raydist (from cheese))) (goto cheese) (go (- (maxDirection (raydist #0)) (to cheese)))))
	;(if (< (raydist (left (dir cat))) (raydist (left (- (to cat) (dir self))))) (goto cheese) (go (right (maxDirection (raydist #0)))))
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

(onCollision
	(if
		(and (Cheese #0) (Mouse #1))
		(seq (destroy #0) (reward 1.0) (create Cheese))

		(and (Mouse #0) (Cat #1))
		(damage #0 1.0 #1)
	)
)

(player Mouse)

(setsize 9 9)
(position (create Mouse) 0 0)
(for 10 (position (create Cat) (* 4 randomDirection)))
(create Cheese)
