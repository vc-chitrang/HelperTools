# JSON Utilities — System #6

Serialize · Deserialize · File I/O · PlayerPrefs · Array support · Clone · Merge · Validate

---

## Structure

```
Json_Manager/
├── Scripts/
│   ├── JsonHelper.cs          # Core serialize / deserialize / clone / merge / validate
│   ├── JsonFileManager.cs     # Atomic file I/O at persistentDataPath
│   ├── JsonPrefsManager.cs    # PlayerPrefs-backed JSON storage
│   ├── JsonExtensions.cs      # Extension methods on object / string
│   ├── JsonDemoController.cs  # Demo scene UI controller
│   └── Editor/
│       ├── JsonUtilitiesMenu.cs    # Tools > JSON Utilities menu
│       └── JsonUtilitiesBuilder.cs # (DELETED after use)
├── Scenes/
│   └── JsonUtilitiesScene.unity   # Live demo
└── README.md
```

---

## Quick Start

```csharp
using JsonUtilities;

// Serialize any [Serializable] object
string json = JsonHelper.ToJson(myObject, prettyPrint: true);

// Deserialize
var obj = JsonHelper.FromJson<MyType>(json);
if (JsonHelper.TryFromJson<MyType>(json, out var safe)) { ... }

// Arrays & Lists  (JsonUtility limitation workaround built-in)
string arrayJson = JsonHelper.ToJsonArray(myArray);
MyType[] arr = JsonHelper.FromJsonArray<MyType>(arrayJson);

// Deep clone via JSON round-trip
var copy = JsonHelper.Clone(myObject);

// Partial merge — apply partial JSON fields onto an existing object
JsonHelper.Merge(target, "{\"level\":5}");

// Validate
bool ok = JsonHelper.IsValidJson(json);
```

---

## File I/O

```csharp
using JsonUtilities;

// Save to  persistentDataPath/player.json  (atomic write)
JsonFileManager.Save(player, "player");

// Load — returns new T() if file missing
var player = JsonFileManager.Load<Player>("player");

// Safe load
if (JsonFileManager.TryLoad<Player>("player", out var p)) { ... }

// Existence / deletion
bool exists = JsonFileManager.Exists("player");
JsonFileManager.Delete("player");

// Sub-folder  (set once at startup)
JsonFileManager.SubFolder = "saves";

// Inspect path
string path = JsonFileManager.GetPath("player");
```

---

## PlayerPrefs JSON

```csharp
using JsonUtilities;

// Store any serializable object in PlayerPrefs
JsonPrefsManager.Set("settings", mySettings);
JsonPrefsManager.Flush();   // force write to disk (important on mobile)

// Read back
var s = JsonPrefsManager.Get<Settings>("settings");
if (JsonPrefsManager.TryGet<Settings>("settings", out var safe)) { ... }

// Existence / deletion
bool has = JsonPrefsManager.HasKey("settings");
JsonPrefsManager.Delete("settings");

// Custom key prefix (default "json.")
JsonPrefsManager.KeyPrefix = "myapp.";
```

---

## Extension Methods

```csharp
using JsonUtilities;

// object → JSON
string json = myObj.ToJson(prettyPrint: true);
var copy     = myObj.JsonClone();

// string → object
var obj  = json.FromJson<MyType>();
bool ok  = json.TryParseJson<MyType>(out var result);

// Arrays / lists
string aj = myArray.ToJsonArray();
MyType[] arr = aj.FromJsonArray<MyType>();

// Validation
bool valid = json.IsValidJson();
bool isObj = json.IsJsonObject();
bool isArr = json.IsJsonArray();
```

---

## Inspector Settings

### JsonFileManager
| Property | Default | Description |
|---|---|---|
| SubFolder | `""` | Sub-folder inside persistentDataPath |

### JsonPrefsManager
| Property | Default | Description |
|---|---|---|
| KeyPrefix | `"json."` | Prefix applied to all PlayerPrefs keys |

---

## Notes

- **No Newtonsoft required** — built entirely on `UnityEngine.JsonUtility`.
- **Top-level array support** — JsonUtility cannot serialize bare arrays; `ToJsonArray`/`FromJsonArray` handle this with an internal wrapper transparently.
- **Atomic writes** — `JsonFileManager.Save` writes to a `.tmp` file then renames, preventing data corruption if the app crashes mid-write.
- **Merge** works best for class (reference) types. For struct types, `JsonUtility.FromJsonOverwrite` operates on a boxed copy; use `Clone` + manual assignment if full struct merge is needed.
- **Validation** is structural (checks root delimiters `{}` / `[]`), not a full JSON parser. For strict validation, integrate Newtonsoft `JToken.Parse`.
- On mobile, call `JsonPrefsManager.Flush()` after critical saves — `PlayerPrefs.Save()` is automatic on quit but the OS may kill the app without firing `OnApplicationQuit`.
