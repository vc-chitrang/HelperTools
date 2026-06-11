using System.Collections.Generic;
using UnityEngine;

namespace InputFramework
{
    // Plain C# class — ticked by InputManager.Update(). No MonoBehaviour needed.
    internal sealed class KeyboardMouseHandler
    {
        // Common keys checked every frame. Users can extend via AddWatchedKey().
        private static readonly KeyCode[] DefaultWatchedKeys =
        {
            KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F,
            KeyCode.G, KeyCode.H, KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L,
            KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.P, KeyCode.Q, KeyCode.R,
            KeyCode.S, KeyCode.T, KeyCode.U, KeyCode.V, KeyCode.W, KeyCode.X,
            KeyCode.Y, KeyCode.Z,
            KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7,
            KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.F1,  KeyCode.F2,  KeyCode.F3,  KeyCode.F4,
            KeyCode.F5,  KeyCode.F6,  KeyCode.F7,  KeyCode.F8,
            KeyCode.F9,  KeyCode.F10, KeyCode.F11, KeyCode.F12,
            KeyCode.Space,     KeyCode.Return,   KeyCode.Escape,
            KeyCode.Backspace, KeyCode.Tab,       KeyCode.Delete,
            KeyCode.Home,      KeyCode.End,       KeyCode.Insert,
            KeyCode.PageUp,    KeyCode.PageDown,
            KeyCode.UpArrow,   KeyCode.DownArrow,
            KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.LeftShift,   KeyCode.RightShift,
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftAlt,     KeyCode.RightAlt,
            KeyCode.LeftCommand, KeyCode.RightCommand,
            KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2,
            KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5,
            KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
            KeyCode.KeypadEnter,    KeyCode.KeypadPlus,     KeyCode.KeypadMinus,
            KeyCode.KeypadMultiply, KeyCode.KeypadDivide,   KeyCode.KeypadPeriod,
            KeyCode.Minus, KeyCode.Equals, KeyCode.LeftBracket, KeyCode.RightBracket,
            KeyCode.Semicolon, KeyCode.Quote, KeyCode.Comma, KeyCode.Period, KeyCode.Slash,
            KeyCode.BackQuote, KeyCode.Backslash,
        };

        private readonly InputManager    _mgr;
        private readonly List<KeyCode>   _extra = new List<KeyCode>();
        private Vector2                  _prevMousePos;

        internal KeyboardMouseHandler(InputManager mgr)
        {
            _mgr          = mgr;
            _prevMousePos = Input.mousePosition;
        }

        /// <summary>Adds a key to the per-frame watch list (idempotent).</summary>
        internal void AddWatchedKey(KeyCode key)
        {
            if (!_extra.Contains(key)) _extra.Add(key);
        }

        internal void Update()
        {
            if (Input.anyKeyDown) _mgr.RaiseAnyKeyDown();

            foreach (var kc in DefaultWatchedKeys) CheckKey(kc);
            foreach (var kc in _extra)             CheckKey(kc);

            PollMouse();
        }

        private void CheckKey(KeyCode kc)
        {
            if (Input.GetKeyDown(kc)) _mgr.RaiseKeyDown(kc);
            if (Input.GetKeyUp(kc))   _mgr.RaiseKeyUp(kc);
            if (Input.GetKey(kc))     _mgr.RaiseKeyHeld(kc);
        }

        private void PollMouse()
        {
            Vector2 pos   = Input.mousePosition;
            Vector2 delta = pos - _prevMousePos;

            if (delta.sqrMagnitude > 0.01f)
                _mgr.RaiseMouseMove(pos);

            for (int i = 0; i <= 2; i++)
            {
                var btn = (InputMouseButton)i;
                if (Input.GetMouseButtonDown(i))
                    _mgr.RaiseMouseDown(new MouseButtonInfo(btn, pos));
                if (Input.GetMouseButtonUp(i))
                    _mgr.RaiseMouseUp(new MouseButtonInfo(btn, pos));
                if (Input.GetMouseButton(i) && delta.sqrMagnitude > 0.01f)
                    _mgr.RaiseMouseDrag(new MouseDragInfo(btn, pos, delta));
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
                _mgr.RaiseMouseScroll(scroll);

            _prevMousePos = pos;
        }
    }
}
