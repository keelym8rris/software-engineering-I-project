# Multiplayer Blackjack

A real-time multiplayer Blackjack game built with Blazor Server and SignalR.

## Branches and environments

This repository uses `main` as the production branch. There is no branch named `master`.

| Branch | Purpose |
| --- | --- |
| `main` | Production source of truth |
| `dev` | Integration and pre-production; starts from the current `main` baseline |
| `keelym8rris/*` | Personal work branches; open pull requests into `dev` |

After changes pass the build on `dev`, promote them to `main` with a pull request. Configure a future DigitalOcean web service to build from `main`; a separate staging service can build from `dev`.

## Requirements

- .NET 9 SDK
- Docker Desktop (optional, for running the container locally)
- Visual Studio Code with the C# Dev Kit extension (optional)

The repository pins the .NET 9 SDK family in `global.json`. Check that the SDK is available with:

```sh
dotnet --list-sdks
```

## Run and try the app

From the repository root, in a VS Code terminal or another terminal:

```sh
dotnet restore BlackjackApp/BlackjackApp.csproj
dotnet run --project BlackjackApp/BlackjackApp.csproj
```

Open <http://localhost:5400>. For a multiplayer smoke check, open a second browser tab, create a game in the first tab, then join it from the second. The `/healthz` endpoint returns HTTP 200 when the web process is running.

In VS Code, open the repository folder and accept the recommended C# Dev Kit extension. The project is also available in the integrated terminal using the commands above.

### Run the Docker version locally

```sh
docker compose up --build
```

Then open <http://localhost:8080>. Stop it with `Ctrl+C`, then run `docker compose down` to remove the container.

## Configuration and deployment

Local launch settings use the `Development` environment. For a deployed container, set `ASPNETCORE_ENVIRONMENT=Production` and use port `8080`. No application secrets are currently required. If secrets are added later, keep them out of source control and configure them as runtime secrets in the hosting provider.

For DigitalOcean App Platform, use the repository's root `Dockerfile`, set the web service HTTP port to `8080`, and configure the health check path as `/healthz`. Keep the service at one instance for now: game tables and player state are held in process memory, so restarts clear games and multiple instances would not share them. Public production use will need a persistence/shared-state design and a concurrency review.

The app targets .NET 9 as requested. Microsoft's current support policy lists .NET 9 end of support as **November 10, 2026**. Before a public production launch after that date, upgrade the target, SDK pin, package, and container images together to .NET 10 LTS.

## Build automation

GitHub Actions restores, builds, and publishes the app on pull requests and pushes to `dev` and `main`. The repository does not have an automated test suite yet, so the workflow currently checks compilation and publishing only.

## How to play

1. Enter your name and click **Create New Game**.
2. Share the game ID; other players enter it on the join screen.
3. Click **Start Game**, place bets, then use **Hit** or **Stand** on your turn.
4. After all players act, the dealer draws to 17 and winnings are settled.
