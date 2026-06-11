using System.Collections.Generic;
using UnityEngine;

namespace InputFramework
{
    // Plain C# class — ticked by InputManager.Update(). No MonoBehaviour needed.
    // Polls Unity's legacy joystick button/axis API (works without the Input System package).
    internal sealed class GamepadHandler
    {
        // Unity names for joystick buttons 0–19 (cross-platform; covers most gamepads)
        private static readonly string[] ButtonNames;

        // Axes 3–8: typically left trigger, right trigger, right-stick X/Y, bumpers
        private static readonly string[] AxisNames =
        {
            "3rd Axis", "4th Axis", "5th Axis", "6th Axis", "7th Axis", "8th Axis",
        };

        static GamepadHandler()
        {
            ButtonNames = new string[20];
            for (int i = 0; i < 20; i++)
                ButtonNames[i] = $"joystick button {i}";
        }

        internal float DeadZone = 0.1f;

        private readonly InputManager    _mgr;
        private readonly HashSet<string> _heldButtons   = new HashSet<string>();
        private readonly float[]         _prevAxisValues = new float[AxisNames.Length];

        internal GamepadHandler(InputManager mgr) => _mgr = mgr;

        internal void Update()
        {
            var joystickNames = Input.GetJoystickNames();
            bool connected = joystickNames.Length > 0 && !string.IsNullOrEmpty(joystickNames[0]);
            if (!connected) return;

            PollButtons();
            PollAxes();
        }

        private void PollButtons()
        {
            foreach (string btn in ButtonNames)
            {
                bool held = false;
                try { held = Input.GetButton(btn); }
                catch { continue; } // axis/button name not present in Input settings

                if (held && _heldButtons.Add(btn))
                    _mgr.RaiseGamepadButton(new GamepadButtonInfo(btn, true));
                else if (!held && _heldButtons.Remove(btn))
                    _mgr.RaiseGamepadButton(new GamepadButtonInfo(btn, false));
            }
        }

        private void PollAxes()
        {
            for (int i = 0; i < AxisNames.Length; i++)
            {
                float val = 0f;
                try { val = Input.GetAxisRaw(AxisNames[i]); }
                catch { continue; }

                float prev    = _prevAxisValues[i];
                bool active   = Mathf.Abs(val)        > DeadZone;
                bool wasActive= Mathf.Abs(prev)        > DeadZone;
                bool changed  = Mathf.Abs(val - prev) > 0.05f;

                if ((active || wasActive) && changed)
                {
                    _mgr.RaiseGamepadAxis(new GamepadAxisInfo(AxisNames[i], val));
                    _prevAxisValues[i] = val;
                }
            }
        }
    }
}
