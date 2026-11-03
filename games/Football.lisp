(template GoalA (cuboid 0.4 4 2) (color "ff004d"))
(template GoalB (cuboid 0.4 4 2) (color "065ab5"))

(template Ball ⚽
	(sphere 0.5)
	(color "fff1e8")

	(onCollision
		(if
			(or (PlayerA #0) (PlayerB #0))
			(putOn self #0)

			(or (GoalA #0) (GoalB #0))
			(seq
				(if (GoalA #0) (reward 1.0) (reward -1.0))
				; A fresh ball clears its velocity after a goal.
				(destroy self)
				(position (create Ball) 0 0)
				(foreach PlayerA (position #1 (uniform -13 -1) (uniform -10 10)))
				(foreach PlayerB (position #1 (uniform 1 13) (uniform -10 10)))
			)
		)
	)
)

(template PlayerA 🧔
	(capsule 0.5 1.6)
	(color "ff77a8")

	(onUse (throw self dir))
	(onCollision (if (and (or (PlayerA #0) (PlayerB #0)) (!= equipped none)) (throw self randomDirection)))

	(sensor a all)
	(sensor ball (closest self Ball a))
	(sensor home (closest self GoalA a))
	(sensor goal (closest self GoalB a))
	(sensor goalAhead (== (beamcast dir) goal))
	(sensor friendAhead (PlayerA (beamcast dir)))
	(sensor empty (== none equipped))
	(sensor friend (closest self PlayerA a))
	(sensor enemy (closest self PlayerB a))
	(sensor teamLeader (closest self (and (PlayerA #0) (Ball (equipped #0))) a))
	(sensor enemyLeader (closest self (and (PlayerB #0) (Ball (equipped #0))) a))
	(let target none)

	(seq
		; Prefer the loose ball, then its carrier on either team.
		(= target ball)
		(if (== target none) (= target teamLeader))
		(if (== target none) (= target enemyLeader))
		(if
			empty
			(if
				(or (== (closest ball PlayerA all) self) (== (closest enemyLeader PlayerA all) self) (< (dotproduct (dir enemyLeader) (to enemyLeader)) 0.0))
				(goto target)

				(== (closest home PlayerA all) self)
				(if (near self home) (turn (to target)) (goto home))

				(< (distance self target) 5.0)
				(go (from friend))

				(goto target)
			)

			(and goalAhead (< (distance self goal) 5.0))
			use

			friendAhead
			use

			(and (< (distance self enemy) 2.5) (< (distance enemy goal) (distance self goal)))
			(go (+ (x2 (from enemy)) (to goal)))

			(goto goal)
		)
	)
)

(template PlayerB 🧑
	(capsule 0.5 1.6)
	(color "29adff")

	(onUse (throw self dir))
	(onCollision (if (and (or (PlayerA #0) (PlayerB #0)) (!= equipped none)) (throw self randomDirection)))

	(sensor a all)
	(sensor ball (closest self Ball a))
	(sensor home (closest self GoalB a))
	(sensor goal (closest self GoalA a))
	(sensor goalAhead (== (beamcast dir) goal))
	(sensor friendAhead (PlayerB (beamcast dir)))
	(sensor empty (== none equipped))

	(patterns
		(== none home)
		(== none goal)
	)

	; Defensive baseline policy.
	(if empty (if (< (distance self home) 3) (goto ball) (goto home)) (if goalAhead use (goto goal)))
)

(setsize 28 20)
(position (create GoalA) -13.8 0)
(position (create GoalB) 13.8 0)
(position (create Ball) 0 0)
(for 5 (create PlayerA))
(for 5 (create PlayerB))

(foreach PlayerA (position #0 (uniform -13 -1) (uniform -10 10)))
(foreach PlayerB (position #0 (uniform 1 13) (uniform -10 10)))

(player PlayerB)
