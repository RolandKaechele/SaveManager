using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaveManager.Runtime
{
    // -------------------------------------------------------------------------
    // SaveSlotMetadata
    // -------------------------------------------------------------------------

    /// <summary>
    /// Lightweight header for a save slot displayed in a save/load UI.
    /// Stored as part of the full <see cref="SaveData"/> JSON file.
    /// </summary>
    [Serializable]
    public class SaveSlotMetadata
    {
        /// <summary>Zero-based slot index.</summary>
        public int slotIndex;

        /// <summary>Player-facing slot label (e.g. "Chapter 3 — Forest Path").</summary>
        public string displayName;

        /// <summary>ISO-8601 timestamp of the last save in this slot.</summary>
        public string lastSaveTime;

        /// <summary>Chapter number at the time of save.</summary>
        public int currentChapter;

        /// <summary>Map id at the time of save.</summary>
        public string currentMapId;

        /// <summary>Total play time in seconds accumulated across all sessions.</summary>
        public float playTimeSeconds;
    }

    // -------------------------------------------------------------------------
    // GameFlags
    // -------------------------------------------------------------------------

    /// <summary>
    /// A string-set of game event flags.
    /// Use arbitrary string keys to track story choices, completed objectives, and world state.
    /// </summary>
    [Serializable]
    public class GameFlags
    {
        [SerializeField] private List<string> _set = new();

        /// <summary>Returns true if <paramref name="flag"/> has been set.</summary>
        public bool IsSet(string flag) => _set.Contains(flag);

        /// <summary>Marks <paramref name="flag"/> as set. Idempotent.</summary>
        public void Set(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && !_set.Contains(flag))
                _set.Add(flag);
        }

        /// <summary>Clears <paramref name="flag"/>. No-op if not set.</summary>
        public void Unset(string flag) => _set.Remove(flag);

        /// <summary>Toggle a flag.</summary>
        public void Toggle(string flag)
        {
            if (IsSet(flag)) Unset(flag);
            else Set(flag);
        }

        /// <summary>Read-only view of all set flags.</summary>
        public IReadOnlyList<string> All => _set;

        /// <summary>Remove all flags.</summary>
        public void Clear() => _set.Clear();
    }

    // -------------------------------------------------------------------------
    // CustomDataEntry  (Dictionary replacement for JsonUtility)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Flat key/value pair stored in <see cref="SaveData.customData"/>.
    /// Replaces Dictionary so that <see cref="JsonUtility"/> can serialize the list.
    /// </summary>
    [Serializable]
    public class CustomDataEntry
    {
        public string key;
        public string value;
    }

    // -------------------------------------------------------------------------
    // SaveData
    // -------------------------------------------------------------------------

    /// <summary>
    /// Full data for a single save slot.
    /// Serialized to JSON at <c>persistentDataPath/Saves/slot_{n}.json</c>.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Slot header used by the save/load UI.</summary>
        public SaveSlotMetadata metadata = new();

        /// <summary>Arbitrary game event and story flags.</summary>
        public GameFlags flags = new();

        /// <summary>Current chapter number.</summary>
        public int currentChapter;

        /// <summary>Id of the last visited map.</summary>
        public string currentMapId;

        /// <summary>Total accumulated play time in seconds.</summary>
        public float playTimeSeconds;

        /// <summary>Ids of all maps the player has visited at least once.</summary>
        public List<string> visitedMapIds = new();

        /// <summary>Chapter indices that have been unlocked.</summary>
        public List<int> unlockedChapters = new();

        /// <summary>
        /// Arbitrary key/value strings for plugin-specific or game-specific data
        /// not covered by the built-in fields (e.g. player position, difficulty setting).
        /// </summary>
        public List<CustomDataEntry> customData = new();

        // -------------------------------------------------------------------------
        // Custom data helpers
        // -------------------------------------------------------------------------

        /// <summary>Return the value for <paramref name="key"/>, or null if not set.</summary>
        public string GetCustom(string key)
        {
            foreach (var e in customData)
                if (e.key == key) return e.value;
            return null;
        }

        /// <summary>Set or overwrite a custom key/value pair.</summary>
        public void SetCustom(string key, string value)
        {
            foreach (var e in customData)
            {
                if (e.key != key) continue;
                e.value = value;
                return;
            }
            customData.Add(new CustomDataEntry { key = key, value = value });
        }

        /// <summary>Remove a custom key. No-op if absent.</summary>
        public void RemoveCustom(string key) =>
            customData.RemoveAll(e => e.key == key);
    }
}
