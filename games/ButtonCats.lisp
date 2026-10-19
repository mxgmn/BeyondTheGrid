(template Button 🔘 (cylinder 0.4 0.25) (color "ff004d"))

(template Mouse 🐭
	(sphere 0.3)
	(color "fff1e8")

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

(onCollision
	(if
		(Button #1)
		(reward 1.0)

		(and (Mouse #0) (Cat #1))
		(damage #0 1.0 #1)
	)
)

(player Mouse)
(define hsize 4.5)
(setsize (x2 hsize) (x2 hsize))

(position (create Button) 0 0)
(position (create Mouse) (uniform (- hsize) -1.0) (uniform (- hsize) hsize))
(for 5 (position (create Cat) (uniform 1.0 hsize) (uniform (- hsize) hsize)))
