# Audio Framework

A reusable, production-ready **audio system** for Unity: one `AudioManager` facade for BGM,
SFX, volume and mute, backed by an Audio Mixer (Master ▸ BGM / SFX) and a pooled SFX system.

Part of the **HelperTools** framework → System #4 *Audio Framework* (Phase 1).

---

## ✨ Features

- **One facade** (`AudioManager`) for everything — gameplay & UI call a single API.
- **4 channels** — `Master`, `BGM`, `SFX`, `VO` — each with its own mixer group, exposed volume param, slider/UI, and AudioSources.
- **Audio Mixer integration** — volume set in correct **decibels** (not raw linear).
- **BGM / Dynamic Music** — crossfade between tracks, fade in/out, pause/resume.
- **SFX / VO**:
  - `PlaySFX(clip, onComplete, volume, pitch)` / `PlayVO(clip, onComplete, volume, pitch)` — **pooled**, auto-returned when finished, with a **completion callback**.
  - `PlaySfxLooping` — lives until you stop it (ambient / "until app closes").
  - **Multiple / overlapping** play — just call repeatedly.
- **Reusable AudioSource pool** — prewarmed, auto-expands to a cap, no per-sound GameObject churn.
- **Volume UI panel** — per-channel slider (0–100%) + mute toggle button with **sprite swap**.
- **Persistence** — volumes & mute states saved to `PlayerPrefs`.
- **Separation of concern** — `AudioManager` (logic) / `AudioVolumePanel` (UI) / mixer (asset) are independent.

---

## 🧱 Architecture

| Piece | Script / Asset | Responsibility |
|-------|----------------|----------------|
| **Facade** | `AudioManager` | BGM, SFX, volume, mute. Singleton, persists across scenes. *No UI.* |
| **Pool** | `SfxAudioSourcePool` | Recycles `AudioSource` instances for SFX. |
| **UI** | `AudioVolumePanel` | Sliders + mute buttons. *Talks only to `AudioManager`.* |
| **Data** | `AudioChannel`, `AudioMixerParameters`, `AudioVolume` | Channel enum, mixer param names, linear⇄dB conversion. |
| **Asset** | `Audio/GameAudioMixer.mixer` | Master ▸ BGM / SFX / VO groups; exposed `MasterVolume`/`BGMVolume`/`SFXVolume`/`VOVolume`. |

---

## 🚀 Setup

The scene `Scenes/AudioManagerScene.unity` is pre-built and wired. To use in another scene:

1. Add an **`AudioManager`** GameObject (add the `AudioManager` component).
2. Assign the **mixer** + **BGM/SFX groups** (`Audio/GameAudioMixer.mixer`).
3. (Optional) Add the **`AudioVolumePanel`** to a Canvas and assign each row's slider/button/icon,
   plus **`_toggleButtonSprites`**: **index 0 = mute sprite, index 1 = unmute sprite**.

> Rebuild anytime via **Tools ▸ Audio Manager ▸ Build Audio Mixer** and **Build Audio Scene**.

---

## 🧑‍💻 Usage

```csharp
using AudioSystem;

// ── BGM ──────────────────────────────────────────────
AudioManager.Instance.PlayBGM(menuMusic);                 // crossfade in
AudioManager.Instance.PlayBGM(battleMusic, fadeDuration: 2f);
AudioManager.Instance.StopBGM();                          // fade out
AudioManager.Instance.PauseBGM();  AudioManager.Instance.ResumeBGM();

// ── SFX (pooled, auto-returns, with completion callback) ──
AudioManager.Instance.PlaySFX(clickClip);                              // fire & forget
AudioManager.Instance.PlaySFX(shootClip, () => Debug.Log("done"), 0.8f); // callback + volume
AudioManager.Instance.PlaySFX(hitClip, null, 1f, pitch: 1.2f);        // overlaps freely

AudioSource loop = AudioManager.Instance.PlaySfxLooping(engineHum);   // lives until stopped
AudioManager.Instance.StopSfx(loop);                                  // stop + recycle

// ── Voice-over (VO channel, same pattern) ────────────
AudioManager.Instance.PlayVO(line01, () => ShowNextLine(), volume: 1f);

// ── Volume & Mute (also driven by the UI panel) ──────
AudioManager.Instance.SetVolume(AudioChannel.VO, 0.5f);   // linear 0–1
AudioManager.Instance.ToggleMute(AudioChannel.BGM);
bool muted = AudioManager.Instance.IsMuted(AudioChannel.Master);
```

---

## 🎚 Volume UI panel

`AudioVolumePanel` exposes:
- `_rows[]` — each binds a `channel` to a `volumeSlider`, `muteButton`, `muteIcon`, and optional `percentLabel`.
- `_toggleButtonSprites[]` — **[0] = mute icon, [1] = unmute icon** (swapped on the button when toggled).

The panel reads/writes only through `AudioManager`, so it stays fully decoupled from audio logic.

---

## ⚙️ Configuration (`AudioManager` inspector)

| Field | Purpose |
|-------|---------|
| Mixer / BGM Group / SFX Group | Audio Mixer + routing groups. |
| BGM Fade Duration | Default crossfade/fade time (seconds). |
| SFX Initial / Max Pool Size | Prewarm count and hard cap for the SFX pool. |
| Persist Across Scenes | `DontDestroyOnLoad` for a global manager. |
| Load Saved Settings | Restore volume/mute from `PlayerPrefs` on start. |

---

## 📝 Notes

- Mixer parameter names live in `AudioMixerParameters` — change them in one place if you rename.
- Linear↔dB conversion is centralized in `AudioVolume` (avoids the "0.5 linear ≈ silent" bug).
- One `AudioListener` per scene (on the Main Camera) — the builder ensures one exists.
