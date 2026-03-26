# Git Hooks And C# Formatting

This repository uses local .NET tools and `Husky.Net` to keep staged C# files formatted before each commit.

## What Happens Automatically

When you run `dotnet restore APITemplate.slnx` or `dotnet build APITemplate.slnx`, MSBuild runs a target from the repository root `Directory.Build.targets` that:

1. Restores local tools from `.config/dotnet-tools.json`
2. Installs Git hooks with `dotnet husky install`

The target is incremental:

- it reruns when `.config/dotnet-tools.json` changes
- it reruns when `.husky/pre-commit` does not exist

## Pre-Commit Behavior

The pre-commit hook runs `dotnet husky run --group pre-commit`.

`Husky.Net` then:

- formats staged `*.cs` files with `CSharpier`
- adds the formatted files back to the Git index
- blocks the commit if formatting fails

## Manual Commands

Format the repository:

```powershell
dotnet csharpier format .
```

Check formatting without changing files:

```powershell
dotnet csharpier check .
```

Run the configured Husky tasks manually:

```powershell
dotnet husky run --group pre-commit
```

Restore local tools manually:

```powershell
dotnet tool restore
```

## CI And Disabling Hook Installation

CI should not install local Git hooks. Disable the MSBuild target by setting either of these environment variables to `0`:

- `RESTORE_TOOLS`
- `HUSKY`

Example:

```powershell
$env:RESTORE_TOOLS = "0"
dotnet build APITemplate.slnx
```

## Troubleshooting

If the hook is missing or outdated:

```powershell
dotnet tool restore
dotnet husky install
```

If you need to bypass the hook for an exceptional case:

```powershell
git commit --no-verify
```
