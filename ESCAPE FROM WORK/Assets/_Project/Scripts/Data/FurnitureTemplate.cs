using UnityEngine;
using EscapeFromWork.Loot;

namespace EscapeFromWork.Data
{
    /// <summary>
    /// All 40 furniture types in the game, organised by room function.
    /// Used by <see cref="FurnitureTemplate"/> to drive loot-container placement.
    /// </summary>
    public enum FurnitureType
    {
        // 办公区 (10)
        EmployeeDesk, SupervisorDesk, FileCabinet, Printer, TrashCan,
        CoatRack, AttendanceBoard, Shredder, WaterDispenser, CubiclePartition,
        // 茶水间 (5)
        CoffeeMachine, Fridge, Microwave, BreakSofa, SnackShelf,
        // 会议室 (4)
        ConferenceTable, Projector, Whiteboard, SpeakerPhone,
        // 服务器/IT (4)
        ServerRack, UPSPower, PatchPanel, ITWorkbench,
        // CEO/高管 (6)
        CEODeck, WineCabinet, Bookshelf, LeatherSofa, OilPainting, Safe,
        // HR/行政 (3)
        ArchiveWall, AdminDesk, KeyCabinet,
        // 财务 (2)
        FinanceDesk, ReportShelf,
        // 前台/大厅 (3)
        ReceptionDesk, WaitingSofa, PlantLarge,
        // 特殊/隐藏 (3)
        CeilingHatch, FishTank, SuggestionBox,
    }

    /// <summary>
    /// ScriptableObject template for a single furniture type.
    /// Defines how the furniture is represented visually (Phase 1: Cube geometry)
    /// and which loot table it uses.
    /// </summary>
    [CreateAssetMenu(menuName = "ESCAPE/Furniture Template")]
    public class FurnitureTemplate : ScriptableObject
    {
        [Header("Identity")]
        public FurnitureType furnitureType;

        [Header("Container")]
        [Tooltip("搜刮容器类型 — determines grid size and interaction UI.")]
        public ContainerType containerType;

        [Tooltip("How many grid cells this furniture occupies (X = width, Y = depth).")]
        public Vector2Int gridSize = Vector2Int.one;

        [Header("Loot")]
        [Tooltip("Loot table for this furniture. Leave null to use default by room type.")]
        public LootTable lootTable;

        [Header("Visual (Phase 2)")]
        [Tooltip("3D prefab. Leave empty for Phase 1 (Cube placeholder).")]
        public GameObject visualPrefab;

        [Tooltip("Phase 1: how to build this with primitives. E.g. 'Desk: 2x1x0.8m top + 4x0.05x0.4m legs'")]
        public string visualDescription;
    }
}
