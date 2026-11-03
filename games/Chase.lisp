(template Button 🔘
	(cylinder 0.4 0.25)
	(color "ff004d")
	(onCollision (if (Robot #0) (reward 1.0)))

	(speed 1.5)

	(let r coin)
	(if (< (raydist dir) 1.0) (if r (go (left dir)) (go (right dir))) (go randomDirection))
)

(template Robot 🤖
	(cylinder 0.4 0.25)
	(color "442434")

	(sensor goal (closest self Button all))
	(functions raydist)

	(patterns (== none goal))

	;(go (dir self))
	(if (< (raydist (to goal)) (x2 (raydist (dir self)))) (go (- (dir goal) (to goal))) (if (< constant (raydist (dir self))) (go (- (dir goal) (dir self))) (goto goal)))
	;(go (maxDirection (* (raydist #0) (* (square (alignment #0 (to goal))) (alignment #0 (- (dir goal) (to goal)))))))
	;(go (maxDirection (* (raydist #0) (* (alignment #0 (from goal)) (square (alignment #0 (- (dir goal) (from goal))))))))
)

(setsize 9 9)
(create Button)
(create Robot)

(player Robot)
