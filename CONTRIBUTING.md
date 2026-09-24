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

## Pull request merge rules

The `CI / build-and-test` workflow checks restore, Release build, and tests on every pull request. To prevent merging a failing change, configure a repository ruleset in GitHub for the default branch:

1. Open **Settings → Rules → Rulesets** and create a branch ruleset targeting the default branch.
2. Require a pull request before merging.
3. Require the `build-and-test` status check to pass before merging.
4. Keep branch deletion and force pushes restricted.

The PR template reminds contributors to verify CI, cover behavior changes with tests, and update relevant documentation. Repository rulesets are configured in GitHub settings and cannot be enforced by a workflow file alone.
