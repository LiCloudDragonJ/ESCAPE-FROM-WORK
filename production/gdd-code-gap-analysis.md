# GDD vs Code Gap Analysis

> **Date**: 2026-07-17
> **Method**: Full-text comparison — 6 GDD files vs 48 C# source files
> **Key**: ✅ Implemented / ⚠️ Partial / ❌ Missing

---

## Summary

| Priority | Count | Description |
|----------|-------|-------------|
| 🔴 P0 | 6 | Blocks core gameplay loop |
| 🟡 P1 | 7 | Severely limits gameplay depth |
| 🟢 P2 | 12 | Quality gaps, polish, integration |

---

## 🔴 P0 — Blocks Core Gameplay

| # | System | Feature | Detail |
|---|--------|---------|--------|
| 1 | Combat | **Stamina system** | No stamina variable exists. Dodge/melee/aim have zero cost. |
| 2 | Combat | **Manual aim key binding** | GDD: hold RMB for manual aim. Code: LeftShift toggle. RMB = melee. |
| 3 | Weapons | **Reload has no duration** | `Reload()` instantly sets `currentAmmo = magazineSize`. No timer, no animation, no interrupt. |
| 4 | Weapons | **No reserve ammo system** | Only `currentAmmo` exists. No ammo inventory, no ammo pickup, no reload-from-reserve. |
| 5 | Enemies | **Only 1 of 14 enemy types** | KPI丧尸 only. All others (PPT怨灵, 邮件幽灵, 会议恶魔, 打印机怪, 饮水机丧尸, 午睡魔, 老鼠群, 保安×2, Boss×4) missing. |
| 6 | Combat | **No cover system** | Proximity to furniture has no effect on damage or hitbox. |

## 🟡 P1 — Severely Limits Depth

| # | System | Feature | Detail |
|---|--------|---------|--------|
| 7 | Enemies | **No floor scaling** | All enemies use raw EnemyData values regardless of floor number. |
| 8 | Enemies | **No random variants** | 30% Elite/Swift/Tanky/Explosive/Regenerating system not implemented. |
| 9 | Weapons | **3 of 4 A-class missing** | No shotgun multi-pellet, no beam weapon, no AOE parabolic projectile. |
| 10 | Weapons | **C-class effects are stubs** | Blind/Root/Taunt only Debug.Log(). Do not actually apply status effects. |
| 11 | Combat | **No leg-shot hit zone** | Capsule region detection not implemented — uses distance threshold instead. |
| 12 | Combat | **No auto-release at max charge** | `IsCharging` stays true forever; no 2s auto-release timer. |
| 13 | Enemies | **Detection is 360°** | Uses full OverlapSphere. No 120° cone angle check, no raycast occlusion, no hearing system. |

## 🟢 P2 — Quality & Integration

| # | System | Feature | Detail |
|---|--------|---------|--------|
| 14 | Floor | **FloorBuilder not wired** | FloorBuilder.cs (692 lines) exists but FloorManager uses old FloorGenerator. |
| 15 | Loot | **No ammo safety net** | Total ammo < 10 should trigger emergency cabinet on next floor entry. |
| 16 | Loot | **No coffee freshness timer** | ItemData has FreshnessDurationMinutes field but no decay logic. |
| 17 | Loot | **Container loading delays differ** | Code: 0.1/0.2/0.5/1/2/3s. GDD: 0/1/2/4/8/12s. |
| 18 | Loot | **Furniture loot variants not applied** | Supervisor/CEO/ransacked variants exist visually but don't modify loot quality. |
| 19 | UI | **No stamina bar** | HUD has no stamina display. |
| 20 | UI | **No base UI panels** | Weapon rack, stash, bulletin board — all missing. |
| 21 | UI | **No reload progress** | No circular progress indicator near crosshair. |
| 22 | UI | **No weapon switch toast** | No weapon name + icon display on switch. |
| 23 | Combat | **No V-key quick melee** | Quick melee binding doesn't exist. |
| 24 | Combat | **Dead enemies keep "Enemy" tag** | Auto-aim can still lock onto dead bodies. |
| 25 | Combat | **Reload on full magazine** | `Reload()` always resets ammo; doesn't check if already full. |

---

## What IS Done Well

- Core Input: WASD movement, mouse look ✓
- Basic shooting: projectile with travel time, spread, headshot bonus ✓
- Weapon slots: A/C/Melee + scroll wheel cycling ✓
- Enemy FSM: Idle/Patrol/Chase/Attack/Dead states ✓
- Loot containers: progressive loading, reopen-continue, F-take-all, drag-drop ✓
- LootTable: weighted random with min/max rolls ✓
- FloorBuilder: 3 archetypes, core筒+ring corridor, 8 stairs, BFS anchor selection ✓
- FurniturePlacer: mandatory/optional placement, supervisor/ransacked variants ✓
- Damage flow: projectile → tag check → IDamageable → floating text ✓
- Inventory: grid-based backpack with stacking, equipment slots, gear equip/unequip ✓
- HUD: health bar, ammo, floor info, interaction prompt, extraction warning ✓
- Death: DeathContext, MemorialWall, equipment drop ✓

---

*End of report.*
