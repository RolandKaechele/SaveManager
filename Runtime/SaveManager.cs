using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SaveManager.Runtime
{
    /// <summary>
    /// <b>SaveManager</b> is the central orchestrator for game progress persistence.
    /// <para>
    /// <b>Responsibilities:</b>
    /// <list type="number">
    /// <item>Manage up to <see cref="MaxSlots"/> JSON save files in <c>persistentDataPath/Saves/</c>.</item>
    /// <item>Track game flags, chapter progress, visited maps, and arbitrary custom data.</item>
    /// <item>Expose events and delegate hooks consumed by bridge components.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Setup:</b> Add to a persistent manager GameObject. No sub-controllers required.
    /// </para>
    /// </summary>
    [AddComponentMenu("SaveManager/Save Manager")]
    [DisallowMultipleComponent]
    public class SaveManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------

        [Header("Slots")]
        [Tooltip("Maximum number of save slots available.")]
        [SerializeField] private int maxSlots = 3;

        [Header("Active slot (read-only at runtime)")]
        [SerializeField] private int activeSlot = 0;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Fired after a slot is saved successfully. Parameter: slot index.</summary>
        public event Action<int> OnSaved;

        /// <summary>Fired after a slot is loaded successfully. Parameter: slot index.</summary>
        public event Action<int> OnLoaded;

        /// <summary>Fired after a slot's file is deleted. Parameter: slot index.</summary>
        public event Action<int> OnDeleted;

        /// <summary>Fired when a game flag changes. Parameters: (flag name, isSet).</summary>
        public event Action<string, bool> OnFlagChanged;

        // -------------------------------------------------------------------------
        // Delegate hooks for bridge components
        // -------------------------------------------------------------------------

        /// <summary>
        /// Optional callback invoked after <see cref="Save(int)"/> completes.
        /// Signature: (slotIndex, saveData). Set by external systems for custom post-save logic.
        /// </summary>
        public Action<int, SaveData> PostSaveCallback;

        /// <summary>
        /// Optional callback invoked after <see cref="Load(int)"/> completes.
        /// Signature: (slotIndex, saveData). Set by external systems for custom post-load logic.
        /// </summary>
        public Action<int, SaveData> PostLoadCallback;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------

        private SaveData _current = new();
        private readonly List<SaveSlotMetadata> _slotHeaders = new();

        /// <summary>The currently active <see cref="SaveData"/> (last loaded or current runtime state).</summary>
        public SaveData Current => _current;

        /// <summary>Index of the currently active slot.</summary>
        public int ActiveSlot => activeSlot;

        /// <summary>Maximum save slots configured for this game.</summary>
        public int MaxSlots => maxSlots;

        // -------------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            RefreshSlotHeaders();
        }

        // -------------------------------------------------------------------------
        // Directory helpers
        // -------------------------------------------------------------------------

        private string SaveDir => Path.Combine(Application.persistentDataPath, "Saves");

        private string SlotPath(int slot) =>
            Path.Combine(SaveDir, $"slot_{slot}.json");

        private void EnsureSaveDir()
        {
            if (!Directory.Exists(SaveDir))
                Directory.CreateDirectory(SaveDir);
        }

        // -------------------------------------------------------------------------
        // Save / Load
        // -------------------------------------------------------------------------

        /// <summary>
        /// Save current game state to <paramref name="slot"/>.
        /// Also updates metadata and refreshes the slot header cache.
        /// </summary>
        public void Save(int slot)
        {
            if (slot < 0 || slot >= maxSlots)
            {
                Debug.LogWarning($"[SaveManager] Slot {slot} is out of range (0–{maxSlots - 1}).");
                return;
            }

            EnsureSaveDir();

            _current.metadata.slotIndex       = slot;
            _current.metadata.lastSaveTime    = DateTime.Now.ToString("o");
            _current.metadata.currentChapter  = _current.currentChapter;
            _current.metadata.currentMapId    = _current.currentMapId;
            _current.metadata.playTimeSeconds = _current.playTimeSeconds;

            if (string.IsNullOrEmpty(_current.metadata.displayName))
                _current.metadata.displayName = $"Chapter {_current.currentChapter}";

            string json = JsonUtility.ToJson(_current, prettyPrint: true);
            try
            {
                File.WriteAllText(SlotPath(slot), json);
                activeSlot = slot;
                RefreshSlotHeaders();
                Debug.Log($"[SaveManager] Saved to slot {slot}.");
                OnSaved?.Invoke(slot);
                PostSaveCallback?.Invoke(slot, _current);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to save slot {slot}: {ex.Message}");
            }
        }

        /// <summary>Save to the currently active slot.</summary>
        public void Save() => Save(activeSlot);

        /// <summary>
        /// Load game state from <paramref name="slot"/>.
        /// Returns true on success; false if no save exists in that slot.
        /// </summary>
        public bool Load(int slot)
        {
            if (slot < 0 || slot >= maxSlots) return false;

            string path = SlotPath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveManager] No save found in slot {slot}.");
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return false;
                _current = data;
                activeSlot = slot;
                Debug.Log($"[SaveManager] Loaded slot {slot} (chapter {_current.currentChapter}, map '{_current.currentMapId}').");
                OnLoaded?.Invoke(slot);
                PostLoadCallback?.Invoke(slot, _current);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Failed to load slot {slot}: {ex.Message}");
                return false;
            }
        }

        /// <summary>Delete the save file for <paramref name="slot"/>.</summary>
        public void Delete(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return;
            File.Delete(path);
            RefreshSlotHeaders();
            Debug.Log($"[SaveManager] Deleted slot {slot}.");
            OnDeleted?.Invoke(slot);
        }

        /// <summary>Returns true if a save file exists for <paramref name="slot"/>.</summary>
        public bool HasSave(int slot) => File.Exists(SlotPath(slot));

        // -------------------------------------------------------------------------
        // Slot headers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns metadata headers for all slots (null entry = empty slot).
        /// </summary>
        public IReadOnlyList<SaveSlotMetadata> GetSlotHeaders() => _slotHeaders;

        private void RefreshSlotHeaders()
        {
            _slotHeaders.Clear();
            for (int i = 0; i < maxSlots; i++)
            {
                string path = SlotPath(i);
                if (!File.Exists(path)) { _slotHeaders.Add(null); continue; }
                try
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    _slotHeaders.Add(data?.metadata);
                }
                catch { _slotHeaders.Add(null); }
            }
        }

        // -------------------------------------------------------------------------
        // Flags
        // -------------------------------------------------------------------------

        /// <summary>Returns true if <paramref name="flag"/> is set in the current save data.</summary>
        public bool IsSet(string flag) => _current.flags.IsSet(flag);

        /// <summary>Set a flag. Fires <see cref="OnFlagChanged"/>.</summary>
        public void SetFlag(string flag)
        {
            _current.flags.Set(flag);
            OnFlagChanged?.Invoke(flag, true);
        }

        /// <summary>Unset a flag. Fires <see cref="OnFlagChanged"/>.</summary>
        public void UnsetFlag(string flag)
        {
            _current.flags.Unset(flag);
            OnFlagChanged?.Invoke(flag, false);
        }

        /// <summary>Toggle a flag. Fires <see cref="OnFlagChanged"/>.</summary>
        public void ToggleFlag(string flag)
        {
            _current.flags.Toggle(flag);
            OnFlagChanged?.Invoke(flag, _current.flags.IsSet(flag));
        }

        // -------------------------------------------------------------------------
        // Progress helpers
        // -------------------------------------------------------------------------

        /// <summary>Record the current chapter number in the active save data.</summary>
        public void SetChapter(int chapter) => _current.currentChapter = chapter;

        /// <summary>Record the current map id and add it to the visited-maps list.</summary>
        public void SetMap(string mapId)
        {
            _current.currentMapId = mapId;
            if (!string.IsNullOrEmpty(mapId) && !_current.visitedMapIds.Contains(mapId))
                _current.visitedMapIds.Add(mapId);
        }

        /// <summary>Whether the player has visited <paramref name="mapId"/> at least once.</summary>
        public bool HasVisited(string mapId) => _current.visitedMapIds.Contains(mapId);

        /// <summary>Add <paramref name="chapter"/> to the unlocked chapters list.</summary>
        public void UnlockChapter(int chapter)
        {
            if (!_current.unlockedChapters.Contains(chapter))
                _current.unlockedChapters.Add(chapter);
        }

        /// <summary>Whether <paramref name="chapter"/> is unlocked.</summary>
        public bool IsChapterUnlocked(int chapter) =>
            _current.unlockedChapters.Contains(chapter);

        // -------------------------------------------------------------------------
        // Custom data
        // -------------------------------------------------------------------------

        /// <summary>Return a custom saved string by key, or null.</summary>
        public string GetCustom(string key) => _current.GetCustom(key);

        /// <summary>Set or overwrite a custom saved string.</summary>
        public void SetCustom(string key, string value) => _current.SetCustom(key, value);

        // -------------------------------------------------------------------------
        // Play time
        // -------------------------------------------------------------------------

        /// <summary>Add <paramref name="seconds"/> to the accumulated play time.</summary>
        public void AddPlayTime(float seconds) => _current.playTimeSeconds += seconds;

        // -------------------------------------------------------------------------
        // New game
        // -------------------------------------------------------------------------

        /// <summary>
        /// Reset the current runtime state to a blank save (new game).
        /// Does not write to disk; call <see cref="Save(int)"/> when ready.
        /// </summary>
        public void NewGame(int slot = 0)
        {
            activeSlot = slot;
            _current = new SaveData();
            _current.metadata.slotIndex    = slot;
            _current.metadata.displayName  = "New Game";
            Debug.Log($"[SaveManager] New game state initialised for slot {slot}.");
        }
    }
}
