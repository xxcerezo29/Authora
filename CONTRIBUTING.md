# Contributing

## Local setup

Install the .NET 10 SDK. Restore, build, and run tests from the repository root:

```powershell
dotnet restore Authora.slnx
dotnet build Authora.slnx --configuration Release --no-restore
dotnet test tests/Authora.Tests/Authora.Tests.csproj --configuration Release --no-build
```

The tests use in-memory stores and SQLite and should not need credentials, a running database, or the sample database file.

## Branches and commits

Use short topic branches named `feat/<topic>`, `fix/<topic>`, `docs/<topic>`, or `test/<topic>`. Use concise Conventional Commit style messages such as `feat: add token abilities` or `test: cover refresh replay`.

Open a pull request with a short summary, behavioral/API impact, and the commands run. Include focused tests for behavior changes and update the relevant guide when changing a public API or security-sensitive behavior. Do not commit secrets, local databases, `bin`/`obj` output, or IDE user state.
