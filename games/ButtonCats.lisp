(template Button 🔘 (cylinder 0.4 0.25) (color "ff004d")
	(onCollision (reward 1.0))
)

(template Mouse 🐭
	(sphere 0.3)
	(color "fff1e8")
	(onCollision (if (Cat #0) (damage self 1.0 #0)))

	(sensor button (closest self Button all))
	(sensor cat (closest self Cat all))
	(sensor n (near self button))
	(functions turn raydist rundist)

	(patterns
		(== none button)
		(== none cat)
		(== button cat)
		(dir button)
	)

	;(go (dir self))
	(if n (if (< constant (distance self cat)) (goto cat) (go (left (maxDirection (raydist (left (- (dir cat) #0))))))) (go (maxDirection (* (raydist #0) (* (rundist #0 Cat) (alignment #0 (- (to cat) (from button))))))))
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

(position (create Button) 0 0)
(position (create Mouse) (uniform -4.5 -1.0) (uniform -4.5 4.5))
(for 5 (position (create Cat) (uniform 1.0 4.5) (uniform -4.5 4.5)))
