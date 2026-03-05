---
title: Getting Started
description: Install ScalarScope and compare your first two runs.
sidebar:
  order: 1
---

## Install from the Microsoft Store

The easiest way to get ScalarScope:

1. Visit the [Microsoft Store listing](https://apps.microsoft.com/detail/9P3HT1PHBKQK) (Store ID: `9P3HT1PHBKQK`)
2. Click Install
3. Requires Windows 10 (build 17763) or later

## Your first comparison

1. Open ScalarScope and click **Compare Two Runs**
2. Load your baseline TFRT trace (before optimization)
3. Load your optimized TFRT trace (after optimization)
4. Review deltas in the **Compare** tab
5. Export a `.scbundle` for reproducible sharing

## Build from source

```bash
# Prerequisites:
#   .NET 9.0 SDK (global.json pins 9.0.100)
#   dotnet workload install maui-windows

git clone https://github.com/mcp-tool-shop-org/ScalarScope-Desktop.git
cd ScalarScope-Desktop
dotnet restore
dotnet build

# Run the desktop app
dotnet run --project src/ScalarScope
```

## Use VortexKit in your own app

The visualization framework is available as a standalone NuGet package:

```bash
dotnet add package VortexKit
```

## Running tests

```bash
# All tests
dotnet test

# Fixture smoke tests only
dotnet test --filter Category=FixtureSmoke

# Determinism tests (verifies reproducible deltas)
dotnet test --filter Category=Determinism
```
