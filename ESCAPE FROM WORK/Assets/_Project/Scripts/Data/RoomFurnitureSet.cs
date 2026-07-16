using UnityEngine;
using EscapeFromWork.Level;

namespace EscapeFromWork.Data
{
    /// <summary>
    /// Maps a room type to its mandatory and optional furniture placements.
    /// One asset per room type (e.g. "OpenOffice_Furniture").
    /// </summary>
    [CreateAssetMenu(menuName = "ESCAPE/Room Furniture Set")]
    public class RoomFurnitureSet : ScriptableObject
    {
        [Header("Room")]
        [Tooltip("Which room type this furniture set applies to.")]
        public RoomType roomType;

        [Header("Furniture")]
        [Tooltip("Furniture that MUST appear in every room of this type.")]
        public FurniturePlacement[] mandatoryFurniture;

        [Tooltip("Furniture that MAY appear, gated by spawnChance.")]
        public FurniturePlacement[] optionalFurniture;
    }

    /// <summary>
    /// Defines how many of a particular furniture type to place in a room,
    /// whether it can have supervisor/broken variants, and its spawn probability.
    /// </summary>
    [System.Serializable]
    public class FurniturePlacement
    {
        [Tooltip("The furniture template to place.")]
        public FurnitureTemplate template;

        [Tooltip("Minimum number of instances to place.")]
        [Min(0)] public int minCount = 1;

        [Tooltip("Maximum number of instances to place.")]
        [Min(0)] public int maxCount = 2;

        [Tooltip("Probability (0-1) of spawning this furniture at all. 1 = always.")]
        [Range(0f, 1f)] public float spawnChance = 1f;

        [Tooltip("Can one instance be upgraded to a supervisor variant?")]
        public bool canBeSupervisor;

        [Tooltip("Can one instance be downgraded to a broken/ransacked variant?")]
        public bool canBeBroken;
    }
}
