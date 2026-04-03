# SaveManager

A modular save system for Unity.  
Manages multiple save slots with JSON persistence, game-event flags, chapter progress, visited maps, and arbitrary custom data.  
Optionally integrates with [MapLoaderFramework](https://github.com/RolandKaechele/MapLoaderFramework) for automatic map and chapter tracking.


## Features

- **Multiple save slots** — configurable slot count; each slot is a separate JSON file in `persistentDataPath/Saves/`
- **Game flags** — string-key event flags for tracking story choices, completed objectives, and world state
- **Chapter progress** — records current chapter; auto-unlocks chapters as the player reaches them
- **Visited maps** — remembers every map the player has visited
- **Custom data** — arbitrary key/value strings for plugin-specific or game-specific state
- **Play time tracking** — accumulates play time across sessions
- **Screenshot capture** — optionally captures a scaled thumbnail of the scene on every save; PNG stored alongside the slot JSON
- **Auto-save** — optionally save automatically on chapter change (requires `SAVEMANAGER_MLF`)
- **MapLoaderFramework integration** — `MapLoaderSaveBridge` subscribes to `OnMapLoaded` and `OnChapterChanged` to track progress automatically (activated via `SAVEMANAGER_MLF`)
- **CutsceneManager integration** — `SaveCutsceneBridge` (in the CutsceneManager package) records seen sequences as save flags to prevent repeated cutscenes (activated via `CUTSCENEMANAGER_SM`)
- **EventManager integration** — `SaveEventBridge` (in the EventManager package) re-broadcasts `OnSaved`, `OnLoaded`, `OnDeleted`, and `OnFlagChanged` as named `GameEvent`s (activated via `EVENTMANAGER_SM`)
- **Custom Inspector** — slot status table with screenshot thumbnails, save/load/delete buttons, flag checker


## Installation

### Option A — Unity Package Manager (Git URL)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Enter:

   ```
   https://github.com/RolandKaechele/SaveManager.git
   ```

### Option B — Clone into Assets

```bash
git clone https://github.com/RolandKaechele/SaveManager.git Assets/SaveManager
```

### Option C — Manual copy

Copy the `SaveManager/` folder into your project's `Assets/` directory.


## Folder Structure

After installation the post-install script creates:

```
Assets/
  Scripts/          ← Lua scripts (used with MapLoaderFramework integration)
```

Save files are written at runtime to `Application.persistentDataPath/Saves/`.


## Quick Start

### 1. Add SaveManager to your scene

Create a persistent GameObject, then add:

| Component | Purpose |
| --------- | ------- |
| `SaveManager` | Main orchestrator (required) |
| `MapLoaderSaveBridge` | Auto-tracking of map/chapter (optional, requires `SAVEMANAGER_MLF`) |

**Screenshot Inspector fields** (on `SaveManager`):

| Field | Default | Description |
| ----- | ------- | ----------- |
| `captureScreenshotOnSave` | `true` | Capture a screenshot after each save |
| `screenshotWidth` | `320` | Thumbnail width in pixels; height is scaled proportionally. Set to `0` for full resolution |

### 2. New game / load

```csharp
SaveManager.Runtime.SaveManager save = FindObjectOfType<SaveManager.Runtime.SaveManager>();

// Start a fresh game in slot 0
save.NewGame(0);

// Or load from an existing slot
save.Load(0);
```

### 3. Save progress

```csharp
// Save to the active slot
save.Save();

// Save to a specific slot
save.Save(2);
```

### 4. Flags

```csharp
save.SetFlag("met_commander_ross");

if (save.IsSet("chapter_01_completed"))
    Debug.Log("Chapter 1 done.");

save.UnsetFlag("temp_flag");
```

### 5. Custom data

```csharp
save.SetCustom("last_choice", "helped_engineer");
string choice = save.GetCustom("last_choice");
```


## MapLoaderFramework Integration

SaveManager can automatically track the current map and chapter in **MapLoaderFramework**.

### Enable

1. Add `SAVEMANAGER_MLF` to **Edit → Project Settings → Player → Scripting Define Symbols**
2. Attach `MapLoaderSaveBridge` to any GameObject in your scene

`MapLoaderSaveBridge.Awake()` subscribes to `OnMapLoaded` and `OnChapterChanged`. When `autoSaveOnChapterChange` is enabled (default: true), the active slot is saved every time a new chapter loads.

```csharp
// Manual save after a cutscene completes
save.Save();
```


## Runtime API

### `SaveManager`

| Member | Description |
| ------ | ----------- |
| `NewGame(slot)` | Reset runtime state to defaults (does not write to disk) |
| `Save(slot)` | Serialize and write save data to slot JSON |
| `Save()` | Save to the active slot |
| `Load(slot)` | Deserialize slot JSON; returns true on success |
| `Delete(slot)` | Delete the save file for a slot |
| `HasSave(slot)` | True if a save file exists for that slot |
| `GetSlotHeaders()` | List of `SaveSlotMetadata` (null = empty slot) |
| `IsSet(flag)` | True if the flag is set |
| `SetFlag(flag)` | Set a game flag |
| `UnsetFlag(flag)` | Unset a game flag |
| `ToggleFlag(flag)` | Toggle a game flag |
| `SetChapter(n)` | Record the current chapter number |
| `SetMap(mapId)` | Record the current map id and add to visited list |
| `HasVisited(mapId)` | True if the map has been visited |
| `UnlockChapter(n)` | Add a chapter to the unlocked list |
| `IsChapterUnlocked(n)` | True if a chapter is unlocked |
| `GetCustom(key)` | Return a custom saved string |
| `SetCustom(key, value)` | Set a custom saved string |
| `AddPlayTime(seconds)` | Accumulate play time |
| `GetScreenshot(slot)` | Load saved thumbnail as `Texture2D`; returns null if none exists. Caller must call `Destroy()` on the texture when done |
| `Current` | The active `SaveData` object |
| `ActiveSlot` | Currently active slot index |
| `OnSaved` | `event Action<int>` — fires after save |
| `OnLoaded` | `event Action<int>` — fires after load |
| `OnDeleted` | `event Action<int>` — fires after delete |
| `OnFlagChanged` | `event Action<string, bool>` — fires on flag change |
| `PostSaveCallback` | `Action<int, SaveData>` delegate — post-save hook |
| `PostLoadCallback` | `Action<int, SaveData>` delegate — post-load hook |

### `MapLoaderSaveBridge` *(requires `SAVEMANAGER_MLF`)*

| Member | Description |
| ------ | ----------- |
| `autoSaveOnChapterChange` | If true, auto-saves when chapter changes (Inspector toggle) |


## Save File Format

Save files are stored as pretty-printed JSON at:

```
Application.persistentDataPath/
  Saves/
    slot_0.json
    slot_0.png      ← screenshot thumbnail (if captureScreenshotOnSave is enabled)
    slot_1.json
    slot_1.png
    slot_2.json
    slot_2.png
```

Example `slot_0.json`:

```json
{
  "metadata": {
    "slotIndex": 0,
    "displayName": "Chapter 3",
    "lastSaveTime": "2026-04-02T14:30:00",
    "currentChapter": 3,
    "currentMapId": "forest_path",
    "playTimeSeconds": 3720.5
  },
  "flags": {
    "_set": ["chapter_01_completed", "met_commander_ross"]
  },
  "currentChapter": 3,
  "currentMapId": "forest_path",
  "visitedMapIds": ["town_square", "forest_path"],
  "unlockedChapters": [1, 2, 3],
  "customData": [
    { "key": "last_choice", "value": "helped_engineer" }
  ]
}
```


## Examples

| File | Description |
| ---- | ----------- |
| `Scripts/example_save_trigger.lua` | Lua trigger for setting flags and saving via MapLoaderFramework |


## Dependencies

| Dependency | Required | Notes |
| ---------- | -------- | ----- |
| Unity 2022.3+ | ✓ | |
| MapLoaderFramework | optional | Required when `SAVEMANAGER_MLF` is defined |
| MoonSharp | optional | Required for Lua-triggered saves (included via MapLoaderFramework) |
| CutsceneManager | optional | `SaveCutsceneBridge` lives there — enable `CUTSCENEMANAGER_SM` |
| EventManager | optional | `SaveEventBridge` lives there — enable `EVENTMANAGER_SM` |


## Repository

[https://github.com/RolandKaechele/SaveManager](https://github.com/RolandKaechele/SaveManager)


## License

MIT — see [LICENSE](LICENSE).
