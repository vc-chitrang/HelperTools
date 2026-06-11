using System;
using UnityEngine;

namespace InputFramework
{
    /// <summary>
    /// Singleton entry-point for the Input Framework.
    /// Drag the <c>InputManager.prefab</c> into any scene — it persists across
    /// scene loads and routes all input events to subscribers.
    ///
    /// <b>Supported inputs:</b>
    /// Keyboard · Mouse (3 buttons, scroll, drag) · Touch gestures (tap, double-tap,
    /// long-press, swipe, pan, pinch, rotate) · Gamepad (buttons + axes).
    ///
    /// <b>Usage:</b>
    /// <code>
    /// InputManager.Instance.OnTap    += info => DoSomething(info.ScreenPosition);
    /// InputManager.Instance.OnKeyDown += key => if (key == KeyCode.Space) Jump();
    /// </code>
    /// </summary>
    [AddComponentMenu("HelperTools/Input/Input Manager")]
    [DisallowMultipleComponent]
    public sealed class InputManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static InputManager Instance { get; private set; }

        // ── Inspector — Named Actions ──────────────────────────────────────────
        [Header("Named Actions (optional)")]
        [Tooltip("ScriptableObject with rebindable named actions. " +
                 "Create via Right-click → Create → HelperTools → Input Actions Asset.")]
        [SerializeField] private InputActionAsset _actionAsset;

        // ── Inspector — Gesture Thresholds ─────────────────────────────────────
        [Header("Gesture Thresholds")]
        [Tooltip("Max seconds from touch-down to touch-up to register as a tap.")]
        [SerializeField] private float _tapMaxTime      = 0.30f;
        [Tooltip("Max pixels a touch can drift and still count as a tap.")]
        [SerializeField] private float _tapMaxMovePx    = 20f;
        [Tooltip("Max seconds between two taps to register as a double-tap.")]
        [SerializeField] private float _doubleTapWindow = 0.35f;
        [Tooltip("Minimum seconds a stationary touch must be held to fire LongPress.")]
        [SerializeField] private float _longPressTime   = 0.60f;
        [Tooltip("Minimum pixel distance a touch must travel to register as a swipe.")]
        [SerializeField] private float _swipeMinDistPx  = 60f;
        [Tooltip("Minimum pixels/sec required to classify a fast move as a swipe (vs. pan).")]
        [SerializeField] private float _swipeMinSpeedPx = 300f;
        [Tooltip("Pixel distance a touch must move before OnPanStarted fires.")]
        [SerializeField] private float _panStartMovePx  = 10f;

        // ── Inspector — Gamepad ────────────────────────────────────────────────
        [Header("Gamepad")]
        [Tooltip("Axis values below this threshold are ignored (prevents stick drift).")]
        [SerializeField] private float _gamepadDeadZone = 0.1f;

        // ── Events — Touch / Gestures ──────────────────────────────────────────

        /// <summary>Fired for a quick touch and release (< TapMaxTime, < TapMaxMovePx).</summary>
        public event Action<TapInfo>       OnTap;
        /// <summary>Fired when a second tap follows the first within DoubleTapWindow.</summary>
        public event Action<TapInfo>       OnDoubleTap;
        /// <summary>Fired when a stationary touch is held for ≥ LongPressTime seconds.</summary>
        public event Action<LongPressInfo> OnLongPressStarted;
        /// <summary>Fired when the long-pressing finger lifts.</summary>
        public event Action<LongPressInfo> OnLongPressEnded;
        /// <summary>Fired when a fast swipe is completed. Includes direction, distance, speed.</summary>
        public event Action<SwipeInfo>     OnSwipe;
        /// <summary>Fired once when a drag passes the PanStartMovePx threshold.</summary>
        public event Action<PanInfo>       OnPanStarted;
        /// <summary>Fired every frame during a drag. Works with 1 or 2 fingers.</summary>
        public event Action<PanInfo>       OnPan;
        /// <summary>Fired when the dragging finger(s) lift.</summary>
        public event Action<PanInfo>       OnPanEnded;
        /// <summary>Fired every frame two fingers are changing distance. ScaleFactor &gt; 1 = spread.</summary>
        public event Action<PinchInfo>     OnPinch;
        /// <summary>Fired every frame two fingers are rotating. DeltaAngle in degrees.</summary>
        public event Action<RotateInfo>    OnRotate;

        // ── Events — Mouse ─────────────────────────────────────────────────────

        /// <summary>Fired when any mouse button is pressed.</summary>
        public event Action<MouseButtonInfo> OnMouseDown;
        /// <summary>Fired when any mouse button is released.</summary>
        public event Action<MouseButtonInfo> OnMouseUp;
        /// <summary>Fired every frame a mouse button is held and the cursor moves.</summary>
        public event Action<MouseDragInfo>   OnMouseDrag;
        /// <summary>Fired every frame the cursor moves (reports current screen position).</summary>
        public event Action<Vector2>         OnMouseMove;
        /// <summary>Fired when the scroll wheel moves (positive = up).</summary>
        public event Action<float>           OnMouseScroll;

        // ── Events — Keyboard ──────────────────────────────────────────────────

        /// <summary>Fired on the first frame a watched key is pressed.</summary>
        public event Action<KeyCode> OnKeyDown;
        /// <summary>Fired on the frame a watched key is released.</summary>
        public event Action<KeyCode> OnKeyUp;
        /// <summary>Fired every frame a watched key is held.</summary>
        public event Action<KeyCode> OnKeyHeld;
        /// <summary>Fired on any key-down event (wraps Input.anyKeyDown).</summary>
        public event Action          OnAnyKeyDown;

        // ── Events — Gamepad ───────────────────────────────────────────────────

        /// <summary>Fired when a joystick button is pressed or released.</summary>
        public event Action<GamepadButtonInfo> OnGamepadButton;
        /// <summary>Fired when a joystick axis value changes beyond the dead-zone.</summary>
        public event Action<GamepadAxisInfo>   OnGamepadAxis;

        // ── Private ────────────────────────────────────────────────────────────

        private KeyboardMouseHandler _kbMouse;
        private TouchGestureHandler  _touch;
        private GamepadHandler       _gamepad;

        // ── Public API — Named Actions ─────────────────────────────────────────

        /// <summary>Returns the <see cref="InputAction"/> with the given name, or null.</summary>
        public InputAction FindAction(string name) => _actionAsset?.FindAction(name);

        /// <summary>
        /// Re-binds a named action's primary key at runtime.
        /// Optionally sets the alternate key too.
        /// </summary>
        public void RebindAction(string name, KeyCode primary, KeyCode alternate = KeyCode.None)
        {
            var action = FindAction(name);
            if (action == null)
            {
                Debug.LogWarning($"[InputManager] RebindAction: action '{name}' not found.");
                return;
            }
            action.Rebind(primary, false);
            action.Rebind(alternate, true);
        }

        /// <summary>Adds a key to the per-frame keyboard watch list (idempotent).</summary>
        public void AddWatchedKey(KeyCode key) => _kbMouse?.AddWatchedKey(key);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _kbMouse = new KeyboardMouseHandler(this);
            _touch   = new TouchGestureHandler(this)
            {
                TapMaxTime      = _tapMaxTime,
                TapMaxMovePx    = _tapMaxMovePx,
                DoubleTapWindow = _doubleTapWindow,
                LongPressTime   = _longPressTime,
                SwipeMinDistPx  = _swipeMinDistPx,
                SwipeMinSpeedPx = _swipeMinSpeedPx,
                PanStartMovePx  = _panStartMovePx,
            };
            _gamepad = new GamepadHandler(this) { DeadZone = _gamepadDeadZone };
        }

        private void Update()
        {
            _kbMouse.Update();
            _touch.Update();
            _gamepad.Update();

            if (_actionAsset != null)
                foreach (var a in _actionAsset.Actions)
                    a?.Poll();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Internal raise helpers — called by handler classes ─────────────────

        internal void RaiseTap(TapInfo i)                    => OnTap?.Invoke(i);
        internal void RaiseDoubleTap(TapInfo i)              => OnDoubleTap?.Invoke(i);
        internal void RaiseLongPressStarted(LongPressInfo i) => OnLongPressStarted?.Invoke(i);
        internal void RaiseLongPressEnded(LongPressInfo i)   => OnLongPressEnded?.Invoke(i);
        internal void RaiseSwipe(SwipeInfo i)                => OnSwipe?.Invoke(i);
        internal void RaisePanStarted(PanInfo i)             => OnPanStarted?.Invoke(i);
        internal void RaisePan(PanInfo i)                    => OnPan?.Invoke(i);
        internal void RaisePanEnded(PanInfo i)               => OnPanEnded?.Invoke(i);
        internal void RaisePinch(PinchInfo i)                => OnPinch?.Invoke(i);
        internal void RaiseRotate(RotateInfo i)              => OnRotate?.Invoke(i);
        internal void RaiseMouseDown(MouseButtonInfo i)      => OnMouseDown?.Invoke(i);
        internal void RaiseMouseUp(MouseButtonInfo i)        => OnMouseUp?.Invoke(i);
        internal void RaiseMouseDrag(MouseDragInfo i)        => OnMouseDrag?.Invoke(i);
        internal void RaiseMouseMove(Vector2 pos)            => OnMouseMove?.Invoke(pos);
        internal void RaiseMouseScroll(float v)              => OnMouseScroll?.Invoke(v);
        internal void RaiseKeyDown(KeyCode k)                => OnKeyDown?.Invoke(k);
        internal void RaiseKeyUp(KeyCode k)                  => OnKeyUp?.Invoke(k);
        internal void RaiseKeyHeld(KeyCode k)                => OnKeyHeld?.Invoke(k);
        internal void RaiseAnyKeyDown()                      => OnAnyKeyDown?.Invoke();
        internal void RaiseGamepadButton(GamepadButtonInfo i)=> OnGamepadButton?.Invoke(i);
        internal void RaiseGamepadAxis(GamepadAxisInfo i)    => OnGamepadAxis?.Invoke(i);
    }
}
