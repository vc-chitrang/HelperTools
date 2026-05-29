# Async Download Manager

A reusable, mobile-friendly **async/await asset downloader** for Unity with a ready-made
progress UI. Drop the prefab into any scene, hand it a list of URLs, and it downloads them
**concurrently**, shows per-asset + overall progress on two sliders, organizes files into
type-based folders, and fires a completion callback.

Part of the **HelperTools** framework → System #1 *Async Systems* (Phase 1).

---

## ✨ Features

- **Async/await** downloads built on `UnityWebRequest` (no blocking, no coroutil spaghetti).
- **Concurrent** downloads with a bounded, **Auto-tuned** parallelism limit.
- **Two-slider progress UI** — current asset (`file ——— 72%`) and overall (`12/38 (31%)`).
- **Disk caching** — already-downloaded files are skipped (no re-download).
- **Category folders** — files auto-sorted into `Images / Videos / Models / Other`.
- **Completion callback** — `onAllAssetDownload Done` via C# event, `UnityEvent`, or method callback.
- **Cancellation + per-file error isolation** — one failure never aborts the batch.
- **Clean separation of concern** — logic / UI / wiring are independent and individually reusable.

---

## 🧱 Architecture (separation of concern)

| Layer | Script | Responsibility |
|-------|--------|----------------|
| **Logic** | `MediaManager` | Orchestrates concurrent downloads, caching, folders. Emits progress **events**. *No UI.* |
| **I/O** | `DownloadUtility` | Pure `UnityWebRequest` → disk. Stateless. *No Unity-asset knowledge.* |
| **UI** | `DownloadProgressView` | Renders progress onto sliders/labels/message. *No download knowledge.* |
| **Wiring** | `DownloadSceneController` | Sources URLs, connects manager events → view, exposes the completion callback. |
| **Data** | `MediaFile`, `MediaRegistry`, `MediaCategory`, `AppDataModel` | Records, catalogue, extension→folder map, JSON model. |
| **Loaders** | `ImageLoader`, `VideoLoader` | Turn downloaded files into `Sprite` / resolved video paths. |

The manager and the view **never reference each other** — the controller is the only place they meet.

---

## 🚀 Quick start (drag & drop)

1. Drag **`Prefabs/DownloadManager.prefab`** into your scene.
2. Provide URLs (pick one):
   - **Inspector** — on the root's `DownloadSceneController`, assign a JSON `TextAsset`, set a JSON
     file name under `persistentDataPath`, or fill the **Manual Url Fallback** list.
   - **Code** — see below.
3. Press **Play**. With *Download On Start* enabled it runs automatically.

> No `EventSystem` is required — the UI is display-only.

---

## 🧑‍💻 Use from code (any project)

```csharp
using UnityEngine;
using DownloadManager;

public class Example : MonoBehaviour
{
    [SerializeField] private DownloadSceneController _downloader; // ref to the prefab instance

    private async void Start()
    {
        string[] urls =
        {
            "https://example.com/a.png",
            "https://example.com/clip.mp4",
            "https://example.com/model.glb",
        };

        await _downloader.StartDownload(urls, success =>
        {
            Debug.Log(success ? "✓ All assets downloaded!" : "✗ Some failed (see console).");
        });
    }
}
```

### Using the logic layer directly (no UI)

```csharp
var manager = GetComponent<MediaManager>();
manager.OverallProgressChanged += p => Debug.Log($"{p.Downloaded}/{p.Total} ({p.Percent}%)");
manager.AssignDownloadableUrls(urls);
await manager.DownloadAllAsync(success => Debug.Log($"Done: {success}"));

// Then load assets on demand:
await manager.LoadSpriteAsync(imageUrl, sprite => image.sprite = sprite);
await manager.LoadVideoAsync(videoUrl, path => videoPlayer.url = path);
```

---

## 🔔 Completion callback — `onAllAssetDownload Done`

Three equivalent ways to hook it:

1. **Method callback** — `StartDownload(urls, success => ...)` or `DownloadAllAsync(success => ...)`.
2. **C# event** — `controller.AllAssetsDownloaded += success => ...` / `manager.AllAssetsDownloaded += ...`.
3. **Inspector `UnityEvent`** — wire `On All Assets Downloaded (bool)` on `DownloadSceneController`.

---

## 📁 Where files are saved

```
Application.persistentDataPath/MediaFiles/
├── Images/   (.png .jpg .jpeg .gif .bmp .tga .webp)
├── Videos/   (.mp4 .mov .webm .mkv .avi .m4v)
├── Models/   (.glb .gltf .obj .fbx .dae .ply .stl)
└── Other/    (everything else)
```

Files keep their original name from the URL. Routing lives in `MediaCategory` — add new
extensions there and every layer picks it up automatically.

---

## ⚙️ Configuration

**`MediaManager`**
- **Max Concurrent Downloads** — `0 = Auto` (recommended). Downloads are network I/O-bound, so the
  optimal value depends on bandwidth + the server's per-host connection cap (~6–8), **not** CPU cores.
  Auto targets that sweet spot. Set `1` for sequential.

**`DownloadSceneController`**
- **Json File Name** — file under `persistentDataPath` to parse for URLs.
- **Absolute Json Path Override** — explicit path (editor testing).
- **Json Asset Fallback** — a bundled JSON `TextAsset`.
- **Manual Url Fallback** — hard-coded URL list.
- **Max Assets** — cap the batch (`0` = all).
- **Download On Start** — auto-run on scene load.

Resolution order for URLs: *absolute path → persistentDataPath file → TextAsset → manual list.*

---

## 🧩 Regenerating the UI / prefab

Editor menu (Editor-only, stripped from builds):

- **Tools ▸ Download Manager ▸ Build Download UI** — (re)build the demo scene UI.
- **Tools ▸ Download Manager ▸ Build Reusable Prefab** — regenerate `Prefabs/DownloadManager.prefab`.

---

## 🗂 JSON URL source format

`AppDataUrlExtractor.ExtractUrls(json)` flattens video + image URLs from the bundled
`SHM_AppData.json` schema (videos first, then images, deduplicated). Swap in your own
extractor or just call `AssignDownloadableUrls(...)` with any `IEnumerable<string>`.
