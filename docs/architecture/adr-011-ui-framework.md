# ADR-011: UI Framework

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

All UI is built with Unity UGUI (Canvas system). The HUD uses polling (read values from PlayerCombat/PlayerHealth each frame). Panel-based UI (inventory, loot, base) uses a single-panel-at-a-time manager. The Canvas uses `Scale With Screen Size` at 1920×1080 reference resolution. HUD polling is intentionally simple for MVP — event-driven UI binding is reserved for Post-MVP optimization.

---

## Context

The UI touches 7 other systems and has 5 screen groups (combat HUD, loot panel, base UI, death screen, memorial wall). The polling architecture is chosen for MVP simplicity — with <10 values polled per frame, the performance cost is negligible (<0.05ms/frame).

### GDD Requirements Addressed

| TR-ID | Requirement |
|-------|-------------|
| TR-UI-001 | Combat HUD with health, stamina, ammo, floor, crosshair |
| TR-UI-002 | Loot panel: 3-column, drag-drop, F-take-all |
| TR-UI-003 | Base UI: stash, weapon rack, bulletin board, workbench |
| TR-UI-004 | Death screen with full death context |
| TR-UI-005 | Memorial wall scrollable list |
| TR-UI-006 | UGUI Canvas: Scale With Screen Size, 1920×1080 reference |

---

## Decision

### Canvas Architecture

```
Canvas (Screen Space - Overlay, Scale With Screen Size 1920×1080)
├── HUD (always visible during Raid)
│   ├── HealthBar (top-left, 480×30)
│   ├── StaminaBar (below health, 480×20)
│   ├── AmmoDisplay (top-left text)
│   ├── FloorInfo (top-right, 330×100)
│   ├── Crosshair (center, 24×24)
│   ├── InteractionPrompt (bottom-center)
│   └── ExtractionWarning (center, hidden until near extract)
├── LootPanel (1450×820, shown on E near container)
│   ├── EquipmentColumn (18%)
│   ├── BackpackGrid (41%)
│   └── ContainerGrid (37%)
├── BasePanels (shown in Tea Room)
│   ├── StashPanel (3-column, equipment|backpack|stash)
│   ├── WeaponRack (80×80 grid + A/C/Melee loadout slots)
│   └── BulletinBoard (quest list|detail|active)
├── DeathScreen (full-screen overlay on death)
└── MemorialWall (scrollable list in Tea Room)
```

### Panel Manager

```csharp
public class UIPanelManager : MonoBehaviour {
    private GameObject _activePanel;

    public void OpenPanel(GameObject panel) {
        if (_activePanel != null)
            _activePanel.SetActive(false);
        _activePanel = panel;
        _activePanel.SetActive(true);
        // Disable player movement while any panel is open.
        GameEvents.OnPanelOpened?.Raise(panel.name);
    }

    public void ClosePanel() {
        if (_activePanel != null)
            _activePanel.SetActive(false);
        _activePanel = null;
        GameEvents.OnPanelClosed?.Raise();
        // Re-enable player movement.
    }
}
```

### HUD Polling Strategy

```csharp
// HUDManager.Update() — called every frame (~16ms at 60fps)
void Update() {
    healthBar.value = Mathf.Lerp(healthBar.value,
        playerHealth.CurrentHealth / playerHealth.MaxHealth, 0.1f);
    staminaBar.value = playerCombat.CurrentStamina / playerCombat.MaxStamina;
    ammoText.text = $"{weapon.CurrentAmmo} / {inventory.GetAmmoCount(weapon.Data.AmmoType)}";
    floorText.text = $"Floor {floorManager.CurrentFloor}";
    crosshair.color = playerAim.IsManualAim ? solidColor : translucentColor;
}
```

### Input Locking

When any panel is open:
- Player movement input → disabled
- Player combat input → disabled (checked in PlayerCombat.Update)
- Camera rotation → disabled
- Panel toggle (Tab) → closes current panel, re-enables input

### Rules

1. **Single panel at a time**: Opening a new panel closes the previous one.
2. **Polling for HUD, events for state changes**: HUD reads values each frame. State transitions (death, extraction) use events.
3. **No UI raycasts through panels**: Invisible full-screen blocker behind panels catches stray clicks.
4. **Rarity color mapping**: Common=#AAAAAA, Uncommon=#44AA44, Rare=#4466CC, Epic=#8844CC, Legendary=#CCAA00, Mythic=#CC2222.
5. **Canvas reference resolution**: 1920×1080, Scale With Screen Size, Screen Match Mode: 0.5 (width/height balance).

---

## Engine Compatibility

| Aspect | Status |
|--------|--------|
| UGUI | Standard Unity — Canvas + Image + Text + Slider + GridLayoutGroup |
| TextMeshPro | Preferred over legacy Text for all UI text |
| World-space UI | FloatingDamageText uses World Space Canvas with billboard component |

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus), ADR-005 (Stamina), ADR-006 (Inventory)
- **Depended On By**: None currently
