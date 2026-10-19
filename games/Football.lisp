(artifact GoalA	(size 0.4 4 2) (color "ff004d"))
(artifact GoalB	(size 0.4 4 2) (color "065ab5"))

(artifact Ball ⚽
	(size 0.5)
	(color "fff1e8")
)

(creature PlayerA 🧔
	(size 0.5 1.6)
	(color "ff77a8")

	(onUse (throw self dir))

	(sensor a all)
	(sensor ball (closest self Ball a))
	(sensor home (closest self GoalA a))
	(sensor goal (closest self GoalB a))
	(sensor goalAhead (== (beamcast dir) goal))
	(sensor friendAhead (PlayerA (beamcast dir)))	
	(sensor empty (== none equipped))

	(seq	
		(let friend (closest self PlayerA a))
		(let enemy (closest self PlayerB a))
		(let teamLeader (closest self (and (PlayerA #0) (Ball (equipped #0))) a))
		(let enemyLeader (closest self (and (PlayerB #0) (Ball (equipped #0))) a))
		(let target (or ball teamLeader enemyLeader))
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

(creature PlayerB 🧑
	(size 0.5 1.6)
	(color "29adff")

	(onUse (throw self dir))

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

	SUB
	(sub
		;(turn dir)
		;(go (left dir))
		;(if empty (if (near self home) (goto ball) (goto home)) (if goalAhead use (goto goal))) ;defensive
		(if empty (if (< (distance self home) 3) (goto ball) (goto home)) (if goalAhead use (goto goal))) ;defensive 2
		
		;(seq (let friend (closest self PlayerB a)) (let enemy (closest self PlayerA a)) (let teamLeader (closest self (and (PlayerB #0) (Ball (equipped #0))) a)) (let enemyLeader (closest self (and (PlayerA #0) (Ball (equipped #0))) a)) (let target (or ball teamLeader enemyLeader)) (if empty (if (or (== (closest ball PlayerB all) self) (== (closest enemyLeader PlayerB all) self) (< (dotproduct (dir enemyLeader) (to enemyLeader)) 0.0)) (goto target) (== (closest home PlayerB all) self) (if (near self home) (turn (to target)) (goto home)) (< (distance self target) 8.0) (go (from friend)) (goto target)) (and goalAhead (< (distance self goal) 4.0)) use friendAhead use (< (distance self enemy) 3.5) (go (+ (x2 (from enemy)) (to goal))) (goto goal)))

		;(if empty (if (< (distance self home) 1) (go (to ball)) (goto home)) (if goalAhead use (goto goal)))
		;(seq (let carrier (closest self (and (PlayerA #0) (Ball (equipped #0))) all)) (if empty (if (and (not (== carrier none)) (< (distance self home) 3.3)) (goto carrier) (if (< (distance self home) 1) (go (to ball)) (goto home))) (if goalAhead use (goto goal))))
	)
	;(seq	
	;	(let friend (closest self PlayerB a))
	;	(let enemy (closest self PlayerA a))
	;	(let teamLeader (closest self (and (PlayerB #0) (Ball (equipped #0))) a))
	;	(let enemyLeader (closest self (and (PlayerA #0) (Ball (equipped #0))) a))
	;	(let target (or ball teamLeader enemyLeader))
	;	(if
	;		empty
	;		(if
	;			(or (== (closest ball PlayerB all) self) (== (closest enemyLeader PlayerB all) self) (< (dotproduct (dir enemyLeader) (to enemyLeader)) 0.0))
	;			(goto target)
	;
	;			(== (closest home PlayerB all) self)
	;			(if (near self home) (turn (to target)) (goto home))
	;			
	;			(< (distance self target) 5.0)
	;			(go (from friend))
	;
	;			(goto target)
	;		)
	;
	;		(and goalAhead (< (distance self goal) 5.0))
	;		use

	;		friendAhead
	;		use

	;		(and (< (distance self enemy) 2.5) (< (distance enemy goal) (distance self goal)))
	;		(go (+ (x2 (from enemy)) (to goal)))

	;		(goto goal)
	;	)
	;)
)

(define hsx 14) ; это простая синтаксическая замена на уровне десугаризации. Кстати, десугаризацию нужно делать всего один раз, в игровом конструкторе
(define hsy 10)
(setsize (* 2 hsx) (* 2 hsy))

(onCollision
	(if
		(and (Ball #0) (or (PlayerA #1) (PlayerB #1)))
		(putOn #0 #1)

		(and (Ball #0) (GoalB #1))
		(seq
			(reward PlayerB -1.0)
			(putOn #0 none)
			(position #0 0 0)

			(foreach PlayerA (position #2 (uniform (- 1.0 hsx) -1.0) (uniform (- hsy) hsy)))
			(foreach PlayerB (position #2 (uniform 1.0 (- hsx 1.0)) (uniform (- hsy) hsy)))
		)

		(and (Ball #0) (GoalA #1))
		(seq
			(reward PlayerB 1.0)
			(putOn #0 none)
			(position #0 0 0)

			(foreach PlayerA (position #2 (uniform (- 1.0 hsx) -1.0) (uniform (- hsy) hsy)))
			(foreach PlayerB (position #2 (uniform 1.0 (- hsx 1.0)) (uniform (- hsy) hsy)))
		)

		(and (or (PlayerA #0) (PlayerB #0)) (or (PlayerA #1) (PlayerB #1)) (!= (equipped #0) none))
		(throw #0 randomDirection)
	)
)

(position (create GoalA) (- 0.2 hsx) 0)
(position (create GoalB) (- hsx 0.2) 0)
(position (create Ball) 0 0)
(for 5 (create PlayerA))
(for 5 (create PlayerB))

(foreach PlayerA (position #0 (uniform (- 1.0 hsx) -1.0) (uniform (- hsy) hsy)))
(foreach PlayerB (position #0 (uniform 1.0 (- hsx 1.0)) (uniform (- hsy) hsy)))

(player PlayerB) ; it's a special form, not an init code? The reason is, games are restarted very often
