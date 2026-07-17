# Play Mode Tests

Integration tests that run in a real game scene.
Use for cross-system interactions, physics, and coroutines.

**Assembly definition required**: `tests/PlayMode/PlayModeTests.asmdef`
(run `Assets > Create > Scripting > Assembly Definition` in Unity Editor)

## What to test here

- Weapon fire → projectile → enemy damage pipeline
- Enemy FSM state transitions (Idle → Patrol → Chase → Attack)
- Player dodge physics (distance, cooldown, wall collision)
- Cover damage reduction (proximity check)
- Reload timer + dodge interrupt
- Loot container open → progressive loading → item transfer
- Death sequence (equipment drop, corpse spawn, GameManager notification)

## What NOT to test here

- Visual fidelity (shader output, VFX appearance)
- "Feel" qualities (input responsiveness, animation curves)
- Full gameplay sessions (covered by playtesting)
