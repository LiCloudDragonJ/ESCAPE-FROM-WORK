using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using EscapeFromWork.Player;
using EscapeFromWork.Data;
using EscapeFromWork.Loot;
using EscapeFromWork.Weapons;

namespace EscapeFromWork.UI
{
    public class LootContainerUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text infoText; // single-click item info

        [Header("Equipment (left)")]
        [SerializeField] private Transform equipParent;

        [Header("Backpack Grid (center)")]
        [SerializeField] private Transform bpGridParent;
        [SerializeField] private Text bpLabel;

        [Header("Container Grid (right)")]
        [SerializeField] private Transform contGridParent;
        [SerializeField] private Text contLabel;

        [Header("Prefabs")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private GameObject itemPrefab;

        [Header("Settings")]
        [SerializeField] private float cellSize = 60f;

        private PlayerInventory _inv;
        private PlayerCombat _combat;
        private List<(ItemData item, bool loaded)> _containerSlots = new();
        private int _contW, _contH, _bpW, _bpH;
        private bool _isOpen;
        public bool IsOpen => _isOpen;

        // Drag
        private RectTransform _ghost;
        private ItemData _dragItem;
        private bool _dragFromContainer;
        private bool _dragFromEquipment;
        private GearSlot _dragEquipSlot;
        private int _dragSlotIdx;
        private bool _isDragging;
        private int _dragW, _dragH;

        // Double-click
        private float _lastClickTime;
        private int _lastClickSlot = -1;
        private bool _lastClickWasCtr;

        Color RColor(Rarity r) => r switch
        {
            Rarity.Mythic => new Color(1f, 0.1f, 0.1f), Rarity.Legendary => new Color(1f, 0.6f, 0f),
            Rarity.Epic => new Color(0.7f, 0.3f, 1f), Rarity.Rare => new Color(0.2f, 0.6f, 1f),
            Rarity.Uncommon => new Color(0.3f, 0.8f, 0.3f), _ => new Color(0.7f, 0.7f, 0.7f)
        };

        void Awake() { MakeTemplates(); }
        void Start() { if (panel != null) panel.SetActive(false); }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) { if (!_isOpen) TryOpenBackpack(); else Close(); return; }
            if (_isOpen && Input.GetKeyDown(KeyCode.F)) { TakeAll(); return; }
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            // Move ghost + R rotation
            if (_isDragging && _ghost != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    panel.GetComponent<RectTransform>(), Input.mousePosition, null, out Vector2 lp);
                _ghost.anchoredPosition = lp + new Vector2(cellSize * 0.3f, -cellSize * 0.3f);

                // R key: rotate multi-cell items during drag
                if (Input.GetKeyDown(KeyCode.R) && (_dragItem.Width > 1 || _dragItem.Height > 1))
                {
                    int tmp = _dragW; _dragW = _dragH; _dragH = tmp;
                    _ghost.sizeDelta = new Vector2(_dragW * cellSize - 3, _dragH * cellSize - 3);
                    var gl = _ghost.GetComponentInChildren<Text>();
                    if (gl != null) gl.text = _dragItem.ItemName + (_dragW != _dragItem.Width ? " [旋转]" : "");
                }
            }
        }

        void MakeTemplates()
        {
            if (cellPrefab == null)
            {
                var g = new GameObject("Cell", typeof(Image)); g.SetActive(false); g.transform.SetParent(transform, false);
                var i = g.GetComponent<Image>(); i.color = new Color(0.12f, 0.12f, 0.15f, 0.7f); i.raycastTarget = false;
                var r = g.GetComponent<RectTransform>(); r.sizeDelta = Vector2.one * 50f; cellPrefab = g;
            }
            if (itemPrefab == null)
            {
                var g = new GameObject("Item", typeof(Image)); g.SetActive(false); g.transform.SetParent(transform, false);
                g.GetComponent<Image>().raycastTarget = true;
                var l = new GameObject("L", typeof(Text)); l.transform.SetParent(g.transform, false);
                var t = l.GetComponent<Text>(); t.font = Font.CreateDynamicFontFromOSFont("Arial", 10);
                t.fontSize = 13; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
                var lr = l.GetComponent<RectTransform>(); lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.sizeDelta = Vector2.zero;
                itemPrefab = g;
            }
        }

        // ---- Open ----

        void TryOpenBackpack()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) OpenBackpack(p.GetComponent<PlayerInventory>());
        }

        public void OpenBackpack(PlayerInventory inventory)
        {
            if (inventory == null) return;
            _inv = inventory; _combat = _inv.GetComponent<PlayerCombat>();
            _containerSlots.Clear(); _isOpen = true; _contW = _contH = 0;
            ResolveBP();
            panel.SetActive(true);
            if (titleText) titleText.text = "背包装备";
            if (contLabel) contLabel.text = "---";
            RebuildAll();
        }

        /// <summary>Called by LootContainer — receives loaded + pending lists.</summary>
        public void OpenWithState(List<ItemData> loaded, List<ItemData> pending, PlayerInventory inventory, ContainerType cType, EscapeFromWork.Loot.LootContainer source)
        {
            _sourceContainer = source;
            Open(pending, inventory, cType, loaded);
        }

        /// <summary>Called by LootContainer's loading coroutine when a new item is revealed.</summary>
        public void RefreshFromContainer()
        {
            if (_sourceContainer != null)
            {
                // Reload container state
                var field = typeof(EscapeFromWork.Loot.LootContainer).GetField("_loadedItems",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var loaded = field?.GetValue(_sourceContainer) as List<ItemData>;
                if (loaded != null)
                {
                    _containerSlots.Clear();
                    foreach (var item in loaded) _containerSlots.Add((item, true));
                    RefreshContainer();
                    if (titleText && _sourceContainer != null)
                    {
                        var pField = typeof(EscapeFromWork.Loot.LootContainer).GetField("_pendingItems",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var p = pField?.GetValue(_sourceContainer) as List<ItemData>;
                        int remaining = p?.Count ?? 0;
                        titleText.text = remaining > 0 ? $"搜刮中... {_containerSlots.Count} 件 (剩余 {remaining})" : $"搜刮完成 — {_containerSlots.Count} 件";
                    }
                }
            }
        }

        private EscapeFromWork.Loot.LootContainer _sourceContainer;

        public void Open(List<ItemData> items, PlayerInventory inventory, ContainerType cType, List<ItemData> preloaded = null)
        {
            if (inventory == null) return;
            _inv = inventory; _combat = _inv.GetComponent<PlayerCombat>();
            _containerSlots.Clear(); _isOpen = true;
            (_contW, _contH) = cType switch
            { ContainerType.Desk => (6,4), ContainerType.FilingCabinet => (5,6), ContainerType.Safe => (3,3), ContainerType.SupplyCloset => (8,5), ContainerType.ServerRack => (4,5), ContainerType.CEODesk => (8,6), _ => (6,4) };
            ResolveBP();
            panel.SetActive(true);
            if (titleText) titleText.text = "搜刮中...";
            if (contLabel) contLabel.text = cType switch { ContainerType.Desk => "办公桌 6×4", ContainerType.FilingCabinet => "文件柜 5×6", ContainerType.Safe => "保险柜 3×3", ContainerType.SupplyCloset => "补给柜 8×5", ContainerType.ServerRack => "服务器 4×5", _ => "容器" };
            if (preloaded != null)
                foreach (var item in preloaded) _containerSlots.Add((item, true));
            RebuildAll();
            if (items != null && items.Count > 0)
                StartCoroutine(LoadItems(items));
            else if (titleText) titleText.text = _containerSlots.Count > 0 ? $"搜刮完成 — {_containerSlots.Count} 件" : "容器是空的";
        }

        void ResolveBP()
        {
            _bpW = 6; _bpH = 4;
            if (_combat?.SlotBackpack != null && _combat.SlotBackpack.IsBackpack)
            { _bpW = _combat.SlotBackpack.BackpackWidth; _bpH = _combat.SlotBackpack.BackpackHeight; }
            if (bpLabel) bpLabel.text = $"背包 {_bpW}×{_bpH}";
        }

        void RebuildAll()
        {
            Clear(bpGridParent); if (_bpW > 0) BuildGrid(bpGridParent, _bpW, _bpH);
            Clear(contGridParent); if (_contW > 0) BuildGrid(contGridParent, _contW, _contH);
            RefreshEquip(); RefreshBackpack(); RefreshContainer();
        }

        System.Collections.IEnumerator LoadItems(List<ItemData> items)
        {
            foreach (var item in items)
            {
                float d = item.Rarity switch { Rarity.Mythic => 3f, Rarity.Legendary => 2f, Rarity.Epic => 1f, Rarity.Rare => 0.5f, Rarity.Uncommon => 0.2f, _ => 0.1f };
                yield return new WaitForSeconds(d);
                _containerSlots.Add((item, true));
                RefreshContainer();
                if (titleText) titleText.text = $"搜刮中... {_containerSlots.Count}/{items.Count}";
            }
            if (titleText) titleText.text = _containerSlots.Count > 0 ? $"搜刮完成 — {_containerSlots.Count} 件" : "容器是空的";
        }

        // ---- Refresh ----

        void RefreshEquip()
        {
            if (equipParent == null) return;
            SetEqSlot("SlotWeaponA", "主武器", 0.68f, 0.83f, _combat?.SlotA?.Data?.WeaponName, MakeWeaponItem(_combat?.SlotA?.Data), GearSlot.WeaponA);
            SetEqSlot("SlotWeaponC", "特殊武器", 0.52f, 0.67f, _combat?.SlotC?.Data?.WeaponName, MakeWeaponItem(_combat?.SlotC?.Data), GearSlot.WeaponC);
            SetEqSlot("SlotMelee", "近战", 0.36f, 0.51f, _combat?.SlotMelee?.Data?.WeaponName, MakeWeaponItem(_combat?.SlotMelee?.Data), GearSlot.Melee);
            SetEqSlot("SlotArmor", "护甲", 0.20f, 0.35f, _combat?.SlotArmor?.ItemName, _combat?.SlotArmor, GearSlot.Armor);
            SetEqSlot("SlotBackpack", "背包", 0.04f, 0.19f, _combat?.SlotBackpack?.ItemName, _combat?.SlotBackpack, GearSlot.Backpack);
        }

        ItemData MakeWeaponItem(WeaponData wd)
        {
            if (wd == null) return null;
            // Create a runtime ItemData representing the weapon as a backpack item
            var so = ScriptableObject.CreateInstance<ItemData>();
            so.name = wd.WeaponName;
            // Use private field reflection to set values since properties are read-only
            typeof(ItemData).GetField("itemName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(so, wd.WeaponName);
            typeof(ItemData).GetField("width", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(so, 2);
            typeof(ItemData).GetField("height", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(so, wd.IsMelee ? 1 : 2);
            typeof(ItemData).GetField("baseValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(so, 100);
            return so;
        }

        void SetEqSlot(string name, string label, float y0, float y1, string itemName, ItemData itemData = null, GearSlot gs = GearSlot.None)
        {
            var go = equipParent.Find(name)?.gameObject;
            if (go == null) { go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(equipParent, false); var rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0.05f, y0); rt.anchorMax = new Vector2(0.95f, y1); rt.sizeDelta = Vector2.zero; }
            go.GetComponent<Image>().color = !string.IsNullOrEmpty(itemName) ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.2f, 0.2f, 0.2f, 0.5f);
            var txt = go.GetComponentInChildren<Text>() ?? MakeSlotLabel(go);
            txt.text = !string.IsNullOrEmpty(itemName) ? $"{label}\n{itemName}" : $"{label}\n空";

            var oldEt = go.GetComponent<EventTrigger>();
            if (oldEt != null) Destroy(oldEt);

            if (itemData != null)
            {
                var et = go.AddComponent<EventTrigger>();
                var bd = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
                ItemData capItem = itemData; GearSlot capSlot = gs;
                bd.callback.AddListener((d) => { _isDragging = true; _dragItem = capItem; _dragFromContainer = false; _dragFromEquipment = true; _dragEquipSlot = capSlot; _dragSlotIdx = -1; _dragW = capItem.Width; _dragH = capItem.Height; CreateDragGhost((PointerEventData)d); });
                et.triggers.Add(bd);
                var dg = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
                dg.callback.AddListener((d) => { });
                et.triggers.Add(dg);
                var ed = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
                ed.callback.AddListener((d) => OnEndDrag((PointerEventData)d));
                et.triggers.Add(ed);

                // Accept drops for re-equipping
                var dt = new EventTrigger.Entry { eventID = EventTriggerType.Drop };
                ItemData dropItem = itemData; GearSlot dropSlot = gs;
                dt.callback.AddListener((d) => {
                    if (_isDragging && !_dragFromEquipment)
                    {
                        TryEquipToSlot(_dragItem, dropSlot);
                        _isDragging = false;
                        if (_ghost != null) { Destroy(_ghost.gameObject); _ghost = null; }
                        RefreshEquip(); RefreshBackpack();
                    }
                });
                et.triggers.Add(dt);
            }
            else
            {
                // Empty slot: accept drops from backpack
                var et = go.AddComponent<EventTrigger>();
                var dt = new EventTrigger.Entry { eventID = EventTriggerType.Drop };
                GearSlot capSlot = gs;
                dt.callback.AddListener((d) => {
                    if (_isDragging && !_dragFromEquipment)
                    {
                        TryEquipToSlot(_dragItem, capSlot);
                        _isDragging = false;
                        if (_ghost != null) { Destroy(_ghost.gameObject); _ghost = null; }
                        RefreshEquip(); RefreshBackpack();
                    }
                });
                et.triggers.Add(dt);
            }
        }

        void CreateDragGhost(PointerEventData data)
        {
            _ghost = Instantiate(itemPrefab, panel.transform).GetComponent<RectTransform>();
            _ghost.name = "DragGhost"; _ghost.gameObject.SetActive(true);
            _ghost.GetComponent<Image>().color = new Color(RColor(_dragItem.Rarity).r, RColor(_dragItem.Rarity).g, RColor(_dragItem.Rarity).b, 0.7f);
            _ghost.sizeDelta = new Vector2(_dragW * cellSize - 3, _dragH * cellSize - 3);
            _ghost.pivot = new Vector2(0, 1);
            _ghost.SetAsLastSibling();
            var gl = _ghost.GetComponentInChildren<Text>();
            if (gl != null) gl.text = _dragItem.ItemName;
            Destroy(_ghost.GetComponent<EventTrigger>());
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                panel.GetComponent<RectTransform>(), data.position, data.pressEventCamera, out Vector2 lp);
            _ghost.anchoredPosition = lp + new Vector2(cellSize * 0.3f, -cellSize * 0.3f);
        }

        void TryEquipToSlot(ItemData item, GearSlot targetSlot)
        {
            if (_combat == null || _inv == null || item == null) return;
            string name = item.ItemName;

            if (targetSlot == GearSlot.Armor && _combat.SlotArmor == null && item.GearSlot == GearSlot.Armor)
            { _combat.SetSlotItem(GearSlot.Armor, item); _inv.RemoveItem(item, 1); return; }
            if (targetSlot == GearSlot.Backpack && _combat.SlotBackpack == null && item.GearSlot == GearSlot.Backpack)
            { _combat.SetSlotItem(GearSlot.Backpack, item); _inv.RemoveItem(item, 1); return; }

            // Weapons: check if item name matches a WeaponBase on the player
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var weapons = player.GetComponentsInChildren<WeaponBase>(true);
                foreach (var w in weapons)
                {
                    if (w.Data?.WeaponName != name) continue;
                    bool match = (targetSlot == GearSlot.WeaponA && w.Data.Slot == WeaponSlot.A)
                              || (targetSlot == GearSlot.WeaponC && w.Data.Slot == WeaponSlot.C)
                              || (targetSlot == GearSlot.Melee && w.Data.Slot == WeaponSlot.Melee);
                    if (match) { _combat.SetSlotWeapon(w); _inv.RemoveItem(item, 1); return; }
                }
            }
        }

        void TryEquipItem(ItemData item)
        {
            if (_combat == null || _inv == null || item == null) return;
            string name = item.ItemName;

            if (item.GearSlot == GearSlot.Armor) { if (_combat.SlotArmor == null) { _combat.SetSlotItem(GearSlot.Armor, item); _inv.RemoveItem(item, 1); } return; }
            if (item.GearSlot == GearSlot.Backpack) { if (_combat.SlotBackpack == null) { _combat.SetSlotItem(GearSlot.Backpack, item); _inv.RemoveItem(item, 1); } return; }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var weapons = player.GetComponentsInChildren<WeaponBase>(true);
                foreach (var w in weapons)
                {
                    if (w.Data?.WeaponName == name)
                    {
                        switch (w.Data.Slot)
                        {
                            case WeaponSlot.A: if (_combat.SlotA == null) { _combat.SetSlotWeapon(w); _inv.RemoveItem(item, 1); } break;
                            case WeaponSlot.C: if (_combat.SlotC == null) { _combat.SetSlotWeapon(w); _inv.RemoveItem(item, 1); } break;
                            case WeaponSlot.Melee: if (_combat.SlotMelee == null) { _combat.SetSlotWeapon(w); _inv.RemoveItem(item, 1); } break;
                        }
                        return;
                    }
                }
            }
            if (infoText) infoText.text = $"无法装备: {name}";
        }

        void UnequipItem(ItemData item)
        {
            if (_combat == null || _inv == null) return;
            if (_combat.SlotBackpack == item) { _combat.ClearSlot(GearSlot.Backpack); _inv.AddItem(item, 1); return; }
            if (_combat.SlotArmor == item) { _combat.ClearSlot(GearSlot.Armor); _inv.AddItem(item, 1); return; }
            // Weapons: try to identify by WeaponData name
            string wname = item.ItemName;
            if (_combat.SlotA?.Data?.WeaponName == wname) { _combat.ClearSlot(GearSlot.WeaponA); _inv.AddItem(item, 1); return; }
            if (_combat.SlotC?.Data?.WeaponName == wname) { _combat.ClearSlot(GearSlot.WeaponC); _inv.AddItem(item, 1); return; }
            if (_combat.SlotMelee?.Data?.WeaponName == wname) { _combat.ClearSlot(GearSlot.Melee); _inv.AddItem(item, 1); return; }
        }

        void RefreshContainer()
        {
            ClearItems(contGridParent);
            var taken = new bool[_contW, _contH];
            for (int i = _containerSlots.Count - 1; i >= 0; i--)
                if (_containerSlots[i].loaded)
                    PlaceItem(contGridParent, _containerSlots[i].item, taken, _contW, _contH, true, i);
        }

        void RefreshBackpack()
        {
            ClearItems(bpGridParent);
            if (_inv == null) return;
            var taken = new bool[_bpW, _bpH];
            var slots = _inv.GetBackpack();
            for (int s = slots.Count - 1; s >= 0; s--)
                if (!slots[s].IsEmpty && slots[s].item != null)
                    PlaceItem(bpGridParent, slots[s].item, taken, _bpW, _bpH, false, -1, slots[s].count);
        }

        void PlaceItem(Transform parent, ItemData item, bool[,] taken, int gw, int gh, bool isCtr, int idx, int stackCount = 1)
        {
            for (int y = 0; y <= gh - item.Height; y++)
                for (int x = 0; x <= gw - item.Width; x++)
                {
                    if (!CanFit(taken, x, y, item.Width, item.Height, gw, gh)) continue;

                    var go = Instantiate(itemPrefab, parent);
                    go.name = isCtr ? $"Item_{idx}" : $"BP_{item.ItemName}";
                    go.SetActive(true);
                    go.GetComponent<Image>().color = RColor(item.Rarity);
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    int iw = item.Width, ih = item.Height;
                    rt.anchoredPosition = new Vector2(
                        (x - gw / 2f) * cellSize + iw * cellSize / 2f,
                        -(y - gh / 2f) * cellSize - ih * cellSize / 2f);
                    rt.sizeDelta = new Vector2(iw * cellSize - 3, ih * cellSize - 3);
                    var lbl = go.GetComponentInChildren<Text>();
                    if (lbl != null) lbl.text = stackCount > 1 ? $"{item.ItemName} ×{stackCount}" : item.ItemName;

                    // Interaction: BeginDrag + PointerClick (for single/double click)
                    var et = go.AddComponent<EventTrigger>();

                    var pd = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                    int capIdx = idx; bool capCtr = isCtr; ItemData capItem = item;
                    if (capCtr)
                        pd.callback.AddListener((d) => OnPointerDown((PointerEventData)d, capCtr, capIdx, capItem));
                    else
                        pd.callback.AddListener((d) => { if (infoText) infoText.text = $"<b>{capItem.ItemName}</b>\n{capItem.Rarity}\n价值: {capItem.BaseValue} 回形针\n尺寸: {capItem.Width}×{capItem.Height}\n{capItem.Description}"; });
                    et.triggers.Add(pd);

                    var bd = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
                    bd.callback.AddListener((d) => OnBeginDrag((PointerEventData)d, capItem, capCtr, capIdx));
                    et.triggers.Add(bd);

                    var dg = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
                    dg.callback.AddListener((d) => { }); // handled in Update
                    et.triggers.Add(dg);

                    var ed = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
                    ed.callback.AddListener((d) => OnEndDrag((PointerEventData)d));
                    et.triggers.Add(ed);

                    Fill(taken, x, y, item.Width, item.Height);
                    return;
                }
        }

        // ---- Pointer Events ----

        void OnPointerDown(PointerEventData data, bool fromCtr, int idx, ItemData item)
        {
            float t = Time.time;
            if (fromCtr && _lastClickWasCtr && _lastClickSlot == idx && t - _lastClickTime < 0.35f)
            {
                // Double-click → instant transfer
                _lastClickTime = 0; _lastClickSlot = -1;
                QuickXfer(idx, item);
            }
            else
            {
                _lastClickTime = t; _lastClickSlot = idx; _lastClickWasCtr = fromCtr;
                // Single click → show info
                if (infoText != null)
                    infoText.text = $"<b>{item.ItemName}</b>\n{(item.Rarity)}\n价值: {item.BaseValue} 回形针\n尺寸: {item.Width}×{item.Height}\n{item.Description}";
            }
        }

        void QuickXfer(int idx, ItemData item)
        {
            if (idx < 0 || idx >= _containerSlots.Count) return;
            if (_inv != null && _inv.AddItem(item, 1))
            {
                _sourceContainer?.OnItemTransferred(item);
                _containerSlots.RemoveAt(idx);
                RefreshContainer(); RefreshBackpack();
                if (infoText) infoText.text = $"已转移: {item.ItemName}";
            }
            else if (infoText) infoText.text = "背包已满!";
        }

        void DropItem(ItemData item)
        {
            if (_inv == null || item == null) return;
            if (_inv.RemoveItem(item, 1))
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Drop_{item.ItemName}";
                    go.transform.position = p.transform.position + Vector3.forward * 2f + Vector3.up * 0.5f;
                    go.transform.localScale = new Vector3(item.Width * 0.3f, 0.2f, item.Height * 0.3f);
                    go.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    go.GetComponent<MeshRenderer>().material.color = RColor(item.Rarity);
                    var pu = go.AddComponent<EscapeFromWork.Loot.PickupItem>();
                    pu.Initialize(item, 1);
                    go.AddComponent<EscapeFromWork.Loot.LooseLootBob>();
                }
                RefreshBackpack(); RefreshEquip();
                if (infoText) infoText.text = $"丢弃: {item.ItemName}";
            }
        }

        void OnBeginDrag(PointerEventData data, ItemData item, bool fromCtr, int idx, bool fromEquip = false)
        {
            _isDragging = true;
            _dragItem = item; _dragFromContainer = fromCtr; _dragFromEquipment = fromEquip; _dragSlotIdx = idx;
            _dragW = item.Width; _dragH = item.Height;

            // Create ghost
            _ghost = Instantiate(itemPrefab, panel.transform).GetComponent<RectTransform>();
            _ghost.name = "DragGhost"; _ghost.gameObject.SetActive(true);
            _ghost.GetComponent<Image>().color = new Color(RColor(item.Rarity).r, RColor(item.Rarity).g, RColor(item.Rarity).b, 0.7f);
            _ghost.sizeDelta = new Vector2(_dragW * cellSize - 3, _dragH * cellSize - 3);
            _ghost.pivot = new Vector2(0, 1);
            _ghost.SetAsLastSibling();
            var gl = _ghost.GetComponentInChildren<Text>();
            if (gl != null) gl.text = item.ItemName;
            Destroy(_ghost.GetComponent<EventTrigger>());
            // Move to mouse
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                panel.GetComponent<RectTransform>(), data.position, data.pressEventCamera, out Vector2 lp);
            _ghost.anchoredPosition = lp + new Vector2(cellSize * 0.3f, -cellSize * 0.3f);
        }

        void OnEndDrag(PointerEventData data)
        {
            if (!_isDragging) return;
            _isDragging = false;
            if (_ghost != null) { Destroy(_ghost.gameObject); _ghost = null; }

            // Check if dropped on "DropZone"
            var dz = GameObject.Find("DropZone");
            if (dz != null && RectTransformUtility.RectangleContainsScreenPoint(
                dz.GetComponent<RectTransform>(), data.position, data.pressEventCamera))
            {
                DropItem(_dragItem);
                RefreshContainer(); RefreshBackpack();
                return;
            }

            // Determine which grid the drop is on
            var targetGrid = GetDropTargetGrid(data.position);
            bool isBp = targetGrid == bpGridParent;

            // Backpack → Equipment panel: re-equip (equip panel not a grid target)
            if (!_dragFromContainer && !_dragFromEquipment && targetGrid == null)
            {
                TryEquipItem(_dragItem);
                RefreshContainer(); RefreshBackpack(); RefreshEquip();
                return;
            }

            if (targetGrid == null) { RefreshContainer(); RefreshBackpack(); return; }

            // Container → Backpack: transfer
            if (_dragFromContainer && isBp && _dragSlotIdx >= 0 && _dragSlotIdx < _containerSlots.Count)
            {
                if (_inv != null && _inv.AddItem(_dragItem, 1))
                {
                    _sourceContainer?.OnItemTransferred(_dragItem);
                    _containerSlots.RemoveAt(_dragSlotIdx);
                    if (infoText) infoText.text = $"已转移: {_dragItem.ItemName}";
                }
                else if (infoText) infoText.text = "背包已满!";
            }
            // Equipment → Backpack: unequip
            else if (_dragFromEquipment && _inv != null)
            {
                UnequipItem(_dragItem);
                if (infoText) infoText.text = $"卸下: {_dragItem.ItemName}";
            }
            // Backpack → Equipment panel: re-equip
            else if (!_dragFromContainer && !_dragFromEquipment && targetGrid == null)
            {
                // Check if dropped on equipment panel
                if (dz == null) // not drop zone either
                {
                    TryEquipItem(_dragItem);
                }
            }

            RefreshContainer(); RefreshBackpack(); RefreshEquip();
        }

        Transform GetDropTargetGrid(Vector2 screenPos)
        {
            // Check which grid the mouse is over
            var bpRt = bpGridParent.GetComponent<RectTransform>();
            if (RectTransformUtility.RectangleContainsScreenPoint(bpRt, screenPos, null))
                return bpGridParent;
            var cRt = contGridParent.GetComponent<RectTransform>();
            if (RectTransformUtility.RectangleContainsScreenPoint(cRt, screenPos, null))
                return contGridParent;
            return null;
        }

        // ---- Helpers ----

        bool CanFit(bool[,] g, int x, int y, int w, int h, int gw, int gh)
        {
            if (x + w > gw || y + h > gh) return false;
            for (int iy = y; iy < y + h; iy++)
                for (int ix = x; ix < x + w; ix++)
                    if (g[ix, iy]) return false;
            return true;
        }

        void Fill(bool[,] g, int x, int y, int w, int h)
        { for (int iy = y; iy < y + h; iy++) for (int ix = x; ix < x + w; ix++) g[ix, iy] = true; }

        void TakeAll()
        {
            if (_inv == null || _containerSlots.Count == 0) return;
            // Sort by rarity descending
            var sorted = new List<(ItemData item, bool loaded)>(_containerSlots);
            sorted.Sort((a, b) => b.item.Rarity.CompareTo(a.item.Rarity));
            int taken = 0, skipped = 0;
            var remaining = new List<(ItemData, bool)>();
            foreach (var (item, loaded) in sorted)
            {
                if (!loaded || item == null) continue;
                if (_inv.AddItem(item, 1)) { _sourceContainer?.OnItemTransferred(item); taken++; }
                else { remaining.Add((item, loaded)); skipped++; }
            }
            _containerSlots = remaining;
            RefreshContainer(); RefreshBackpack();
            string msg = taken > 0 ? $"转移 {taken} 件" : "";
            if (skipped > 0) msg += $"  (背包满, {skipped} 件未取)";
            if (infoText) infoText.text = msg;
            if (_containerSlots.Count == 0 && titleText) titleText.text = "容器已空";
        }

        void Close()
        {
            _isOpen = false;
            if (_isDragging) { _isDragging = false; if (_ghost != null) { Destroy(_ghost.gameObject); _ghost = null; } }
            if (panel != null) panel.SetActive(false);
            Clear(bpGridParent); Clear(contGridParent);
        }

        Text MakeSlotLabel(GameObject parent)
        {
            var tgo = new GameObject("L", typeof(RectTransform), typeof(Text));
            tgo.transform.SetParent(parent.transform, false);
            var t = tgo.GetComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", 12); t.fontSize = 14;
            t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var tr = tgo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
            return t;
        }

        void Clear(Transform p) { foreach (Transform c in p) Destroy(c.gameObject); }
        void ClearItems(Transform p) { foreach (Transform c in p) if (!c.name.StartsWith("C")) Destroy(c.gameObject); }

        void BuildGrid(Transform parent, int w, int h)
        {
            float cs = cellSize;
            var prt = parent.GetComponent<RectTransform>();
            // Keep parent anchors as set by layout (don't override!)
            // Size the grid to content, centered within zone via pivot
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(w * cs, h * cs);
            prt.anchoredPosition = Vector2.zero;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var go = Instantiate(cellPrefab, parent); go.name = $"C{x}_{y}"; go.SetActive(true);
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2((x - w/2f + 0.5f) * cs, -(y - h/2f + 0.5f) * cs);
                    rt.sizeDelta = new Vector2(cs, cs);
                }
        }
    }
}
