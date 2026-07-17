# Edit Mode Tests

Unit tests that run without entering Play Mode.
Use for pure logic: formulas, state machines, data validation.

**Assembly definition required**: `tests/EditMode/EditModeTests.asmdef`
(run `Assets > Create > Scripting > Assembly Definition` in Unity Editor)

## What to test here

- Damage formula calculations
- Loot table weighted random distribution
- Floor generation seed determinism
- Enemy floor-scaling math
- Stamina regen timing
- Inventory stacking logic
- Item grid placement validation
- WeaponData field validation

## What NOT to test here

- Anything requiring `Time.deltaTime`
- Physics interactions (colliders, triggers)
- MonoBehaviour lifecycle (Awake, Start, Update)
- Coroutines
- Scene loading
