using UnityEngine;

namespace ModelController
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  MODEL VIEW CONTROLLER
    ///  Orbit / Zoom / Pan / Inertia / Multi-Touch / Reset
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Attach to a Camera. Supports:
    ///    • Mouse   — Unity Editor, Windows, macOS
    ///    • Touch   — Android (1-finger orbit, 2-finger pinch+pan)
    ///
    ///  Mouse bindings:
    ///    LMB drag       → Orbit
    ///    RMB / MMB drag → Pan
    ///    Scroll wheel   → Zoom
    ///    R key          → Reset view
    ///
    ///  Touch bindings:
    ///    1-finger drag      → Orbit
    ///    2-finger pinch     → Zoom
    ///    2-finger translate → Pan
    ///    Double-tap         → Reset view
    /// </summary>
    [AddComponentMenu("HelperTools/Model Controller/ModelViewController")]
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class ModelViewController : MonoBehaviour
    {
        // ── Initial Pose ──────────────────────────────────────────────────
        [Header("Initial Pose")]
        [SerializeField] private Vector3   _defaultTarget    = Vector3.zero;
        [SerializeField] private float     _defaultAzimuth   = 45f;
        [SerializeField] private float     _defaultElevation = 20f;
        [SerializeField] private float     _defaultDistance  = 5f;
        [SerializeField] private Transform _autoFocusTarget;   // optional — FocusOn() called on Start

        // ── Orbit ─────────────────────────────────────────────────────────
        [Header("Orbit")]
        [SerializeField] private float _orbitSpeedMouse = 0.25f;
        [SerializeField] private float _orbitSpeedTouch = 0.18f;
        [SerializeField] private float _minElevation    = -80f;
        [SerializeField] private float _maxElevation    =  80f;
        [SerializeField] private bool  _invertOrbitX    = true;
        [SerializeField] private bool  _invertOrbitY    = true;

        // ── Zoom ──────────────────────────────────────────────────────────
        [Header("Zoom")]
        [SerializeField] private float _zoomScrollSpeed = 0.12f;   // fraction of distance per scroll tick
        [SerializeField] private float _zoomPinchSpeed  = 0.002f;  // fraction of distance per pixel
        [SerializeField] private float _zoomSmoothing   = 12f;
        [SerializeField] private float _minDistance     =  0.3f;
        [SerializeField] private float _maxDistance     = 30f;

        // ── Pan ───────────────────────────────────────────────────────────
        [Header("Pan")]
        [SerializeField] private float _panSpeedMouse = 1f;
        [SerializeField] private float _panSpeedTouch = 1f;
        [SerializeField] private bool  _invertPanX    = true;
        [SerializeField] private bool  _invertPanY    = true;

        // ── Inertia ───────────────────────────────────────────────────────
        [Header("Inertia")]
        [SerializeField] private bool  _inertiaEnabled = true;
        [SerializeField] private float _inertiaDamping = 6f;

        // ── Reset ─────────────────────────────────────────────────────────
        [Header("Reset")]
        [SerializeField] private float   _resetDuration = 0.45f;
        [SerializeField] private KeyCode _resetKey      = KeyCode.R;

        // ── Runtime state ─────────────────────────────────────────────────
        private float   _azimuth;
        private float   _elevation;
        private float   _distance;
        private float   _targetDistance;
        private Vector3 _targetPoint;

        // Inertia
        private Vector2 _orbitVelocity;
        private bool    _orbitActive;

        // Mouse
        private Vector2 _prevMousePos;

        // Touch
        private bool    _touchInitialized;
        private Vector2 _prevTouchMid;
        private float   _prevTouchDist;

        // Double-tap (touch reset)
        private float       _lastTapTime;
        private const float DoubleTapWindow = 0.35f;

        // Reset animation
        private bool    _resetting;
        private float   _resetT;
        private float   _resetFromAz, _resetFromEl, _resetFromDist;
        private Vector3 _resetFromTarget;

        private Camera _cam;

        // ── Public properties ─────────────────────────────────────────────
        public Vector3 TargetPoint => _targetPoint;
        public float   Azimuth     => _azimuth;
        public float   Elevation   => _elevation;
        public float   Distance    => _distance;

        // Invert toggles — read/write at runtime
        public bool InvertOrbitX { get => _invertOrbitX; set => _invertOrbitX = value; }
        public bool InvertOrbitY { get => _invertOrbitY; set => _invertOrbitY = value; }
        public bool InvertPanX   { get => _invertPanX;   set => _invertPanX   = value; }
        public bool InvertPanY   { get => _invertPanY;   set => _invertPanY   = value; }

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            ResetImmediate();
        }

        private void Start()
        {
            if (_autoFocusTarget != null)
                FocusOn(_autoFocusTarget);
        }

        private void Update()
        {
            if (_resetting) { TickReset(); return; }

            bool hasTouch = Input.touchCount > 0;

            if (hasTouch)
                ProcessTouchInput();
            else
                ProcessMouseInput();

            // Smooth zoom toward target distance
            _distance = Mathf.Lerp(_distance, _targetDistance,
                Time.deltaTime * _zoomSmoothing);

            // Inertia (orbit only)
            if (_inertiaEnabled && !_orbitActive && _orbitVelocity.sqrMagnitude > 0.00001f)
            {
                ApplyOrbitDelta(_orbitVelocity);
                _orbitVelocity = Vector2.Lerp(_orbitVelocity, Vector2.zero,
                    Time.deltaTime * _inertiaDamping);
                if (_orbitVelocity.sqrMagnitude < 0.00001f)
                    _orbitVelocity = Vector2.zero;
            }
            else if (!_orbitActive)
            {
                _orbitVelocity = Vector2.zero;
            }

            if (Input.GetKeyDown(_resetKey)) ResetView();

            ApplyPose();
        }

        // ── Mouse input ───────────────────────────────────────────────────

        private void ProcessMouseInput()
        {
            bool panNow = Input.GetMouseButton(1) || Input.GetMouseButton(2);

            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                _prevMousePos = Input.mousePosition;

            // Orbit — left button (only when not panning)
            if (Input.GetMouseButton(0) && !panNow)
            {
                var delta = (Vector2)Input.mousePosition - _prevMousePos;
                if (delta.sqrMagnitude > 0.01f)
                {
                    var od = new Vector2(
                        -delta.x * _orbitSpeedMouse,
                         delta.y * _orbitSpeedMouse);
                    ApplyOrbitDelta(od);
                    _orbitVelocity = od;
                    _orbitActive   = true;
                }
            }
            else
            {
                _orbitActive = false;
            }

            // Pan — right or middle button
            if (panNow)
            {
                var delta = (Vector2)Input.mousePosition - _prevMousePos;
                if (delta.sqrMagnitude > 0.01f)
                {
                    Pan(delta * _panSpeedMouse);
                    _orbitVelocity = Vector2.zero;
                    _orbitActive   = false;
                }
            }

            _prevMousePos = Input.mousePosition;

            // Zoom — scroll wheel (fractional keeps feel consistent at any distance)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                _targetDistance *= 1f - scroll * _zoomScrollSpeed * 10f;
                _targetDistance  = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
            }
        }

        // ── Touch input ───────────────────────────────────────────────────

        private void ProcessTouchInput()
        {
            int count = Input.touchCount;

            if (count == 1)
            {
                Touch t = Input.GetTouch(0);

                if (t.phase == TouchPhase.Began)
                {
                    float now = Time.realtimeSinceStartup;
                    if (now - _lastTapTime < DoubleTapWindow) ResetView();
                    _lastTapTime = now;

                    _prevTouchMid     = t.position;
                    _touchInitialized = false;
                    _orbitActive      = false;
                    return;
                }

                if (t.phase == TouchPhase.Moved)
                {
                    Vector2 delta = t.position - _prevTouchMid;
                    float   ppi   = Screen.dpi > 0 ? Screen.dpi : 160f;
                    float   scale = _orbitSpeedTouch * (ppi / 160f);
                    var od = new Vector2(-delta.x * scale, delta.y * scale);
                    ApplyOrbitDelta(od);
                    _orbitVelocity = od;
                    _orbitActive   = true;
                    _prevTouchMid  = t.position;
                }

                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    _orbitActive = false;
            }
            else if (count >= 2)
            {
                _orbitActive   = false;
                _orbitVelocity = Vector2.zero;

                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);

                Vector2 mid  = (t0.position + t1.position) * 0.5f;
                float   dist = Vector2.Distance(t0.position, t1.position);

                if (!_touchInitialized
                    || t0.phase == TouchPhase.Began
                    || t1.phase == TouchPhase.Began)
                {
                    _prevTouchMid     = mid;
                    _prevTouchDist    = dist;
                    _touchInitialized = true;
                    return;
                }

                // Pinch → zoom
                float pinchDelta = dist - _prevTouchDist;
                if (Mathf.Abs(pinchDelta) > 0.5f)
                {
                    _targetDistance *= 1f - pinchDelta * _zoomPinchSpeed;
                    _targetDistance  = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
                }

                // Midpoint translate → pan
                Vector2 midDelta = mid - _prevTouchMid;
                if (midDelta.sqrMagnitude > 0.01f)
                    Pan(midDelta * _panSpeedTouch);

                _prevTouchMid  = mid;
                _prevTouchDist = dist;
            }
            else
            {
                _orbitActive = false;
            }
        }

        // ── Core operations ───────────────────────────────────────────────

        private void ApplyOrbitDelta(Vector2 delta)
        {
            float dAz = _invertOrbitX ? -delta.x : delta.x;
            float dEl = _invertOrbitY ? -delta.y : delta.y;
            // Turntable: camera orbits around world Y. Horizon always stays level.
            _azimuth   += dAz;
            _elevation  = Mathf.Clamp(_elevation + dEl, _minElevation, _maxElevation);
        }

        private void Pan(Vector2 screenDeltaPixels)
        {
            // Scale by frustum height so pan speed matches perceived model size.
            float halfFovRad    = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float frustumHeight = 2f * _distance * Mathf.Tan(halfFovRad);
            float worldPerPx    = frustumHeight / Screen.height;

            float px = _invertPanX ? -screenDeltaPixels.x : screenDeltaPixels.x;
            float py = _invertPanY ? -screenDeltaPixels.y : screenDeltaPixels.y;
            _targetPoint += (transform.right * px + transform.up * py) * worldPerPx;
        }

        // ── Pose ──────────────────────────────────────────────────────────

        private void ApplyPose()
        {
            Vector3 offset = SphericalToCartesian(_azimuth, _elevation, _distance);
            transform.position = _targetPoint + offset;

            // Avoid gimbal lock when directly above/below
            Vector3 dir = (_targetPoint - transform.position).normalized;
            Vector3 up  = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f
                ? Vector3.forward : Vector3.up;
            transform.LookAt(_targetPoint, up);
        }

        private static Vector3 SphericalToCartesian(float azDeg, float elDeg, float r)
        {
            float az    = azDeg * Mathf.Deg2Rad;
            float el    = elDeg * Mathf.Deg2Rad;
            float cosEl = Mathf.Cos(el);
            return new Vector3(
                Mathf.Sin(az) * cosEl,
                Mathf.Sin(el),
                Mathf.Cos(az) * cosEl) * r;
        }

        // ── Focus / Frame ─────────────────────────────────────────────────

        /// <summary>Frames the camera so all renderers under the given Transform are visible.</summary>
        public void FocusOn(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            FocusOnBounds(b);
        }

        /// <summary>Frames the camera on the given world-space bounds.</summary>
        public void FocusOnBounds(Bounds b)
        {
            _targetPoint    = b.center;
            float halfFov   = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            _targetDistance = Mathf.Clamp(
                (b.extents.magnitude / Mathf.Tan(halfFov)) * 1.4f,
                _minDistance, _maxDistance);
            _distance       = _targetDistance;
        }

        // ── Reset ─────────────────────────────────────────────────────────

        /// <summary>Smoothly animates the camera back to its default pose.</summary>
        public void ResetView()
        {
            if (_resetting) return;
            _resetFromAz     = _azimuth;
            _resetFromEl     = _elevation;
            _resetFromDist   = _distance;
            _resetFromTarget = _targetPoint;
            _resetT          = 0f;
            _resetting       = true;
            _orbitVelocity   = Vector2.zero;
        }

        private void ResetImmediate()
        {
            _azimuth        = _defaultAzimuth;
            _elevation      = _defaultElevation;
            _distance       = _defaultDistance;
            _targetDistance = _defaultDistance;
            _targetPoint    = _defaultTarget;
        }

        private void TickReset()
        {
            _resetT += Time.deltaTime / Mathf.Max(0.01f, _resetDuration);
            float t  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_resetT));

            _azimuth        = Mathf.LerpAngle(_resetFromAz,   _defaultAzimuth,   t);
            _elevation      = Mathf.LerpAngle(_resetFromEl,   _defaultElevation, t);
            _distance       = Mathf.Lerp    (_resetFromDist,  _defaultDistance,  t);
            _targetDistance = _distance;
            _targetPoint    = Vector3.Lerp  (_resetFromTarget, _defaultTarget,   t);

            if (_resetT >= 1f) _resetting = false;
            ApplyPose();
        }
    }
}
