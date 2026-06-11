# Input Framework — System #14

Event-driven input manager for Unity. Covers all major input types with a single drag-and-drop prefab.

**Platforms:** Unity Editor · Windows · macOS · Android · iOS

---

## Quick Start

1. Drag **`Prefabs/InputManager.prefab`** into your scene hierarchy.  
2. It survives scene loads (`DontDestroyOnLoad`). Only one instance is needed per project.
3. Subscribe to events from any script:

```csharp
using InputFramework;

void OnEnable()
{
    InputManager.Instance.OnTap    += HandleTap;
    InputManager.Instance.OnSwipe  += HandleSwipe;
    InputManager.Instance.OnKeyDown += HandleKey;
}

void OnDisable()
{
    InputManager.Instance.OnTap    -= HandleTap;
    InputManager.Instance.OnSwipe  -= HandleSwipe;
    InputManager.Instance.OnKeyDown -= HandleKey;
}

void HandleTap(TapInfo i)        => Debug.Log($"Tapped at {i.ScreenPosition}");
void HandleSwipe(SwipeInfo i)    => Debug.Log($"Swiped {i.Direction}");
void HandleKey(KeyCode k)        => Debug.Log($"Key pressed: {k}");
```

---

## All Events

### Touch & Gesture
| Event | Data | Notes |
|---|---|---|
| `OnTap` | `TapInfo` | Quick touch & release |
| `OnDoubleTap` | `TapInfo` | Two taps within `DoubleTapWindow` seconds |
| `OnLongPressStarted` | `LongPressInfo` | Stationary touch held ≥ `LongPressTime` |
| `OnLongPressEnded` | `LongPressInfo` | Long-press finger lifted |
| `OnSwipe` | `SwipeInfo` | Fast directional move; includes direction, speed, distance |
| `OnPanStarted` | `PanInfo` | Drag begins (1 or 2 fingers) |
| `OnPan` | `PanInfo` | Every frame during a drag; `Delta` is frame displacement |
| `OnPanEnded` | `PanInfo` | Drag finger(s) lifted |
| `OnPinch` | `PinchInfo` | Two-finger spread/squeeze; `ScaleFactor` for camera zoom |
| `OnRotate` | `RotateInfo` | Two-finger rotation; `DeltaAngle` in degrees |

> On **desktop / Editor**, the left mouse button simulates a single touch, giving you tap, double-tap, long-press, swipe, and pan testing without a touchscreen.

### Mouse
| Event | Data |
|---|---|
| `OnMouseDown` | `MouseButtonInfo` (button, position) |
| `OnMouseUp` | `MouseButtonInfo` |
| `OnMouseDrag` | `MouseDragInfo` (button, position, frame delta) |
| `OnMouseMove` | `Vector2` current screen position |
| `OnMouseScroll` | `float` scroll delta (positive = up) |

### Keyboard
| Event | Data |
|---|---|
| `OnKeyDown` | `KeyCode` |
| `OnKeyUp` | `KeyCode` |
| `OnKeyHeld` | `KeyCode` (fires every frame while held) |
| `OnAnyKeyDown` | — |

Keyboard events fire for 85 common keys. Add more at runtime:
```csharp
InputManager.Instance.AddWatchedKey(KeyCode.Joystick1Button0);
```

### Gamepad
| Event | Data |
|---|---|
| `OnGamepadButton` | `GamepadButtonInfo` (name, isDown) — joystick buttons 0-19 |
| `OnGamepadAxis` | `GamepadAxisInfo` (name, value) — axes 3-8 (triggers, right stick) |

---

## Named Actions & Rebinding

1. Create an asset: **Right-click → Create → HelperTools → Input Actions Asset**
2. Add actions in the Inspector (name + primary key + optional alternate key)
3. Assign the asset to `InputManager._actionAsset`
4. Subscribe to action events:

```csharp
// Subscribe to a specific action
var jump = InputManager.Instance.FindAction("Jump");
if (jump != null)
{
    jump.OnStarted  += () => Debug.Log("Jump pressed");
    jump.OnCanceled += () => Debug.Log("Jump released");
}

// Poll manually
bool isJumping = InputManager.Instance.FindAction("Jump")?.IsDown ?? false;

// Rebind at runtime
InputManager.Instance.RebindAction("Jump", KeyCode.Space, KeyCode.W);
```

A `DefaultInputActions.asset` with four pre-configured actions (Jump, Fire, Interact, Pause) is created automatically by the builder and lives in `ScriptableObjects/`.

---

## Inspector Settings

| Property | Default | Description |
|---|---|---|
| `_tapMaxTime` | 0.3 s | Max touch duration for a tap |
| `_tapMaxMovePx` | 20 px | Max drift for a tap |
| `_doubleTapWindow` | 0.35 s | Max gap between two taps |
| `_longPressTime` | 0.6 s | Hold duration before long-press fires |
| `_swipeMinDistPx` | 60 px | Minimum swipe travel |
| `_swipeMinSpeedPx` | 300 px/s | Minimum speed to classify as swipe vs. pan |
| `_panStartMovePx` | 10 px | Movement before `OnPanStarted` fires |
| `_gamepadDeadZone` | 0.1 | Axis values below this are ignored |

---

## Demo Scene

Open via **Tools → Input Manager → Open Demo Scene** or directly at  
`Assets/_Source/Input_Manager/Scenes/InputManagerScene.unity`.

The scene shows:
- **Event Log** (left) — last 18 events with timestamps and color-coded types
- **Input Status** (top right) — live mouse position/buttons, touch finger list, modifiers, gamepad name
- **Gesture Indicator** (bottom right) — flashes the name of the most recent gesture

---

## File Structure

```
Input_Manager/
├── Scripts/
│   ├── InputManager.cs          — Singleton MonoBehaviour, all events
│   ├── InputData.cs             — Enums + readonly data structs
│   ├── InputAction.cs           — Rebindable named action
│   ├── InputActionAsset.cs      — ScriptableObject action list
│   ├── KeyboardMouseHandler.cs  — Keyboard + mouse polling
│   ├── TouchGestureHandler.cs   — Touch + gesture state machine
│   ├── GamepadHandler.cs        — Joystick button + axis polling
│   ├── InputDemoController.cs   — Demo scene UI controller
│   └── Editor/
│       └── InputManagerMenu.cs  — Tools/Input Manager menu
├── Prefabs/
│   └── InputManager.prefab      — Drag into any scene
├── ScriptableObjects/
│   └── DefaultInputActions.asset
├── Scenes/
│   └── InputManagerScene.unity
└── README.md
```

---

## Notes

- Built on **Unity's legacy Input system** — no extra packages required.
- `DontDestroyOnLoad` means one prefab instance survives all scene transitions.
- Always subscribe in `OnEnable`/`Start` and **unsubscribe in `OnDisable`/`OnDestroy`** to avoid dangling references.
- Pinch and rotate require two physical fingers; they cannot be simulated from a mouse.
