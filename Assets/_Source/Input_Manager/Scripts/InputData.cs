using UnityEngine;

namespace InputFramework
{
    // ── Enumerations ───────────────────────────────────────────────────────────

    public enum SwipeDirection { None, Up, Down, Left, Right }
    public enum InputMouseButton { Left = 0, Right = 1, Middle = 2 }

    // ── Gesture / Touch structs ────────────────────────────────────────────────

    public readonly struct TapInfo
    {
        public readonly Vector2 ScreenPosition;
        public readonly int     FingerId;
        public TapInfo(Vector2 pos, int id) { ScreenPosition = pos; FingerId = id; }
    }

    public readonly struct SwipeInfo
    {
        public readonly Vector2        Start;
        public readonly Vector2        End;
        public readonly SwipeDirection Direction;
        public readonly float          Speed;    // pixels/sec
        public readonly float          Distance; // pixels
        public readonly int            FingerId;
        public SwipeInfo(Vector2 s, Vector2 e, SwipeDirection d, float sp, float dist, int id)
        { Start = s; End = e; Direction = d; Speed = sp; Distance = dist; FingerId = id; }
    }

    public readonly struct LongPressInfo
    {
        public readonly Vector2 ScreenPosition;
        public readonly int     FingerId;
        public LongPressInfo(Vector2 pos, int id) { ScreenPosition = pos; FingerId = id; }
    }

    public readonly struct PinchInfo
    {
        public readonly Vector2 Center;
        public readonly float   DeltaDistance; // positive = spread, negative = squeeze
        public readonly float   CurrentDistance;
        public readonly float   ScaleFactor;   // currentDist / startDist
        public PinchInfo(Vector2 c, float dd, float cd, float sf)
        { Center = c; DeltaDistance = dd; CurrentDistance = cd; ScaleFactor = sf; }
    }

    public readonly struct PanInfo
    {
        public readonly Vector2 Position;
        public readonly Vector2 Delta;
        public readonly int     FingerCount;
        public PanInfo(Vector2 pos, Vector2 delta, int fingers)
        { Position = pos; Delta = delta; FingerCount = fingers; }
    }

    public readonly struct RotateInfo
    {
        public readonly Vector2 Center;
        public readonly float   DeltaAngle; // degrees; positive = counter-clockwise
        public RotateInfo(Vector2 c, float da) { Center = c; DeltaAngle = da; }
    }

    // ── Mouse structs ──────────────────────────────────────────────────────────

    public readonly struct MouseButtonInfo
    {
        public readonly InputMouseButton Button;
        public readonly Vector2          ScreenPosition;
        public MouseButtonInfo(InputMouseButton b, Vector2 pos) { Button = b; ScreenPosition = pos; }
    }

    public readonly struct MouseDragInfo
    {
        public readonly InputMouseButton Button;
        public readonly Vector2          ScreenPosition;
        public readonly Vector2          Delta;
        public MouseDragInfo(InputMouseButton b, Vector2 pos, Vector2 d)
        { Button = b; ScreenPosition = pos; Delta = d; }
    }

    // ── Gamepad structs ────────────────────────────────────────────────────────

    public readonly struct GamepadButtonInfo
    {
        public readonly string ButtonName;
        public readonly bool   IsDown; // true = pressed, false = released
        public GamepadButtonInfo(string n, bool down) { ButtonName = n; IsDown = down; }
    }

    public readonly struct GamepadAxisInfo
    {
        public readonly string AxisName;
        public readonly float  Value;
        public GamepadAxisInfo(string n, float v) { AxisName = n; Value = v; }
    }
}
