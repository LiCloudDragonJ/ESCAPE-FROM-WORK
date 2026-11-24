using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.Level
{
    /// <summary>
    /// Persistent per-floor data that survives scene reloads via a static dictionary.
    /// Tracks whether a floor has been cleared, when it was last visited,
    /// which loot containers have already been opened, and applies a
    /// time-based loot decay penalty for repeated visits within a 24-hour window.
    ///
    /// <para>This data is ephemeral for the current play session (runtime only).
    /// A Save() placeholder is provided for future serialization to disk.</para>
    /// </summary>
    [System.Serializable]
    public class FloorState
    {
        // ---- Instance fields ----------------------------------------------------

        /// <summary>Which floor this state tracks (1 = top, 50 = ground/lobby).</summary>
        public int floorNumber;

        /// <summary>True when all enemies on this floor have been eliminated.</summary>
        public bool isCleared;

        /// <summary>Real-world time when the floor was last cleared.</summary>
        public DateTime clearedTime;

        /// <summary>Real-world time when the player last entered this floor.</summary>
        public DateTime lastEntryTime;

        /// <summary>
        /// Number of times the player has entered this floor within the
        /// current 24-hour rolling window. Drives loot decay.
        /// </summary>
        public int consecutiveVisits24h;

        /// <summary>
        /// Set of container instance IDs that have already been looted
        /// on this floor. Prevents double-looting.
        /// </summary>
        public HashSet<int> lootedContainerIds = new HashSet<int>();

        // ---- Static cache -------------------------------------------------------

        /// <summary>
        /// In-memory cache of all floor states for the current session,
        /// keyed by floor number. Survives scene reloads; does not survive
        /// application quit (placeholder until serialization is added).
        /// </summary>
        private static Dictionary<int, FloorState> _allFloors = new Dictionary<int, FloorState>();

        // ---- Static factory -----------------------------------------------------

        /// <summary>
        /// Return the <see cref="FloorState"/> for the given floor number,
        /// creating a new one if none has been cached yet.
        /// </summary>
        /// <param name="floorNum">Floor number (1-50).</param>
        /// <returns>The cached or newly created state.</returns>
        public static FloorState LoadOrCreate(int floorNum)
        {
            if (_allFloors.TryGetValue(floorNum, out FloorState existing))
                return existing;

            FloorState state = new FloorState
            {
                floorNumber = floorNum,
                isCleared = false,
                clearedTime = DateTime.MinValue,
                lastEntryTime = DateTime.UtcNow,
                consecutiveVisits24h = 1
            };

            _allFloors[floorNum] = state;
            return state;
        }

        /// <summary>
        /// Clear the in-memory cache for all floors. Useful when starting
        /// a completely new run so stale data does not carry over.
        /// </summary>
        public static void ClearAllStates()
        {
            _allFloors.Clear();
        }

        // ---- Instance methods ---------------------------------------------------

        /// <summary>
        /// Calculate a loot multiplier that decays based on repeat visits
        /// within a 24-hour window.
        ///
        /// <para>Safe floors (not yet cleared) always return 1.0 (full loot).
        /// After the first clear, each subsequent visit within 24 hours
        /// reduces loot by 25% multiplicatively.</para>
        /// </summary>
        /// <returns>Loot multiplier in the range [0, 1].</returns>
        public float GetLootDecayMultiplier()
        {
            // Full loot until the floor has been cleared at least once.
            if (!isCleared)
                return 1f;

            // After clearing, each repeat visit within 24h reduces loot by 25%.
            // Formula: 1.0 * (0.75 ^ (visits - 1))
            // Visit 1 (first clear):   0.75^(0) = 1.00
            // Visit 2:                 0.75^(1) = 0.75
            // Visit 3:                 0.75^(2) = 0.56
            // Visit 4:                 0.75^(3) = 0.42
            // Visit 5:                 0.75^(4) = 0.31
            int extraVisits = Math.Max(0, consecutiveVisits24h - 1);
            float multiplier = Mathf.Pow(0.75f, (float)extraVisits);
            return Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// Determine whether loot containers on this floor should be refreshed.
        /// Returns true when the floor has been cleared AND at least 4 hours
        /// have passed since the last refresh (or never refreshed).
        ///
        /// <para>Used by the loot spawning system to decide whether to
        /// re-populate containers that were previously emptied.</para>
        /// </summary>
        /// <returns>True if loot should be respawned.</returns>
        public bool ShouldRefreshLoot()
        {
            if (!isCleared)
                return false;

            // If clearedTime has never been set (DateTime.MinValue), assume
            // loot has never been placed - allow refresh.
            if (clearedTime == DateTime.MinValue)
                return true;

            TimeSpan elapsed = DateTime.UtcNow - clearedTime;
            return elapsed.TotalHours >= 4.0;
        }

        /// <summary>
        /// Check whether a specific container has already been looted.
        /// </summary>
        public bool IsContainerLooted(int containerId)
        {
            return lootedContainerIds.Contains(containerId);
        }

        /// <summary>
        /// Record that a specific container has been looted, persisting
        /// across room re-entries within the same floor instance.
        /// </summary>
        public void MarkContainerLooted(int containerId)
        {
            lootedContainerIds.Add(containerId);
        }

        /// <summary>
        /// Mark the floor as cleared and update all timestamps.
        /// Call this when the last enemy on the floor is eliminated.
        /// </summary>
        public void MarkCleared()
        {
            // Only count consecutive visits when the floor was already cleared.
            if (isCleared)
            {
                consecutiveVisits24h++;
            }

            isCleared = true;
            clearedTime = DateTime.UtcNow;
            lastEntryTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Record the player entering this floor and update the visit counter.
        /// Call this at the start of each floor entry.
        /// </summary>
        public void RecordEntry()
        {
            // Check whether we are still within the 24-hour window for
            // consecutive visits. If the last entry was more than 24 hours ago,
            // reset the counter.
            if (lastEntryTime != DateTime.MinValue)
            {
                TimeSpan sinceLast = DateTime.UtcNow - lastEntryTime;
                if (sinceLast.TotalHours >= 24.0)
                {
                    consecutiveVisits24h = 0;
                }
            }

            consecutiveVisits24h++;
            lastEntryTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Placeholder for future serialization to disk.
        /// Currently stores state only in the static dictionary
        /// (lost on application quit). Replace with JSON/binary
        /// serialization when the save system is implemented.
        /// </summary>
        public void Save()
        {
            // TODO: Serialize _allFloors to persistent storage (e.g. JSON to
            // Application.persistentDataPath). The static dictionary is the
            // authoritative source; individual FloorState.Save() is a no-op
            // until the serialization format is defined.
        }
    }
}
