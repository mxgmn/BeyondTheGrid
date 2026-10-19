(template Cheese 🧀 (cylinder 0.3 0.2) (color "ffec27"))
(template Button 🔘 (cylinder 0.4 0.25) (color "ff004d"))

(template Mouse 🐭
	(sphere 0.3)
	(color "fff1e8")

	(onCollision (if
		(Button #0)	(create Cheese)
		(Cheese #0)	(seq (destroy #0) (reward 1.0))
	))

	(sensor cheese (closest self Cheese all) required)
	(sensor button (closest self Button all) required)
	(sensor n (near self button))
	(functions raydist) ; because raydist should help woth near-wall buttons

	(patterns
		(== none button)
		(== button cheese)
		(dir button)
		(dir cheese)
	)

	(go dir)
	;(if (== none cheese) (goto button) (goto cheese))
	;(go (maxDirection (* (raydist #0) (alignment #0 (to button)))))

	;(if n (if (< constant (raydist (right (dir self)))) (go (- (right (dir self)) (to button))) (go (left (to cheese)))) (if (< constant (distance self cheese)) (goto button) (goto cheese)))
	;(if (near self button) (go (right (to button))) (if (< (distance self button) (distance self cheese)) (goto button) (goto cheese)))
	;(if (< (distance self cheese) (x2 (distance self button))) (goto cheese) (if n (go (right (to button))) (goto button)))
)

(setsize 8 8)
;(apply "GG/GG" "EG/GE")
;(markov (apply "EG/GG" "EG/GE"))
(create Mouse)
(create Button)

(player Mouse)
