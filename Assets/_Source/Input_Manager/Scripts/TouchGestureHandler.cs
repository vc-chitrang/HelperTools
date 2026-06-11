using System.Collections.Generic;
using UnityEngine;

namespace InputFramework
{
    // Plain C# class — ticked by InputManager.Update(). No MonoBehaviour needed.
    // Detects: Tap, DoubleTap, LongPress, Swipe (4 dirs), Pan (1-2 fingers), Pinch, Rotate.
    // On non-touch platforms (Editor/PC) the left mouse button simulates a single touch.
    internal sealed class TouchGestureHandler
    {
        private readonly InputManager _mgr;

        // ── Configurable thresholds (set from InputManager inspector values) ──
        internal float TapMaxTime      = 0.30f;
        internal float TapMaxMovePx    = 20f;
        internal float DoubleTapWindow = 0.35f;
        internal float LongPressTime   = 0.60f;
        internal float SwipeMinDistPx  = 60f;
        internal float SwipeMinSpeedPx = 300f; // pixels/sec
        internal float PanStartMovePx  = 10f;

        // ── Per-finger tracking ────────────────────────────────────────────────
        private class FingerData
        {
            public int     FingerId;
            public Vector2 StartPos;
            public Vector2 PrevPos;
            public float   StartTime;
            public bool    Moved;        // moved past PanStartMovePx
            public bool    LongFired;    // long-press event already sent
            public bool    PanStarted;   // OnPanStarted already sent
        }

        private readonly Dictionary<int, FingerData> _fingers = new Dictionary<int, FingerData>();
        private float   _lastTapTime = -999f;

        // ── Two-finger state ───────────────────────────────────────────────────
        private float   _prevPinchDist;
        private float   _prevPinchAngle;
        private Vector2 _prevPinchMid;
        private bool    _twoFingerInit;

        // ── Mouse-as-single-touch (Editor / standalone fallback) ───────────────
        private bool    _mouseTouchActive;
        private Vector2 _mouseTouchStart;
        private float   _mouseTouchStartTime;
        private bool    _mouseTouchMoved;
        private bool    _mouseTouchLongFired;
        private bool    _mouseTouchPanStarted;
        private Vector2 _mousePrevPos;

        internal TouchGestureHandler(InputManager mgr) => _mgr = mgr;

        internal void Update()
        {
            if (Input.touchCount > 0)
                ProcessRealTouch();
            else
                ProcessMouseAsTouch();
        }

        // ── Real touch processing ──────────────────────────────────────────────

        private void ProcessRealTouch()
        {
            int count = Input.touchCount;

            if (count >= 2)
            {
                EndSingleFingerPans();
                ProcessTwoFingerTouch(Input.GetTouch(0), Input.GetTouch(1));
                return;
            }

            _twoFingerInit = false;

            if (count == 1)
                ProcessSingleTouch(Input.GetTouch(0));
        }

        private void EndSingleFingerPans()
        {
            foreach (var kv in _fingers)
                if (kv.Value.PanStarted)
                    _mgr.RaisePanEnded(new PanInfo(kv.Value.PrevPos, Vector2.zero, 1));
            _fingers.Clear();
        }

        private void ProcessSingleTouch(Touch t)
        {
            switch (t.phase)
            {
                case TouchPhase.Began:
                    _fingers[t.fingerId] = new FingerData
                    {
                        FingerId  = t.fingerId,
                        StartPos  = t.position,
                        PrevPos   = t.position,
                        StartTime = Time.realtimeSinceStartup,
                    };
                    return;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                {
                    if (!_fingers.TryGetValue(t.fingerId, out var fd)) return;

                    float held = Time.realtimeSinceStartup - fd.StartTime;
                    float dist = Vector2.Distance(t.position, fd.StartPos);

                    if (dist > PanStartMovePx)
                    {
                        fd.Moved = true;
                        if (!fd.PanStarted)
                        {
                            fd.PanStarted = true;
                            _mgr.RaisePanStarted(new PanInfo(t.position, Vector2.zero, 1));
                        }
                        Vector2 frameDelta = t.position - fd.PrevPos;
                        if (frameDelta.sqrMagnitude > 0.01f)
                            _mgr.RaisePan(new PanInfo(t.position, frameDelta, 1));
                    }

                    if (!fd.Moved && !fd.LongFired && held >= LongPressTime)
                    {
                        fd.LongFired = true;
                        _mgr.RaiseLongPressStarted(new LongPressInfo(t.position, t.fingerId));
                    }

                    fd.PrevPos = t.position;
                    return;
                }

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                {
                    if (!_fingers.TryGetValue(t.fingerId, out var fd)) return;
                    _fingers.Remove(t.fingerId);

                    float duration  = Time.realtimeSinceStartup - fd.StartTime;
                    float totalDist = Vector2.Distance(t.position, fd.StartPos);

                    if (fd.PanStarted)
                        _mgr.RaisePanEnded(new PanInfo(t.position, Vector2.zero, 1));

                    if (fd.LongFired)
                    {
                        _mgr.RaiseLongPressEnded(new LongPressInfo(t.position, t.fingerId));
                        return;
                    }

                    // Swipe takes priority over tap when fast + far
                    if (totalDist >= SwipeMinDistPx && duration > 0.001f)
                    {
                        float speed = totalDist / duration;
                        if (speed >= SwipeMinSpeedPx)
                        {
                            var dir = GetSwipeDir(t.position - fd.StartPos);
                            _mgr.RaiseSwipe(new SwipeInfo(
                                fd.StartPos, t.position, dir, speed, totalDist, t.fingerId));
                            return;
                        }
                    }

                    // Tap / double-tap
                    if (!fd.Moved && duration < TapMaxTime)
                    {
                        float now = Time.realtimeSinceStartup;
                        bool isDouble = (now - _lastTapTime) < DoubleTapWindow;
                        if (isDouble) _mgr.RaiseDoubleTap(new TapInfo(t.position, t.fingerId));
                        else          _mgr.RaiseTap(new TapInfo(t.position, t.fingerId));
                        _lastTapTime = now;
                    }
                    return;
                }
            }
        }

        private void ProcessTwoFingerTouch(Touch t0, Touch t1)
        {
            bool anyBegan = t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began;

            Vector2 mid   = (t0.position + t1.position) * 0.5f;
            float   dist  = Vector2.Distance(t0.position, t1.position);
            float   angle = Mathf.Atan2(
                                t1.position.y - t0.position.y,
                                t1.position.x - t0.position.x) * Mathf.Rad2Deg;

            if (!_twoFingerInit || anyBegan)
            {
                _prevPinchDist  = dist;
                _prevPinchAngle = angle;
                _prevPinchMid   = mid;
                _twoFingerInit  = true;
                return;
            }

            float distDelta  = dist - _prevPinchDist;
            float angleDelta = Mathf.DeltaAngle(_prevPinchAngle, angle);
            Vector2 midDelta = mid - _prevPinchMid;

            if (Mathf.Abs(distDelta) > 0.5f)
            {
                float scale = _prevPinchDist > 0.001f ? dist / _prevPinchDist : 1f;
                _mgr.RaisePinch(new PinchInfo(mid, distDelta, dist, scale));
            }

            if (Mathf.Abs(angleDelta) > 0.5f)
                _mgr.RaiseRotate(new RotateInfo(mid, angleDelta));

            if (midDelta.sqrMagnitude > 1f)
                _mgr.RaisePan(new PanInfo(mid, midDelta, 2));

            _prevPinchDist  = dist;
            _prevPinchAngle = angle;
            _prevPinchMid   = mid;
        }

        // ── Mouse-as-single-touch (non-mobile fallback) ────────────────────────

        private void ProcessMouseAsTouch()
        {
            if (Application.isMobilePlatform) return;

            Vector2 pos = Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
            {
                _mouseTouchActive     = true;
                _mouseTouchStart      = pos;
                _mouseTouchStartTime  = Time.realtimeSinceStartup;
                _mouseTouchMoved      = false;
                _mouseTouchLongFired  = false;
                _mouseTouchPanStarted = false;
                _mousePrevPos         = pos;
                return;
            }

            if (_mouseTouchActive && Input.GetMouseButton(0))
            {
                float held = Time.realtimeSinceStartup - _mouseTouchStartTime;
                float dist = Vector2.Distance(pos, _mouseTouchStart);

                if (dist > PanStartMovePx)
                {
                    _mouseTouchMoved = true;
                    if (!_mouseTouchPanStarted)
                    {
                        _mouseTouchPanStarted = true;
                        _mgr.RaisePanStarted(new PanInfo(pos, Vector2.zero, 1));
                    }
                    Vector2 frameDelta = pos - _mousePrevPos;
                    if (frameDelta.sqrMagnitude > 0.01f)
                        _mgr.RaisePan(new PanInfo(pos, frameDelta, 1));
                }

                if (!_mouseTouchMoved && !_mouseTouchLongFired && held >= LongPressTime)
                {
                    _mouseTouchLongFired = true;
                    _mgr.RaiseLongPressStarted(new LongPressInfo(pos, 0));
                }

                _mousePrevPos = pos;
                return;
            }

            if (_mouseTouchActive && Input.GetMouseButtonUp(0))
            {
                _mouseTouchActive = false;

                float duration  = Time.realtimeSinceStartup - _mouseTouchStartTime;
                float totalDist = Vector2.Distance(pos, _mouseTouchStart);

                if (_mouseTouchPanStarted)
                    _mgr.RaisePanEnded(new PanInfo(pos, Vector2.zero, 1));

                if (_mouseTouchLongFired)
                {
                    _mgr.RaiseLongPressEnded(new LongPressInfo(pos, 0));
                    return;
                }

                if (totalDist >= SwipeMinDistPx && duration > 0.001f)
                {
                    float speed = totalDist / duration;
                    if (speed >= SwipeMinSpeedPx)
                    {
                        var dir = GetSwipeDir(pos - _mouseTouchStart);
                        _mgr.RaiseSwipe(new SwipeInfo(
                            _mouseTouchStart, pos, dir, speed, totalDist, 0));
                        return;
                    }
                }

                if (!_mouseTouchMoved && duration < TapMaxTime)
                {
                    float now = Time.realtimeSinceStartup;
                    bool isDouble = (now - _lastTapTime) < DoubleTapWindow;
                    if (isDouble) _mgr.RaiseDoubleTap(new TapInfo(pos, 0));
                    else          _mgr.RaiseTap(new TapInfo(pos, 0));
                    _lastTapTime = now;
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static SwipeDirection GetSwipeDir(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                return delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
            return delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
        }
    }
}
