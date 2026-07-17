# Test Infrastructure

**Engine**: 团结引擎 1.9.3 (Tuanjie Engine — Unity 中国版)
**Test Framework**: Unity Test Framework (UTF)
**CI**: `.github/workflows/tests.yml`
**Setup date**: 2026-07-17

## Directory Layout

```
tests/
  unit/           # Isolated unit tests (formulas, state machines, logic)
  integration/    # Cross-system and save/load tests
  EditMode/       # UTF Edit Mode — runs without Play Mode, pure C#
  PlayMode/       # UTF Play Mode — runs in real game scene
  smoke/          # Critical path test list for /smoke-check gate
  evidence/       # Screenshot logs and manual test sign-off records
```

## Running Tests

**In Unity Editor**: Window → General → Test Runner → Run All

**Headless (CI)**: Unity Test Runner via `game-ci/unity-test-runner@v4` GitHub Action

## Test Naming

- **Files**: `[system]_[feature]_test.cs`
- **Functions**: `[Feature]_[Scenario]_[ExpectedResult]`
- **Example**: `Combat_DamageFormula_HeadshotAppliesMultiplier`

## Story Type → Test Evidence

| Story Type | Required Evidence | Location |
|---|---|---|
| Logic | Automated unit test — must pass | `tests/unit/[system]/` |
| Integration | Integration test OR playtest doc | `tests/integration/[system]/` |
| Visual/Feel | Screenshot + lead sign-off | `tests/evidence/` |
| UI | Manual walkthrough OR interaction test | `tests/evidence/` |
| Config/Data | Smoke check pass | `production/qa/smoke-*.md` |

## Assembly Definitions

Each test directory requires an Assembly Definition file:
- `tests/EditMode/EditModeTests.asmdef` — references game code assemblies
- `tests/PlayMode/PlayModeTests.asmdef` — references game code assemblies

## CI

Tests run automatically on every push to `main` and on every pull request.
A failed test suite blocks merging.
