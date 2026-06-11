using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InputFramework
{
    /// <summary>
    /// Subscribes to all <see cref="InputManager"/> events and displays them live
    /// in the Input Manager demo scene.
    /// </summary>
    [AddComponentMenu("HelperTools/Input/Input Demo Controller")]
    public sealed class InputDemoController : MonoBehaviour
    {
        [Header("Event Log")]
        [SerializeField] private TMP_Text _logText;
        [SerializeField] private int      _maxLogLines = 18;

        [Header("Status — Mouse")]
        [SerializeField] private TMP_Text _mousePosText;
        [SerializeField] private TMP_Text _mouseButtonsText;
        [SerializeField] private TMP_Text _mouseScrollText;

        [Header("Status — Touch")]
        [SerializeField] private TMP_Text _touchStatusText;

        [Header("Status — Keyboard")]
        [SerializeField] private TMP_Text _keyModifiersText;

        [Header("Status — Gamepad")]
        [SerializeField] private TMP_Text _gamepadStatusText;

        [Header("Gesture Indicator")]
        [SerializeField] private TMP_Text _gestureNameText;
        [SerializeField] private Image    _gestureFlashImage;

        // ── Private state ──────────────────────────────────────────────────────

        private readonly Queue<string> _logQueue = new Queue<string>();
        private float                  _flashTimer;
        private static readonly Color  _flashColor = new Color(0.18f, 0.82f, 0.45f, 1f);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            // Wait one frame before subscribing so InputManager.Awake() has run.
            var m = InputManager.Instance;
            if (m == null)
            {
                Log("<color=#FF6666>InputManager not found! Add InputManager.prefab to the scene.</color>");
                return;
            }

            // Touch / Gesture
            m.OnTap              += OnTap;
            m.OnDoubleTap        += OnDoubleTap;
            m.OnLongPressStarted += OnLongPressStarted;
            m.OnLongPressEnded   += OnLongPressEnded;
            m.OnSwipe            += OnSwipe;
            m.OnPanStarted       += OnPanStarted;
            m.OnPanEnded         += OnPanEnded;
            m.OnPinch            += OnPinch;
            m.OnRotate           += OnRotate;

            // Mouse
            m.OnMouseDown        += OnMouseDown;
            m.OnMouseUp          += OnMouseUp;
            m.OnMouseScroll      += OnMouseScroll;

            // Keyboard
            m.OnKeyDown          += OnKeyDown;
            m.OnKeyUp            += OnKeyUp;
            m.OnAnyKeyDown       += OnAnyKeyDown;

            // Gamepad
            m.OnGamepadButton    += OnGamepadButton;
            m.OnGamepadAxis      += OnGamepadAxis;

            Log($"<color=#88CCFF>Platform: {Application.platform}</color>");
            Log($"<color=#88CCFF>Touch supported: {Input.touchSupported}</color>");
            Log("<color=#AAFFAA>Waiting for input…</color>");
        }

        private void OnDestroy()
        {
            var m = InputManager.Instance;
            if (m == null) return;

            m.OnTap              -= OnTap;
            m.OnDoubleTap        -= OnDoubleTap;
            m.OnLongPressStarted -= OnLongPressStarted;
            m.OnLongPressEnded   -= OnLongPressEnded;
            m.OnSwipe            -= OnSwipe;
            m.OnPanStarted       -= OnPanStarted;
            m.OnPanEnded         -= OnPanEnded;
            m.OnPinch            -= OnPinch;
            m.OnRotate           -= OnRotate;
            m.OnMouseDown        -= OnMouseDown;
            m.OnMouseUp          -= OnMouseUp;
            m.OnMouseScroll      -= OnMouseScroll;
            m.OnKeyDown          -= OnKeyDown;
            m.OnKeyUp            -= OnKeyUp;
            m.OnAnyKeyDown       -= OnAnyKeyDown;
            m.OnGamepadButton    -= OnGamepadButton;
            m.OnGamepadAxis      -= OnGamepadAxis;
        }

        private void Update()
        {
            RefreshStatusPanels();

            // Fade gesture flash
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_gestureFlashImage != null)
                    _gestureFlashImage.color = Color.Lerp(Color.clear, _flashColor, _flashTimer / 0.5f);
            }
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void OnTap(TapInfo i)              { Log($"<color=#FFFF88>Tap</color> @ {Pos(i.ScreenPosition)} finger={i.FingerId}"); Gesture("TAP"); }
        private void OnDoubleTap(TapInfo i)        { Log($"<color=#FFDD44>Double Tap</color> @ {Pos(i.ScreenPosition)}");               Gesture("DOUBLE TAP"); }
        private void OnLongPressStarted(LongPressInfo i) { Log($"<color=#FF8844>Long Press ▼</color> @ {Pos(i.ScreenPosition)}");       Gesture("LONG PRESS"); }
        private void OnLongPressEnded(LongPressInfo i)   { Log($"<color=#FF8844>Long Press ▲</color> @ {Pos(i.ScreenPosition)}"); }

        private void OnSwipe(SwipeInfo i)
        {
            string arrow = i.Direction switch
            {
                SwipeDirection.Up    => "▲",
                SwipeDirection.Down  => "▼",
                SwipeDirection.Left  => "◄",
                SwipeDirection.Right => "►",
                _                   => "?"
            };
            Log($"<color=#88FFFF>Swipe {arrow} {i.Direction}</color>  dist={i.Distance:F0}px  speed={i.Speed:F0}px/s");
            Gesture($"SWIPE {i.Direction.ToString().ToUpper()}");
        }

        private void OnPanStarted(PanInfo i) { Log($"<color=#AAFFCC>Pan Start</color> ({i.FingerCount} finger)"); }
        private void OnPanEnded(PanInfo i)   { Log("<color=#AAFFCC>Pan End</color>"); }

        private void OnPinch(PinchInfo i)
        {
            string label = i.DeltaDistance > 0 ? "Spread" : "Pinch";
            Log($"<color=#FF88FF>{label}</color>  Δ={i.DeltaDistance:F1}px  scale={i.ScaleFactor:F3}");
            Gesture(label.ToUpper());
        }

        private void OnRotate(RotateInfo i)
        {
            Log($"<color=#FFAAFF>Rotate</color>  Δangle={i.DeltaAngle:F1}°");
            Gesture($"ROTATE {i.DeltaAngle:F0}°");
        }

        private void OnMouseDown(MouseButtonInfo i)   => Log($"Mouse {i.Button} <color=#88FF88>▼</color> @ {Pos(i.ScreenPosition)}");
        private void OnMouseUp(MouseButtonInfo i)     => Log($"Mouse {i.Button} <color=#FF8888>▲</color> @ {Pos(i.ScreenPosition)}");
        private void OnMouseScroll(float v)           => Log($"Scroll  {(v > 0 ? "▲" : "▼")}  {v:F2}");
        private void OnKeyDown(KeyCode k)             => Log($"Key <color=#88DDFF>▼</color>  {k}");
        private void OnKeyUp(KeyCode k)               => Log($"Key <color=#FFAA88>▲</color>  {k}");
        private void OnAnyKeyDown()                   { /* covered by OnKeyDown */ }
        private void OnGamepadButton(GamepadButtonInfo i) => Log($"Gamepad btn <b>{i.ButtonName}</b>  {(i.IsDown ? "▼" : "▲")}");
        private void OnGamepadAxis(GamepadAxisInfo i)     => Log($"Gamepad axis <b>{i.AxisName}</b>  {i.Value:F2}");

        // ── Status panels (polled every frame) ────────────────────────────────

        private void RefreshStatusPanels()
        {
            // Mouse position + buttons
            if (_mousePosText != null)
            {
                Vector2 mp = Input.mousePosition;
                _mousePosText.text = $"Pos: ({mp.x:F0}, {mp.y:F0})";
            }
            if (_mouseButtonsText != null)
            {
                string l = Input.GetMouseButton(0) ? "<color=#00FF88>[L]</color>" : "[L]";
                string r = Input.GetMouseButton(1) ? "<color=#00FF88>[R]</color>" : "[R]";
                string m = Input.GetMouseButton(2) ? "<color=#00FF88>[M]</color>" : "[M]";
                _mouseButtonsText.text = $"{l}  {r}  {m}";
            }
            if (_mouseScrollText != null)
                _mouseScrollText.text = $"Scroll: {Input.mouseScrollDelta.y:F2}";

            // Touch
            if (_touchStatusText != null)
            {
                if (Input.touchCount == 0)
                {
                    _touchStatusText.text = "No touches";
                }
                else
                {
                    var sb = new StringBuilder($"{Input.touchCount} touch(es)\n");
                    for (int i = 0; i < Mathf.Min(Input.touchCount, 4); i++)
                    {
                        var t = Input.GetTouch(i);
                        sb.AppendLine($"  [{t.fingerId}] {Pos(t.position)}  {t.phase}");
                    }
                    _touchStatusText.text = sb.ToString();
                }
            }

            // Key modifiers
            if (_keyModifiersText != null)
            {
                var mods = new StringBuilder();
                if (Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift))   mods.Append("SHIFT ");
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) mods.Append("CTRL ");
                if (Input.GetKey(KeyCode.LeftAlt)     || Input.GetKey(KeyCode.RightAlt))     mods.Append("ALT ");
                if (Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)) mods.Append("CMD ");
                _keyModifiersText.text = mods.Length > 0 ? mods.ToString() : "none";
            }

            // Gamepad
            if (_gamepadStatusText != null)
            {
                var joys = Input.GetJoystickNames();
                if (joys.Length == 0 || string.IsNullOrEmpty(joys[0]))
                    _gamepadStatusText.text = "Not connected";
                else
                    _gamepadStatusText.text = joys[0];
            }
        }

        // ── Log helpers ────────────────────────────────────────────────────────

        private void Log(string msg)
        {
            _logQueue.Enqueue($"<color=#666666>{Time.time:F2}s</color>  {msg}");
            while (_logQueue.Count > _maxLogLines) _logQueue.Dequeue();
            if (_logText != null)
                _logText.text = string.Join("\n", _logQueue);
        }

        private void Gesture(string name)
        {
            if (_gestureNameText != null) _gestureNameText.text = name;
            _flashTimer = 0.5f;
            if (_gestureFlashImage != null) _gestureFlashImage.color = _flashColor;
        }

        private static string Pos(Vector2 v) => $"({v.x:F0},{v.y:F0})";
    }
}
