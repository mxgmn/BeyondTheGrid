(template Sweet 🍬 (cylinder 0.3 0.2) (color "fff1e8") passable
	(onCollision (if
		(Child #0) (destroy self)
		(Parent #0)	(putOn self #0)
	))
)

(template Child 👶
	(sphere 0.5)
	(color "065ab5")
	(speed 0.333)

	(sensor sweet (closest self Sweet all))

	(goto sweet)
)

(template Parent 👨
	(capsule 0.5 1.6)
	(color "ab5236")
	passable

	(onUse drop)

	(sensor child (closest self Child all))
	(sensor sweet (closest child Sweet all))
	;(sensor mt (maxTile (projdist #0 Child)))
	;(sensor on (< (length mt) 1.0) true)
	(sensor empty (== none equipped))

	(patterns
		(== none sweet)
		(== none child)
		(== child sweet)
		(dir sweet)
	)

	(go (dir self))
	;(if empty (goto sweet) (if (< (distance self child) (distance child sweet)) (if (< (distance self sweet) (distance child sweet)) (go (from sweet)) (go (right (to sweet)))) use))
	;(if empty (goto sweet) (if (< (distance self child) (distance child sweet)) (go (- (right (to sweet)) (dir self))) use))
	;(if empty (goto sweet) (< constant (distance self child)) use (go (from child))) ; my policy
	;(if empty (go (- (dir self) (from sweet))) (if (< (distance self child) (distance child sweet)) (go (- (left (dir self)) (to sweet))) use))
)

(onFrame (reward (amount Sweet)))

(setsize 12 12)
(player Parent)

(create Child)
(create Parent)
(for 10 (create Sweet))
