# Smoke Test: Critical Paths

**Purpose**: Run these checks in under 15 minutes before any QA hand-off.
**Run via**: `/smoke-check`
**Update**: Add new entries when new core systems are implemented.

## Core Stability (always run)

1. Game launches without crash
2. Player character spawns and responds to WASD movement
3. Camera follows player correctly (third-person shoulder view)

## Core Mechanic (MVP — update per sprint)

4. Player can shoot (LMB) and projectile flies toward aim direction
5. Player can switch weapons via scroll wheel (A → C → Melee → A)
6. Player can quick-melee (V key) consuming stamina
7. Player can dodge (Space) consuming 25 stamina
8. Player can manual-aim (RMB hold) draining stamina at 8/s
9. Stamina regenerates after 0.5s idle at 15/s
10. Reload (R) takes weapon-specific time and fills magazine
11. Enemy (KPI丧尸) spawns, detects player, chases, and attacks
12. Enemy dies on HP ≤ 0 and drops loot (badge + paperclips)
13. Floor generates with entry/exit stairs and BFS-verified path
14. Loot container opens, items load progressively, player can transfer items

## Data Integrity

15. Save game completes without error (once save system is implemented)
16. Load game restores correct state (once load system is implemented)

## Performance

17. No visible frame rate drops on target hardware (60fps target)
18. No memory growth over 5 minutes of play
