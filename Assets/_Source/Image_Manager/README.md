# Image & Texture Systems

A reusable, dependency-free **image & texture toolkit** for Unity: sprite⇄texture
conversion, CSS-like fill modes for uGUI, runtime resize/thumbnails, compression,
blur, and async screenshots. Every utility is a static extension method — drop the
folder into any project and call them from anywhere.

Part of the **HelperTools** framework → System #2 *Image & Texture Systems* (Phase 4).

---

## ✨ Features

- **Sprite conversion** — `Texture2D ⇄ Sprite`, `RenderTexture → Texture2D`, atlas-safe extraction, Base64 transport, readable copies of compressed/non-readable textures.
- **Fill modes** — `AdaptiveImage` uGUI component: **Fill / Fit / Stretch / Crop / PreserveAspect**.
- **Runtime resize** — GPU-blit based (fast, works on non-readable sources), aspect-aware fit/cover variants.
- **Thumbnails** — `tex.CreateThumbnail(128)` (never upscales).
- **Compression** — JPG/PNG encode, lossy in-memory recompress, GPU `Compress()` wrapper, save/load disk.
- **Blur** — separable box blur (3 passes ≈ gaussian), O(pixels) regardless of radius, shader-free.
- **Screenshots** — async full-screen (with UI), region, camera-only (without UI), capture-and-save.

---

## 🧱 Architecture (separation of concern)

| Piece | Script | Responsibility |
|-------|--------|----------------|
| **Conversion** | `SpriteConversionUtility` | Texture⇄Sprite⇄RenderTexture, Base64, readable copies. The foundation the rest builds on. |
| **Math** | `ImageFillMode` + `FillModeMath` | Pure fill-mode sizing math — shared by UI and resizing (one source of truth). |
| **Resize** | `ImageResizeUtility` | GPU resize, fit/cover variants, thumbnails. |
| **Compression** | `ImageCompressionUtility` | Encoding, in-memory compression, disk I/O. |
| **Blur** | `ImageBlurUtility` | CPU gaussian-approximation blur. |
| **Screenshots** | `ScreenshotUtility` | Async end-of-frame captures, camera renders, save-to-disk. |
| **UI** | `AdaptiveImage` | The only MonoBehaviour — applies fill modes to a uGUI frame. |
| **Editor** | `Editor/ImageManagerMenu` | Tools ▸ Image Manager menu (screenshot, thumbnail, blur from selection). |

All utilities are **static extension methods** with zero state and zero cross-dependencies
beyond `SpriteConversionUtility` / `FillModeMath` — any file works standalone.

---

## 🚀 Quick start

```csharp
using ImageSystem;
```

### Sprite conversion
```csharp
Sprite sprite   = myTexture.ToSprite();              // cheap wrap, no pixel copy
Texture2D tex   = mySprite.ToTexture2D();            // atlas-safe extraction
Texture2D tex2  = myRenderTexture.ToTexture2D();     // GPU → CPU readback
Texture2D copy  = compressedTex.GetReadableCopy();   // works on non-readable imports
string payload  = myTexture.ToBase64Png();           // for JSON/web transport
Texture2D back  = SpriteConversionUtility.FromBase64(payload);
```

### Resize & thumbnails
```csharp
Texture2D exact = tex.Resize(512, 512);              // distorts to exact size
Texture2D fit   = tex.ResizeToFit(1024, 1024);       // aspect kept, fits inside
Texture2D cover = tex.ResizeToCover(1024, 1024);     // aspect kept, covers fully
Texture2D thumb = tex.CreateThumbnail(128);          // longest edge = 128, never upscales
```

### Compression & disk
```csharp
byte[] jpg = tex.EncodeJpg(quality: 75);             // lossy (no alpha)
byte[] png = tex.EncodePng();                        // lossless (keeps alpha)
Texture2D small = tex.CompressLossy(60);             // recompress in memory
tex.CompressInPlace();                               // GPU DXT/ASTC (~1/4 GPU memory)

string path = tex.SaveToFile("avatar.jpg");          // persistentDataPath/Images/
Texture2D loaded = ImageCompressionUtility.LoadFromFile(path);
```

### Blur
```csharp
Texture2D frosted = tex.Blur(radius: 8);             // 3 passes ≈ gaussian
// Tip: blur a downscaled copy for big speedups on large images:
Texture2D fastBg = tex.CreateThumbnail(512).Blur(6);
```

### Screenshots
```csharp
Texture2D shot  = await ScreenshotUtility.CaptureAsync();                       // with UI
Texture2D part  = await ScreenshotUtility.CaptureRegionAsync(new RectInt(0, 0, 512, 512));
Texture2D clean = ScreenshotUtility.CaptureCamera(Camera.main, 1920, 1080);     // without UI
string file     = await ScreenshotUtility.SaveAsync();                          // auto-named PNG
```

### Fill modes in UI
1. Add **`AdaptiveImage`** (HelperTools ▸ Image ▸ AdaptiveImage) to an empty RectTransform — that rect is the *frame*.
2. Assign a Sprite **or** Texture (inspector or `SetSprite` / `SetTexture`).
3. Pick a **Fill Mode**; the component manages a child `Content` graphic and re-applies on every resize.

```csharp
adaptiveImage.SetTexture(downloadedTexture);   // pairs perfectly with Download_Manager
adaptiveImage.FillMode = ImageFillMode.Crop;   // CSS object-fit: cover
```

| Mode | Behaviour |
|------|-----------|
| `Fill` | Cover the frame, overflow visible |
| `Fit` | Letterbox inside the frame |
| `Stretch` | Distort to match exactly |
| `Crop` | Cover + overflow masked (CSS "cover") |
| `PreserveAspect` | Native pixel size, centered, no scaling |

---

## 📚 Example use cases

Real-world scenarios, from simple to advanced. Each example is complete — copy it
into a MonoBehaviour and it runs.

### 1. Show a downloaded image in UI without distortion *(simple)*

**Scenario:** you downloaded a photo (any aspect ratio) and must fill a square
card in your UI without stretching faces.

```csharp
using ImageSystem;
using UnityEngine;

public class ProfileCard : MonoBehaviour
{
    [SerializeField] private AdaptiveImage _photoFrame; // square frame in the UI

    public void ShowPhoto(Texture2D downloadedPhoto)
    {
        // Crop = fill the square completely, cut off the overflow (like CSS "cover").
        _photoFrame.SetTexture(downloadedPhoto);
        _photoFrame.FillMode = ImageFillMode.Crop;
    }
}
```

> Pairs directly with **Download_Manager**: pass the texture from
> `MediaManager.LoadSpriteAsync` / a downloaded file straight into `SetTexture`.

---

### 2. Build a photo gallery with thumbnails *(simple)*

**Scenario:** a gallery grid showing 50 photos. Loading 50 full-resolution
textures would waste hundreds of MB — show small thumbnails instead, and load
the full image only when one is tapped.

```csharp
using ImageSystem;
using UnityEngine;
using UnityEngine.UI;

public class GalleryGrid : MonoBehaviour
{
    [SerializeField] private Image _cellTemplate;

    public void AddPhotoCell(Texture2D fullPhoto)
    {
        // 1. Shrink to a 128px thumbnail (aspect preserved, never upscales).
        Texture2D thumb = fullPhoto.CreateThumbnail(128);

        // 2. The full photo is no longer needed in memory — free it now.
        Destroy(fullPhoto);

        // 3. Wrap the thumbnail in a Sprite and show it.
        Image cell = Instantiate(_cellTemplate, transform);
        cell.sprite = thumb.ToSprite();
    }
}
```

---

### 3. Frosted-glass pause menu background *(detailed)*

**Scenario:** when the player pauses, blur the gameplay behind the pause menu —
the classic "frosted glass" effect, with no shaders required.

```csharp
using ImageSystem;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private RawImage _blurBackground; // full-screen RawImage behind the menu
    [SerializeField] private GameObject _menuRoot;

    private Texture2D _frostedShot;

    public async void OpenPauseMenu()
    {
        // 1. Capture exactly what the player sees (UI included).
        Texture2D shot = await ScreenshotUtility.CaptureAsync();

        // 2. PERFORMANCE TRICK: blur a downscaled copy, not the full 1080p/4K image.
        //    At 512px the blur runs ~10-20x faster and looks identical once stretched.
        Texture2D small = shot.CreateThumbnail(512);
        _frostedShot = small.Blur(radius: 6);

        // 3. Clean up the intermediates immediately (textures are not GC'd!).
        Destroy(shot);
        Destroy(small);

        // 4. Show it behind the menu, then pause the game.
        _blurBackground.texture = _frostedShot;
        _blurBackground.gameObject.SetActive(true);
        _menuRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ClosePauseMenu()
    {
        Time.timeScale = 1f;
        _menuRoot.SetActive(false);
        _blurBackground.gameObject.SetActive(false);
        Destroy(_frostedShot); // free the blurred texture too
    }
}
```

---

### 4. Photo mode — clean screenshot, saved + shareable *(detailed)*

**Scenario:** a "photo mode" button that captures the scene **without UI**, saves
it to disk, and produces a Base64 string you can POST to your backend.

```csharp
using ImageSystem;
using UnityEngine;

public class PhotoMode : MonoBehaviour
{
    public async void TakePhoto()
    {
        // ── Option A: WITHOUT UI ─────────────────────────────────────────
        // Renders only what Camera.main sees; Screen Space Overlay canvases
        // never pass through a camera, so the HUD is automatically excluded.
        Texture2D clean = ScreenshotUtility.CaptureCamera(Camera.main, 1920, 1080);

        // Save to persistentDataPath/Images/ as JPG (quality 85).
        string path = clean.SaveToFile("photo_mode.jpg", jpgQuality: 85);
        Debug.Log($"Photo saved → {path}");

        // Upload-ready Base64 payload (e.g. JSON: { "image": "<base64>" }).
        string base64 = clean.ToBase64Png();
        // await myApi.UploadAsync(base64); ...

        Destroy(clean);

        // ── Option B: WITH UI, one line ──────────────────────────────────
        // Captures everything on screen and auto-names the file with a timestamp:
        string fullPath = await ScreenshotUtility.SaveAsync();
        Debug.Log($"Full screenshot saved → {fullPath}");
    }
}
```

---

### 5. Avatar: receive → compress → cache on disk → reload next launch *(detailed)*

**Scenario:** the server sends a user avatar as Base64 inside JSON. Show it,
keep a small compressed copy on disk, and load from disk on the next launch so
no network call is needed.

```csharp
using ImageSystem;
using UnityEngine;

public class AvatarCache : MonoBehaviour
{
    [SerializeField] private AdaptiveImage _avatarFrame;
    private const string FileName = "avatar.jpg";

    /// <summary>Call when the server response arrives (first launch / avatar change).</summary>
    public void OnAvatarReceived(string base64FromJson)
    {
        // 1. Decode the Base64 payload into a texture.
        Texture2D avatar = SpriteConversionUtility.FromBase64(base64FromJson);
        if (avatar == null) { Debug.LogError("Avatar payload was not a valid image."); return; }

        // 2. Avatars are shown small — 256px is plenty. Shrink before caching.
        Texture2D resized = avatar.ResizeToCover(256, 256);
        Destroy(avatar);

        // 3. Cache to disk as JPG (small file, alpha not needed for photos).
        resized.SaveToFile(FileName, jpgQuality: 75);

        // 4. Show it cropped into the circular/square frame.
        _avatarFrame.SetTexture(resized);
        _avatarFrame.FillMode = ImageFillMode.Crop;
    }

    /// <summary>Call on startup — instant, offline, no network.</summary>
    public bool TryLoadCachedAvatar()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "Images", FileName);
        Texture2D cached = ImageCompressionUtility.LoadFromFile(path);
        if (cached == null) return false; // not cached yet → fetch from server

        _avatarFrame.SetTexture(cached);
        _avatarFrame.FillMode = ImageFillMode.Crop;
        return true;
    }
}
```

---

### 6. Cut one sprite out of an atlas and save it as a PNG *(simple)*

**Scenario:** you need a single icon out of a packed sprite atlas as a standalone
image file (e.g. to upload it, or to feed a native share sheet).

```csharp
using ImageSystem;
using UnityEngine;

public class AtlasExtractor : MonoBehaviour
{
    public string ExportIcon(Sprite atlasSprite)
    {
        // ToTexture2D is atlas-safe: it extracts ONLY the sprite's sub-rect,
        // and it works even when the atlas texture is compressed/non-readable.
        Texture2D icon = atlasSprite.ToTexture2D();

        string path = icon.SaveToFile($"{atlasSprite.name}.png");
        Destroy(icon);
        return path; // absolute path under persistentDataPath/Images/
    }
}
```

---

### 7. Shrink GPU memory after a big download *(simple)*

**Scenario:** the Download_Manager just pulled 38 gallery images; on a mobile
kiosk you want them to occupy ~¼ of the GPU memory.

```csharp
using ImageSystem;
using UnityEngine;

public static class DownloadedTextureOptimizer
{
    public static void Optimize(Texture2D downloaded)
    {
        // In-place GPU compression (DXT/ASTC). Requirement: width & height
        // must be multiples of 4 — most photos/banners are; others are skipped
        // with a warning, never corrupted.
        downloaded.CompressInPlace();
        // Note: also frees the CPU-side pixel copy (makeNoLongerReadable),
        // so do any GetPixels/encode work BEFORE calling this.
    }
}
```

---

### 8. Live camera preview / minimap into the UI *(simple)*

**Scenario:** show a top-down minimap camera inside a UI panel, sized correctly
whatever the panel's shape.

```csharp
using ImageSystem;
using UnityEngine;

public class MinimapView : MonoBehaviour
{
    [SerializeField] private Camera _minimapCamera;
    [SerializeField] private AdaptiveImage _minimapFrame;

    public void RefreshMinimap()
    {
        // One-shot render of the minimap camera at a fixed resolution.
        Texture2D frame = ScreenshotUtility.CaptureCamera(_minimapCamera, 512, 512);

        // Fit = letterbox inside the panel, never cropped, never distorted.
        _minimapFrame.SetTexture(frame);
        _minimapFrame.FillMode = ImageFillMode.Fit;
    }
}
```

> For a **continuously live** minimap, assign a `RenderTexture` to the camera and
> call `_minimapFrame.SetTexture(myRenderTexture)` once — `AdaptiveImage` accepts
> any `Texture`, including RenderTextures.

---

### Cheat-sheet: which API for which job?

| I want to… | Use |
|------------|-----|
| Show any texture in UI without distortion | `AdaptiveImage` + `Crop`/`Fit` |
| Make a small preview image | `CreateThumbnail(maxSize)` |
| Make an exact-size copy | `Resize(w, h)` |
| Blur something once (popup, effect) | `CreateThumbnail(512).Blur(6)` |
| Screenshot with UI | `await ScreenshotUtility.CaptureAsync()` |
| Screenshot without UI | `ScreenshotUtility.CaptureCamera(cam, w, h)` |
| Save an image to disk | `tex.SaveToFile("name.jpg")` |
| Load an image from disk | `ImageCompressionUtility.LoadFromFile(path)` |
| Send an image inside JSON | `tex.ToBase64Png()` / `FromBase64(str)` |
| Cut a sprite out of an atlas | `sprite.ToTexture2D()` |
| Reduce GPU memory of a texture | `tex.CompressInPlace()` |
| Read pixels of a compressed texture | `tex.GetReadableCopy()` |

---

## 🛠 Editor menu

| Menu (Tools ▸ Image Manager) | Action |
|------------------------------|--------|
| **Capture Game View Screenshot** | Timestamped PNG → `Screenshots/` at project root |
| **Create Thumbnail From Selected Texture** | 256px `_thumb.png` next to the selected asset |
| **Blur Selected Texture** | `_blurred.png` next to the selected asset |

---

## ⚠️ Notes & gotchas

- Every helper returning a `Texture2D` allocates a **new** texture — `Destroy()` it when done (textures are not garbage-collected).
- `EncodeJpg` drops alpha; use PNG when transparency matters.
- `CompressInPlace` requires dimensions that are multiples of 4.
- Screenshot methods (`CaptureAsync` etc.) must be awaited on the **main thread**; camera capture is synchronous and UI-free.
- The blur is CPU-side — ideal for one-off effects (frosted popups, photo filters), not per-frame full-screen blur.
