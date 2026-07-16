using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.Level
{
    // ---- Enums ----

    public enum QuestType
    {
        Collect,      // Collect N items
        Eliminate,    // Kill N enemies
        Extract,      // Extract a specific item from a floor
        Explore,      // Reach a specific floor and extract
        StoryChain,   // Multi-step story quest
    }

    public enum QuestStatus { Locked, Available, Active, Completed, TurnedIn }

    // ---- Data classes ----

    [System.Serializable]
    public class QuestRequirement
    {
        [Tooltip("Item asset path for Collect/Extract quests.")]
        public string itemAssetPath;

        public int count;

        [Tooltip("Target floor for Extract/Explore quests.")]
        public int targetFloor;

        [Tooltip("Enemy tag for Eliminate quests.")]
        public string enemyTag;
    }

    [System.Serializable]
    public class QuestReward
    {
        [Tooltip("Item asset path.")]
        public string itemAssetPath;
        public int count;

        [Tooltip("回形针 (currency) amount.")]
        public int currencyAmount;

        public bool isStashUpgrade;
        public bool isNewWeapon;
    }

    /// <summary>
    /// Defines one quest: requirements, rewards, prerequisites, and unlock chain.
    /// Instantiated as ScriptableObject assets or defined inline in QuestManager.
    /// </summary>
    [System.Serializable]
    public class QuestDef
    {
        public string questId;
        public string title;
        [TextArea(2, 4)] public string description;
        public string giverName;
        public QuestType type;
        public QuestRequirement[] requirements;
        public QuestReward[] rewards;
        public string[] prerequisiteQuestIds;
        public string[] unlockQuestIds;
        [TextArea(2, 4)] public string loreText;
    }

    // ---- Manager ----

    /// <summary>
    /// Tracks quest state across a run. Quests are accepted from NPCs at the
    /// tea-room base between raids. Progress is checked after each extraction.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        /// <summary>All quest definitions (assigned in inspector or populated from SOs).</summary>
        public List<QuestDef> allQuests = new List<QuestDef>();

        /// <summary>Runtime quest states, keyed by questId.</summary>
        public Dictionary<string, QuestStatus> questStates = new Dictionary<string, QuestStatus>();

        // Counters for active quest tracking.
        public Dictionary<string, int> collectProgress   = new Dictionary<string, int>();
        public Dictionary<string, int> eliminateProgress = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialise quest states.
            foreach (var q in allQuests)
            {
                if (!questStates.ContainsKey(q.questId))
                {
                    bool hasPrereqs = q.prerequisiteQuestIds != null && q.prerequisiteQuestIds.Length > 0;
                    questStates[q.questId] = hasPrereqs ? QuestStatus.Locked : QuestStatus.Available;
                }
            }
        }

        // ---- Public API ----

        public void AcceptQuest(string questId)
        {
            if (questStates.TryGetValue(questId, out var s) && s == QuestStatus.Available)
            {
                questStates[questId] = QuestStatus.Active;
                Debug.Log($"[QuestManager] Quest accepted: {questId}");
            }
        }

        /// <summary>Called after each extraction — update all active quest progress.</summary>
        public void CheckProgress()
        {
            foreach (var q in allQuests)
            {
                if (questStates.TryGetValue(q.questId, out var s) && s != QuestStatus.Active)
                    continue;

                if (IsQuestComplete(q))
                {
                    questStates[q.questId] = QuestStatus.Completed;
                    Debug.Log($"[QuestManager] Quest complete: {q.questId}");
                }
            }
        }

        public void TurnInQuest(string questId)
        {
            if (questStates.TryGetValue(questId, out var s) && s == QuestStatus.Completed)
            {
                var def = allQuests.Find(q => q.questId == questId);
                if (def != null) GrantRewards(def);

                questStates[questId] = QuestStatus.TurnedIn;

                // Unlock follow-up quests.
                if (def?.unlockQuestIds != null)
                {
                    foreach (var nextId in def.unlockQuestIds)
                    {
                        if (questStates.ContainsKey(nextId))
                            questStates[nextId] = QuestStatus.Available;
                    }
                }
            }
        }

        // ---- Queries ----

        public List<QuestDef> GetAvailableQuests()
        {
            return allQuests.FindAll(q =>
                questStates.TryGetValue(q.questId, out var s) && s == QuestStatus.Available);
        }

        public List<QuestDef> GetActiveQuests()
        {
            return allQuests.FindAll(q =>
                questStates.TryGetValue(q.questId, out var s) && s == QuestStatus.Active);
        }

        // ---- Helpers ----

        private bool IsQuestComplete(QuestDef def)
        {
            if (def?.requirements == null) return true;

            foreach (var req in def.requirements)
            {
                switch (def.type)
                {
                    case QuestType.Collect:
                        if (BaseState.Instance.GetItemCount(req.itemAssetPath) < req.count)
                            return false;
                        break;
                    case QuestType.Eliminate:
                        // Check kill counter.
                        string key = def.questId + "_elim";
                        if (!eliminateProgress.TryGetValue(key, out int kills) || kills < req.count)
                            return false;
                        break;
                }
            }
            return true;
        }

        private void GrantRewards(QuestDef def)
        {
            foreach (var reward in def.rewards)
            {
                if (reward.currencyAmount > 0)
                {
                    // Grant currency (回形针).
                    BaseState.Instance.StoreItem("currency_paperclip", reward.currencyAmount);
                }
                if (!string.IsNullOrEmpty(reward.itemAssetPath))
                {
                    BaseState.Instance.StoreItem(reward.itemAssetPath, reward.count);
                }
                if (reward.isStashUpgrade)
                    BaseState.Instance.UpgradeStash();
                if (reward.isNewWeapon && !string.IsNullOrEmpty(reward.itemAssetPath))
                    BaseState.Instance.AddWeapon(reward.itemAssetPath);
            }
        }

        // ---- Event hooks (called by other systems) ----

        /// <summary>Call when an enemy is killed to track elimination quests.</summary>
        public void OnEnemyKilled(string enemyTag)
        {
            foreach (var q in GetActiveQuests())
            {
                if (q.type != QuestType.Eliminate) continue;
                if (q.requirements == null) continue;

                foreach (var req in q.requirements)
                {
                    if (req.enemyTag == enemyTag)
                    {
                        string key = q.questId + "_elim";
                        eliminateProgress.TryGetValue(key, out int current);
                        eliminateProgress[key] = current + 1;
                    }
                }
            }
        }

        // ---- Phase 1 quest definitions (8 quests) ----

        public static List<QuestDef> CreatePhase1Quests()
        {
            return new List<QuestDef>
            {
                new QuestDef { questId = "Q01", title = "打印纸告急", giverName = "李阿姨",
                    description = "茶水间的打印纸快用完了，从办公区带5卷回来。",
                    type = QuestType.Collect,
                    requirements = new[] { new QuestRequirement { itemAssetPath = "PrinterPaper", count = 5 } },
                    rewards = new[] { new QuestReward { currencyAmount = 50 } },
                    prerequisiteQuestIds = null, unlockQuestIds = new[] { "Q02" } },

                new QuestDef { questId = "Q02", title = "咖啡续命", giverName = "李阿姨",
                    description = "45楼茶水间藏着好咖啡豆，去带回来。",
                    type = QuestType.Extract,
                    requirements = new[] { new QuestRequirement { itemAssetPath = "CoffeeBean", count = 3, targetFloor = 45 } },
                    rewards = new[] { new QuestReward { currencyAmount = 100, itemAssetPath = "CoffeeBean", count = 5 } },
                    prerequisiteQuestIds = new[] { "Q01" }, unlockQuestIds = new[] { "Q03" } },

                new QuestDef { questId = "Q03", title = "清理工位", giverName = "老张",
                    description = "这些KPI丧尸太烦了，消灭10个。",
                    type = QuestType.Eliminate,
                    requirements = new[] { new QuestRequirement { enemyTag = "Enemy", count = 10 } },
                    rewards = new[] { new QuestReward { currencyAmount = 200 } },
                    prerequisiteQuestIds = new[] { "Q02" } },

                new QuestDef { questId = "Q04", title = "解密服务器", giverName = "小王",
                    description = "27楼服务器房有加密数据，找到那个U盘。",
                    type = QuestType.Extract,
                    requirements = new[] { new QuestRequirement { itemAssetPath = "USB", count = 1, targetFloor = 27 } },
                    rewards = new[] { new QuestReward { currencyAmount = 150 } },
                    prerequisiteQuestIds = null, unlockQuestIds = new[] { "Q05" } },

                new QuestDef { questId = "Q05", title = "黑入系统", giverName = "小王",
                    description = "带着U盘去27楼，黑掉服务器然后活着撤离。",
                    type = QuestType.Explore,
                    requirements = new[] { new QuestRequirement { targetFloor = 27 } },
                    rewards = new[] { new QuestReward { currencyAmount = 300, isStashUpgrade = true } },
                    prerequisiteQuestIds = new[] { "Q04" } },

                new QuestDef { questId = "Q06", title = "年终奖名单", giverName = "陈总",
                    description = "35楼财务部有一份年终奖名单，拿到它。",
                    type = QuestType.Extract,
                    requirements = new[] { new QuestRequirement { itemAssetPath = "FinReport", count = 1, targetFloor = 35 } },
                    rewards = new[] { new QuestReward { currencyAmount = 200 } },
                    prerequisiteQuestIds = null, unlockQuestIds = new[] { "Q07" } },

                new QuestDef { questId = "Q07", title = "万能门禁", giverName = "陈总",
                    description = "CEO保险柜里有万能门禁卡——拿到它就能去任何楼层。",
                    type = QuestType.Collect,
                    requirements = new[] { new QuestRequirement { itemAssetPath = "MasterKey", count = 1 } },
                    rewards = new[] { new QuestReward { currencyAmount = 500, isNewWeapon = true } },
                    prerequisiteQuestIds = new[] { "Q06" }, unlockQuestIds = new[] { "Q08" } },

                new QuestDef { questId = "Q08", title = "逃出生天", giverName = "陈总",
                    description = "一切都准备好了。打到1楼大堂，走出这栋楼。",
                    type = QuestType.Explore,
                    requirements = new[] { new QuestRequirement { targetFloor = 1 } },
                    rewards = new[] { new QuestReward { currencyAmount = 1000 } },
                    prerequisiteQuestIds = new[] { "Q07" } },
            };
        }
    }
}
