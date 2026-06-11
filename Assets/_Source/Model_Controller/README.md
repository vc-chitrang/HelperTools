# Model Controller — System #3

Orbit · Zoom · Pan · Inertia · Multi-Touch · Reset  
Works on: **Unity Editor**, **Windows**, **macOS**, **Android Touch**

---

## Structure

```
Model_Controller/
├── Scripts/
│   ├── ModelViewController.cs      # Camera controller (all input + math)
│   ├── ModelViewDemoUI.cs          # Demo scene UI wiring
│   └── Editor/
│       ├── ModelControllerMenu.cs  # Tools > Model Controller menu
│       └── ModelControllerBuilder.cs  # (DELETED after use)
├── Prefabs/
│   └── ModelViewCamera.prefab      # Camera + ModelViewController, drag-and-drop ready
├── Scenes/
│   └── ModelControllerScene.unity  # Live demo with compound 3D model
└── README.md
```

---

## Quick Start

### Option A — Prefab

1. Drag **ModelViewCamera.prefab** into your scene.
2. Delete any other camera if needed.
3. Hit Play — orbit/zoom/pan works immediately.

### Option B — Add Component

1. Select your existing Camera GameObject.
2. Add Component → **HelperTools / Model Controller / ModelViewController**.
3. Configure settings in the Inspector.

---

## Controls

### Mouse (Editor / Windows / macOS)

| Input | Action |
|---|---|
| **LMB Drag** | Orbit (rotate around target) |
| **RMB Drag** | Pan (translate target point) |
| **MMB Drag** | Pan (alternative) |
| **Scroll Wheel** | Zoom in / out |
| **R key** | Reset to default pose |

### Touch (Android)

| Input | Action |
|---|---|
| **1-Finger Drag** | Orbit |
| **2-Finger Pinch** | Zoom in / out |
| **2-Finger Translate** | Pan |
| **Double Tap** | Reset to default pose |

---

## Inspector Settings

### Initial Pose
| Field | Default | Description |
|---|---|---|
| Default Target | (0,0,0) | World-space point the camera orbits around |
| Default Azimuth | 45° | Horizontal angle (Y-axis rotation) |
| Default Elevation | 20° | Vertical angle above horizon |
| Default Distance | 5 | Distance from target |
| Auto Focus Target | — | Optional Transform — FocusOn() called on Start |

### Orbit
| Field | Default |
|---|---|
| Orbit Speed Mouse | 0.25 (deg/px) |
| Orbit Speed Touch | 0.18 (deg/px) |
| Min Elevation | −80° |
| Max Elevation | +80° |

### Zoom
| Field | Default |
|---|---|
| Zoom Scroll Speed | 0.12 (fraction/tick) |
| Zoom Pinch Speed | 0.002 |
| Zoom Smoothing | 12 |
| Min Distance | 0.3 |
| Max Distance | 30 |

### Pan
| Field | Default |
|---|---|
| Pan Speed Mouse | 1× (world-per-pixel scale) |
| Pan Speed Touch | 1× |

### Inertia
| Field | Default |
|---|---|
| Inertia Enabled | true |
| Inertia Damping | 6 |

---

## Public API

```csharp
using ModelController;

var mvc = Camera.main.GetComponent<ModelViewController>();

// Reset to default pose (animated)
mvc.ResetView();

// Frame all renderers under a transform
mvc.FocusOn(myModel.transform);

// Frame on explicit bounds
mvc.FocusOnBounds(new Bounds(center, size));

// Read current state
float az   = mvc.Azimuth;
float el   = mvc.Elevation;
float dist = mvc.Distance;
Vector3 pt = mvc.TargetPoint;
```

---

## Notes

- Pan speed is automatically scaled by frustum height so panning feels consistent at all zoom levels.
- Elevation is clamped to ±80° to avoid gimbal lock at the poles.
- Zoom uses a **fractional** formula (`distance *= 1 − scroll × speed`) so scrolling feels linear at any distance.
- Inertia applies only to **orbit**, not pan or zoom.
- On Android, the double-tap window is 350 ms.
