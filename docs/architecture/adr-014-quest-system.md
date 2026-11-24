# ADR-014: Quest System

**Status**: Proposed
**Date**: 2026-07-17
**Author**: Claude Code (/architecture-review)

---

## Summary

Quests are data-driven via `QuestData` ScriptableObject, with runtime state tracked in a `QuestManager` MonoBehaviour. Each quest has objectives (collect/kill/reach/interact/escort), a giver NPC, floor-level unlock requirements, and rewards. Quest progress persists across character deaths (meta-progression). NPC好感度 (favor) is tracked per-NPC and unlocks trade discounts and hidden quests.

---

## Context

4 NPCs with 3-4 quests each = 14 total quests across 50 floors. Quest progress must survive character death (new character can continue predecessor's quests). The quest board UI depends on this system.

### GDD Requirements Addressed

| Source | Requirement |
|--------|-------------|
| Quest GDD | 4 NPCs, 14 quests, 6 quest types, 好感度 system |
| TR-UI-003 | Bulletin board UI: quest list / detail / active |

---

## Decision

### Architecture

```csharp
public class QuestManager : MonoBehaviour {
    private Dictionary<string, QuestState> _activeQuests;
    private Dictionary<string, int> _npcFavor;    // NPC ID → favor score

    public void AcceptQuest(string questId);
    public void UpdateProgress(string questId, string objectiveId, int delta);
    public void TurnInQuest(string questId);
    public bool IsQuestAvailable(string questId);
    public int GetFavor(string npcId);
}
```

### Quest State Machine

```
Locked → Available (floor reached + prerequisites met)
  → Active (player accepts)
    → ReadyToTurnIn (all objectives complete)
      → Completed (player turns in, rewards granted)
```

### Quest Persistence

Quest state is part of `SaveData.questStates[]` — serialized with each save. On death, quest progress is preserved (only character inventory is lost). New character can continue from predecessor's quest state.

### 好感度 System

- Range: 0–100 per NPC
- Gain: +10 per quest completed, +5 for special item delivery
- Loss: -50 if NPC is attacked (permanent — lose all quests from that NPC)
- Threshold 50: 10% trade discount
- Threshold 80: hidden quest unlocked

### Rules

1. **One active quest tracking at a time**: HUD shows current tracked quest. Player can switch tracking via bulletin board.
2. **Collect items must be in backpack to turn in**: Items sold/vendored don't count.
3. **Escort NPC death = quest failure but re-acceptable**: NPC respawns at origin, favor -5.
4. **Quest items are protected from auto-sell**: "F-take-all" in shops skips quest-tagged items.

---

## ADR Dependencies

- **Depends On**: ADR-001 (Event Bus), ADR-002 (SO Data), ADR-013 (Save/Load)
- **Depended On By**: None
