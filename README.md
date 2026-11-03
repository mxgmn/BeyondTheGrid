# BeyondTheGrid

This is the code for the paper https://arxiv.org/abs/2609.24025,
to be presented at SIGGRAP Asia 2026.

<p align="center"><img src="png.png"/></p>

To run, install the .NET 10 SDK and run this command from the repository root:

```sh
dotnet run
```

Edit `games.lisp` in the repository root to choose which games to run. Game definitions
are in `games/`, and graphics assets are in `resources/`. Building copies these files
into `bin/` automatically; `bin/` and `obj/` are generated locally and excluded from Git.

The project is `BeyondTheGrid.csproj`, and the Windows executable is `bin/BeyondTheGrid.exe`.
Only the 15 emoji images used by the included games are bundled in `resources/emoji/`.

To search for policies, use an entry such as
`(search "Football" (maxLength 8) (numSeeds 3) (steps 120))` in `games.lisp` and run
`dotnet run`. Comment out any active `(real ...)` entries first. `maxLength` limits
policy size; optional `minLength` defaults to 1. `steps` sets the simulation duration.

The paper's agentic sketching workflow is also available from the command line.
Choose a game with a `test` entry in `games.lisp`, for example:

```lisp
(test "Football" (length 8) (numSeeds 5) (steps 10800))
```

From the repository root, run:

```powershell
# Evaluate a complete policy (several quoted policy arguments are also accepted).
dotnet run -- "(if empty (goto ball) use)"

# Enumerate complete policies by placing the hole at the root.
dotnet run -- "(search (subtype ACTION) (shape SUB))"

# Fill an action branch; exclude conditions already known in that branch.
dotnet run -- "(search (subtype ACTION) (shape (if empty SUB use)) (antiPatterns empty) (length 6))"

# Search for a Boolean condition in a fixed policy.
dotnet run -- "(search (subtype BOOL) (shape (if SUB (goto ball) use)) (length 5))"
```

`SUB` is a typed hole. Search inserts each generated expression into the shape and
evaluates the resulting **complete policy**. `ACTION` is the default hole type;
`BOOL` fills a Boolean condition. Repeated `SUB` occurrences use the same expression.
Optional `antiPatterns` exclude matching subexpressions from the generated fragment,
without restricting the fixed part of the shape. The usual game antipatterns also apply.
Movement, item-use, and required-sensor checks apply to the completed policy.

`length` counts nodes in the expression replacing `SUB`: `use` has length 1,
`(goto ball)` has length 2, and `(go (dir self))` has length 3. The surrounding
shape does not count toward this limit. A command's `(length N)` overrides the
`test` entry's `length`; the fallback is 10. `maxLength` is used separately by
configured whole-policy searches. `numSeeds` and `steps` come from the `test`
entry; the objective is the minimum score across those seeds.

Search prints the best fragment and completed policy at each size bound and saves
XML reports under `reports/`. Improved completed policies are recorded in
`disc-<Game>.lisp` and can be passed directly to the evaluator. Run searches
sequentially because they use all CPU cores and share discovery files.

To prepare the isolated coding-agent folders used by the paper, comment out active
`real` entries, add an entry such as
`(prepare "Football" (length 8) (numSeeds 5) (steps 10800) tool)`, and run `dotnet run`.
Omit `tool` for the direct-evaluation-only agent prompt. Each folder under `evals/`
contains the executable and dependencies, game description, DSL sources, configuration,
and `prompt.txt`. Run commands from that folder; .NET 10 is required.
The generated configuration sets `timeLimitMinutes` to 20: subsequent commands are
refused after that interval from the first invocation. The experiment runner must
terminate any command still running at the deadline. Ordinary `test` entries have
no time limit unless one is explicitly configured.

Game templates can also pair a policy containing `SUB` with `(sub <expression>)`;
the replacement's type determines the hole type. Unfilled or mistyped holes are errors.
Generated experiment folders, logs, discoveries, and reports are excluded from Git.
