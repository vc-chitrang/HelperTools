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

### Step 1 — Set up your scene hierarchy

```
ModelViewPivot          ← empty GameObject (the pivot / orbit centre)
  └── YourModel         ← your 3D model as a child of the pivot
ModelViewCamera         ← Camera with ModelViewController attached
```

> **Important:** Place your 3D model as a child of a GameObject named **`ModelViewPivot`**.  
> Assign `ModelViewPivot` to the **Model Pivot** field on `ModelViewController`.  
> This is the transform all orbit, pan, and local-axis operations act on.

### Step 2 — Add the controller

**Option A — Prefab**

1. Drag **ModelViewCamera.prefab** into your scene.
2. Create an empty `ModelViewPivot` GameObject and place your model under it.
3. Assign `ModelViewPivot` to the **Model Pivot** field on `ModelViewController`.
4. Hit Play.

**Option B — Add Component**

1. Select your Camera and add **HelperTools / Model Controller / ModelViewController**.
2. Create a `ModelViewPivot` empty, parent your model under it.
3. Assign it to **Model Pivot** in the Inspector.

---

## Controls

### Mouse (Editor / Windows / macOS)

| Input | Action |
|---|---|
| **LMB Drag** | Orbit (camera moves) or local-axis rotate (model spins) |
| **RMB / MMB Drag** | Pan |
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
| Default Azimuth | 45° | Horizontal start angle |
| Default Elevation | 20° | Vertical start angle |
| Default Distance | 5 | Start distance from target |
| Auto Focus Target | — | Optional Transform — FocusOn() called on Start |

### Model Pivot
| Field | Description |
|---|---|
| Model Pivot | Assign the `ModelViewPivot` empty parent of your 3D model. All operations pivot here. |

### Orbit
| Field | Default | Description |
|---|---|---|
| Orbit Speed Mouse | 0.25 | deg/pixel |
| Orbit Speed Touch | 0.18 | deg/pixel |
| Min / Max Elevation | −80° / +80° | Clamp to avoid pole flip |
| Invert Orbit X | true | Flip horizontal orbit direction |
| Invert Orbit Y | true | Flip vertical orbit direction |
| Orbit Local Axis | false | **OFF** = camera orbits around pivot (model still). **ON** = pivot rotates on its own local axes (model spins in place, camera fixed). |

### Zoom
| Field | Default |
|---|---|
| Zoom Scroll Speed | 0.12 (fraction/tick) |
| Zoom Pinch Speed | 0.002 |
| Zoom Smoothing | 12 |
| Min / Max Distance | 0.3 / 30 |

### Pan
| Field | Default | Description |
|---|---|---|
| Pan Speed Mouse | 1× | Frustum-height scaled |
| Pan Speed Touch | 1× | |
| Invert Pan X | true | Flip horizontal pan |
| Invert Pan Y | true | Flip vertical pan |

> When **Orbit Local Axis** is ON, pan translates the `ModelViewPivot` in world space (the model slides). When OFF, pan moves the camera's orbit centre.

### Inertia
| Field | Default |
|---|---|
| Inertia Enabled | true |
| Inertia Damping | 6 |

### Reset
| Field | Default |
|---|---|
| Reset Duration | 0.45 s |
| Reset Key | R |

> Reset returns both the camera pose **and** the `ModelViewPivot` transform (position + rotation) to their Start() state.

---

## Public API

```csharp
using ModelController;

var mvc = Camera.main.GetComponent<ModelViewController>();

// Reset camera + pivot to Start() pose (animated)
mvc.ResetView();

// Frame all renderers under a transform
mvc.FocusOn(myModel.transform);

// Frame on explicit bounds
mvc.FocusOnBounds(new Bounds(center, size));

// Toggle orbit mode at runtime
mvc.OrbitLocalAxis = true;   // model rotates on its own axis
mvc.OrbitLocalAxis = false;  // camera orbits around model

// Invert toggles
mvc.InvertOrbitX = true;
mvc.InvertOrbitY = true;
mvc.InvertPanX   = true;
mvc.InvertPanY   = true;

// Read current state
float az    = mvc.Azimuth;
float el    = mvc.Elevation;
float dist  = mvc.Distance;
Vector3 pt  = mvc.TargetPoint;
Transform p = mvc.ModelPivot;
```

---

## Orbit Mode Reference

| Mode | Who moves | Behaviour |
|---|---|---|
| **Turntable** (`OrbitLocalAxis = false`) | Camera | Orbits around `ModelViewPivot` using world Y as up. Horizon stays level. Model is stationary. |
| **Local Axis** (`OrbitLocalAxis = true`) | Model Pivot | `ModelViewPivot` rotates on its own local Y (horizontal drag) and local X (vertical drag). Camera stays at its current spherical position. Model spins in place. |

---

## Notes

- Pan speed is automatically scaled by frustum height so panning feels consistent at all zoom levels.
- Elevation is clamped to ±80° to avoid gimbal lock at the poles.
- Zoom uses a **fractional** formula (`distance *= 1 − scroll × speed`) so scrolling feels proportional at any distance.
- Inertia applies only to **orbit**. In Local-Axis mode, inertia continues to spin the pivot after you release the mouse.
- On Android, the double-tap reset window is 350 ms.
