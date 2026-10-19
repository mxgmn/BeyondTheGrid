(template Ball 🏐 (sphere 0.4) (color "fff1e8")
	(onCollisionWith
		(if
			(Citizen #0) (reward 1.0)
			(Thrower #0) (putOn self #0)
		)
	)
)

(template Citizen 🧑
	(capsule 0.5 1.5)
	(color "ab5236")

	(let r coin)
	(if (< (raydist dir) 1.0) (if r (go left) (go right)) (go randomDirection))
)

(template Thrower 👨
	(capsule 0.4 1.3)
	(color "065ab5")
	(speed 0.9)

	(onUse (throw self dir))

	(sensor target (closest self Citizen all))
	(sensor ball (closest self Ball all))
	(sensor targetAhead (== target (beamcast dir)))
	(functions aligned)

	(patterns
		(dir ball) ; can be automatic with the "round" peoperty
		(== none target) ; это сложнее сделать автоматическим, и точно не на следующей неделе
		(== target ball)
	)

	;(if (== none ball) use (go (to ball)))
	(if (aligned (to target) (to ball)) (if (< (x2 constant) (distance target ball)) (if targetAhead (go (left (- (to target) (dir self)))) use) (goto ball)) (if (< constant (distance target ball)) (if targetAhead use (goto target)) (go (- (left (to ball)) (from ball)))))
	;(if targetAhead (go (dir target)) (if (== none ball) (if (< 0.75 (distance self target)) (goto target) use) (goto ball)))
)

(player Thrower)
(setsize 12 12)

(create Ball)
(create Citizen)
(create Thrower)
