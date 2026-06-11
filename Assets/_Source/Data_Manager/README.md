# Data Management — System #23

Three tools in one package: CSV parsing, Excel (.xlsx) reading, and a ScriptableObject database system.

---

## CSV

### Parse

```csharp
using DataManagement;

// From a Resources folder (omit the .csv extension)
var rows = CSVReader.LoadFromResourcesWithHeaders("Data/Items");
foreach (var row in rows)
    Debug.Log($"{row["Name"]} — Level {row["Level"]}");

// From StreamingAssets
var rows = CSVReader.LoadFromStreamingAssetsWithHeaders("Config/Enemies.csv");

// From an absolute path (Editor / standalone)
var rows = CSVReader.LoadFromPathWithHeaders("/path/to/file.csv");

// Raw text
var rows = CSVReader.ParseWithHeaders(csvString);

// Without headers (returns List<string[]>)
var raw = CSVReader.LoadFromResources("Data/Items");
```

### Write

```csharp
// Write rows
CSVWriter.Write("/path/output.csv", myRows);

// Write objects — public fields + properties become columns automatically
var items = new List<MyItem> { ... };
CSVWriter.Write("/path/items.csv", items);

// Shorthand: write to persistentDataPath
CSVWriter.WriteToPersistentData("highscores.csv", scoreList);
```

### Serialize / deserialize in-memory

```csharp
// Parse CSV text → rows
List<string[]> rows = CSVParser.Parse(text);
List<Dictionary<string,string>> dict = CSVParser.ParseWithHeaders(text);

// Objects → CSV text
string csv = CSVParser.Serialize(myObjects);
```

---

## Excel (.xlsx)

Reads basic spreadsheets without any third-party package.  
**Supports:** number, string, boolean cells.  
**Does NOT support:** formulas, merged cells, rich text.  
**Not supported on WebGL** (check `ExcelReader.IsSupported` first).

```csharp
using DataManagement;

if (ExcelReader.IsSupported)
{
    // From StreamingAssets
    var rows = ExcelReader.ReadFromStreamingAssets("Data/Items.xlsx");

    // With headers
    var dict = ExcelReader.ReadWithHeaders("/absolute/path/Items.xlsx");

    // From raw bytes (e.g., UnityWebRequest download)
    var rows = ExcelReader.Read(downloadedBytes, sheetIndex: 0);
}
```

**Tip:** For WebGL, convert .xlsx to CSV in the Editor and ship the CSV instead.

---

## ScriptableObject Database

### 1. Create your record type

```csharp
[CreateAssetMenu(menuName = "MyGame/Item Record")]
public class ItemRecord : DataRecord
{
    public string ItemName;
    public string ItemType;
    public int    Level;
    public float  Power;
}
```

### 2. Create your database type

```csharp
[CreateAssetMenu(menuName = "MyGame/Item Database")]
public class ItemDatabase : SODatabase<ItemRecord> { }
```

### 3. Create assets

- **Right-click** → Create → MyGame → Item Database → saves `ItemDatabase.asset`
- Either add individual `ItemRecord.asset` files in the Inspector,
  **or** use the CSV importer (see below).

### 4. Query at runtime

```csharp
[SerializeField] private ItemDatabase _db;

// Single lookup
ItemRecord sword = _db.FindById("sword_01");

// Predicate search
var weapons = _db.FindAll(r => r.ItemType == "Weapon" && r.Level >= 5);
ItemRecord first = _db.Find(r => r.Power > 30f);

// Iterate all
foreach (var rec in _db.Enumerate())
    Debug.Log(rec.ItemName);

// Validation (development check)
_db.Validate();
```

---

## CSV → SO Database Importer

Open via **Tools → Data Manager → Import CSV to SO Database**.

1. Choose the target `SODatabase` asset.
2. Choose the CSV file (first row = column headers, must match public field names).
3. Choose the output folder for generated `.asset` files.
4. Click **Import Now**.

Each CSV row becomes one `DataRecord.asset` file. The `Id` column maps to `DataRecord.Id`.

**Programmatic import (build scripts):**

```csharp
#if UNITY_EDITOR
DataManagement.Editor.SODatabaseImporter.Import(
    database:      myDatabaseAsset,
    csvText:       File.ReadAllText("Items.csv"),
    recordType:    typeof(ItemRecord),
    outputFolder:  "Assets/Data/Records");
#endif
```

---

## Custom CSV → SO mapping

By default `PopulateFromCSVRow` uses reflection to map column names to public fields (case-insensitive). Override it for custom logic:

```csharp
public class ItemRecord : DataRecord
{
    public string ItemName;
    public int    Level;

    public override void PopulateFromCSVRow(Dictionary<string, string> row)
    {
        base.PopulateFromCSVRow(row);          // reflection pass for common fields
        // Custom overrides:
        if (row.TryGetValue("item_name", out var n)) ItemName = n;
        if (row.TryGetValue("lvl", out var l) && int.TryParse(l, out int v)) Level = v;
    }
}
```

**Supported auto-mapped types:** `string`, `int`, `float`, `double`, `long`, `bool`,
`Vector2` (`"x,y"`), `Vector3` (`"x,y,z"`), `Color` (`"#RRGGBB"` or `"r,g,b"`), `enum`.

---

## Demo Scene

Open via **Tools → Data Manager → Open Demo Scene**

- **Left panel (CSV)** — loads `DemoItems.csv` from Resources; live filter by any column value
- **Middle panel (Excel)** — shows how to load .xlsx from StreamingAssets; place `Sample.xlsx` in `StreamingAssets/DataManagerDemo/` to see live data
- **Right panel (SO Database)** — queries the pre-populated `DemoItemDatabase.asset`

---

## File Structure

```
Data_Manager/
├── Scripts/
│   ├── CSV/
│   │   ├── CSVParser.cs              — RFC 4180 parse + serialize
│   │   ├── CSVReader.cs              — Load from Resources / StreamingAssets / path
│   │   └── CSVWriter.cs              — Write to file
│   ├── Excel/
│   │   └── ExcelReader.cs            — .xlsx via ZipArchive + XDocument
│   ├── SODatabase/
│   │   ├── DataRecord.cs             — Abstract base ScriptableObject
│   │   ├── DataRecordReflectionHelper.cs  — Reflection-based field mapping
│   │   └── SODatabase.cs             — Generic queryable database
│   ├── Demo/
│   │   ├── DemoItemRecord.cs
│   │   └── DemoItemDatabase.cs
│   ├── DataDemoController.cs
│   └── Editor/
│       ├── DataManagerMenu.cs        — Tools/Data Manager menu
│       └── SODatabaseImporter.cs     — Editor window + static import helper
├── Resources/Demo/
│   └── DemoItems.csv                 — 15 sample items
├── SOData/                           — Generated .asset files (after build)
├── Scenes/
│   └── DataManagerScene.unity
└── README.md
```
