# BeyondTheGrid
This is the code for the paper https://arxiv.org/abs/2609.24025, to be presented at SIGGRAPH Asia 2026.

<p align="center"><img src="png.png"/></p>

To run, install the .NET 10 SDK and run this command from the repository root:

```sh
dotnet run -c Release
```

Edit `games.lisp` in the repository root to choose which games to run. Game definitions are in `games/`, and graphics assets are in `resources/`.

To search for policies, use an entry such as `(search "ButtonCheeseCats" (maxLength 12) (numSeeds 10))` in `games.lisp` and run
`dotnet run -c Release`. `maxLength` limits policy size; optional `minLength` defaults to 1. `steps` sets the simulation duration.
