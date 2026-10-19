(template Mouse 🐭
	(speed 1.0)
	(sphere 0.3)
	(color "fff1e8")

	(sensor cat (closest self Cat all))
	(functions raydist turn rundist)

	(patterns (== none cat))

	;(go (dir self))
	(if (< constant (rundist (dir self) Cat)) (go (- (left (maxDirection (rundist #0 Cat))) (maxDirection (raydist (left (- (dir cat) #0)))))) (if (< (raydist (dir self)) (x2 (raydist (to cat)))) (if (< constant (rundist (to cat) Cat)) (goto cat) (go (maxDirection (rundist #0 Cat)))) (go (dir self))))
	;(if (< 3.0 (distance self cat)) (go (left (- dir (dir cat)))) (go (maxDirection (rundist #0 Cat))))
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

(onFrame (foreach Mouse (reward Mouse 1.0)))
(onCollision
	(if
		(and (Mouse #0) (Cat #1))
		(damage #0 1.0 #1)
	)
)

(setsize 9 9)
(player Mouse)

(apply "GG/GG" "EG/GE")
(markov (apply "EG/GG" "EG/GE"))

(position (create Mouse) 0 0)
(for 12 (position (create Cat) (* 4.0 randomDirection)))
