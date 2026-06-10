# Save & Load System

A reusable, production-ready **multi-slot save system** for Unity: atomic writes,
optional AES-256 encryption, auto-backup, auto-save, and a clean `ISaveable`
interface so any MonoBehaviour can opt in to save/load with zero coupling.

Part of the **HelperTools** framework → System #13 *Save & Load Framework* (Phase 1).

---

## ✨ Features

- **Multi-slot** — 3 configurable save slots (change `SlotCount` constant).
- **Atomic writes** — write to `.tmp` → rename over `.json`; old file auto-becomes `.bak`.
- **Auto-backup** — if the primary file is corrupt, the `.bak` is silently promoted.
- **AES-256 encryption** — toggle in the inspector; prevents casual save editing.
- **Auto-save** — periodic (interval + `OnApplicationPause` for mobile safety).
- **`ISaveable` interface** — any MonoBehaviour registers itself; no god-object coupling.
- **Play-time tracking** — accumulated `totalPlayTime` survives load/save cycles.
- **Editor tools** — open save folder, print info, delete all (Tools ▸ Save Manager).
- **Drag-and-drop prefab** — `SaveManager.prefab` + `SaveManagerScene.unity` demo.

---

## 🧱 Architecture

| Piece | Script | Responsibility |
|-------|--------|----------------|
| **DTOs** | `SaveDataModel` | `ISaveable`, `GameSaveData`, `PlayerSaveData`, `SaveSlotInfo` |
| **IO** | `SaveFileIO` | Atomic read/write, AES-256, `.bak` auto-restore |
| **Manager** | `SaveManager` | Singleton, multi-slot, auto-save, play-time tracking |
| **UI** | `SaveSlotUI` | Per-slot card (name / timestamp / playtime / Save-Load-Delete) |
| **Editor** | `SaveManagerMenu` | Tools ▸ Save Manager (open folder, print info, delete all) |

---

## 🚀 Quick start

### 1. Add the prefab
Drop **`SaveManager.prefab`** into your first/persistent scene.
Configure in Inspector:
- `Encrypt` — AES-256 on/off
- `Auto Save Interval` — seconds between auto-saves (0 = disabled)
- `Auto Save Slot` — which slot receives periodic auto-saves

### 2. Implement ISaveable
```csharp
using UnityEngine;
using SaveSystem;

public class PlayerController : MonoBehaviour, ISaveable
{
    public int score;
    public int level = 1;

    private void Awake()  => SaveManager.Instance.Register(this);
    private void OnDestroy() => SaveManager.Instance?.Unregister(this);

    public void GatherSaveData(GameSaveData data)
    {
        data.player.score = score;
        data.player.level = level;
        data.player.SetPosition(transform.position);
    }

    public void ApplySaveData(GameSaveData data)
    {
        score = data.player.score;
        level = data.player.level;
        transform.position = data.player.GetPosition();
    }
}
```

### 3. Save / Load from UI or code
```csharp
SaveManager.Instance.Save(0);           // save to slot 0
SaveManager.Instance.Load(0);           // load from slot 0
SaveManager.Instance.Delete(0);         // delete slot 0
bool exists = SaveManager.Instance.HasSave(0);
SaveSlotInfo info = SaveManager.Instance.GetSlotInfo(0);
```

### 4. React to save/load events
```csharp
void OnEnable()
{
    SaveManager.OnSaved   += slot => Debug.Log($"Saved slot {slot}");
    SaveManager.OnLoaded  += slot => Debug.Log($"Loaded slot {slot}");
    SaveManager.OnDeleted += slot => Debug.Log($"Deleted slot {slot}");
}
void OnDisable()
{
    SaveManager.OnSaved   -= ...;
    SaveManager.OnLoaded  -= ...;
    SaveManager.OnDeleted -= ...;
}
```

---

## 📚 Example use cases

### 1. Three-slot save screen *(simple)*

```csharp
using SaveSystem;
using UnityEngine;

public class SaveScreen : MonoBehaviour
{
    // Assign 3 SaveSlotUI components in the Inspector (one per card).
    // They wire themselves automatically via SaveManager events — nothing else needed.
}
```
> Drop `SaveManager.prefab` + add three `SaveSlotUI` components to your UI cards.
> Each card has Save / Load / Delete buttons that self-enable based on slot state.

---

### 2. Auto-save on checkpoint *(simple)*

```csharp
using SaveSystem;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            SaveManager.Instance.Save(slotIndex: 0);
    }
}
```

---

### 3. New Game / Continue flow *(detailed)*

```csharp
using SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void OnContinueClicked()
    {
        // Load the most recently written slot
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            if (SaveManager.Instance.HasSave(i))
            {
                SaveManager.Instance.Load(i);
                SceneManager.LoadScene("GameScene");
                return;
            }
        }
        Debug.LogWarning("No save found — start a new game instead.");
    }

    public void OnNewGameClicked()
    {
        // Delete slot 0 and start fresh
        SaveManager.Instance.Delete(0);
        SceneManager.LoadScene("GameScene");
    }
}
```

---

### 4. Show slot info in the UI *(detailed)*

```csharp
using SaveSystem;
using TMPro;
using UnityEngine;

public class SlotPreview : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameLabel;
    [SerializeField] private TMP_Text _dateLabel;
    [SerializeField] private TMP_Text _timeLabel;

    public void ShowSlot(int slotIndex)
    {
        var info = SaveManager.Instance.GetSlotInfo(slotIndex);
        if (!info.exists) { _nameLabel.text = "— Empty —"; return; }

        _nameLabel.text = info.slotName;
        _dateLabel.text = info.Timestamp.ToString("MMM dd, yyyy  HH:mm");
        _timeLabel.text = info.FormattedPlayTime;   // "2h 05m"
    }
}
```

---

### 5. Encrypted save for competitive / leaderboard games *(simple)*

```csharp
// In Inspector: enable "Encrypt" on the SaveManager component.
// Nothing else changes — SaveFileIO handles AES-256 transparently.
// Save files become binary; opening them in a text editor shows garbage.
```

---

### 6. Extend GameSaveData with inventory *(detailed)*

```csharp
// 1. Add fields to GameSaveData in SaveDataModel.cs:
[Serializable]
public class GameSaveData
{
    public int version = SaveManager.DataVersion;
    // ...existing fields...
    public InventorySaveData inventory = new InventorySaveData();
}

[Serializable]
public class InventorySaveData
{
    public int[] itemIds;
    public int[] quantities;
}

// 2. Implement ISaveable on your InventoryManager:
public class InventoryManager : MonoBehaviour, ISaveable
{
    private void Awake() => SaveManager.Instance.Register(this);
    private void OnDestroy() => SaveManager.Instance?.Unregister(this);

    public void GatherSaveData(GameSaveData data)
    {
        // Write items[] into data.inventory.itemIds / quantities
    }

    public void ApplySaveData(GameSaveData data)
    {
        // Restore from data.inventory
    }
}
// No changes to SaveManager needed — it calls GatherSaveData/ApplySaveData
// on every registered ISaveable automatically.
```

---

## 💾 Save file location

| Platform | Path |
|----------|------|
| Windows | `%AppData%/../LocalLow/<Company>/<Product>/Saves/` |
| macOS | `~/Library/Application Support/<bundleID>/Saves/` |
| Android | Internal storage (sandbox) |
| iOS | `Documents/Saves/` |

---

## 🛠 Editor menu (Tools ▸ Save Manager)

| Menu | Action |
|------|--------|
| **Open Save Folder** | Reveals `persistentDataPath/Saves/` in Explorer/Finder |
| **Print Save Info** | Logs file names, sizes, timestamps to console |
| **Delete All Save Files** | Wipes `.json` / `.bak` / `.tmp` (with confirmation) |

---

## ⚠️ Notes & gotchas

- **Never use `BinaryFormatter`** — it's a security vulnerability. This system uses `JsonUtility` + AES.
- `GameSaveData` must stay a plain `[Serializable]` C# class — **not** a MonoBehaviour.
- `JsonUtility` does not serialize `Dictionary<>` — use `List<>` or parallel arrays instead.
- AES key is hardcoded for convenience — **replace `AesKey` / `AesIV` bytes before shipping**.
- `OnApplicationQuit` may not fire on iOS/Android — the system hooks `OnApplicationPause(true)` for mobile safety.
- Call `Register(this)` in `Awake`, **not** `Start`, so state is ready before the first save.
