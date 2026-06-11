using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModelController
{
    /// <summary>
    /// Demo UI controller for the Model Controller scene.
    /// Wires Reset / Focus buttons, invert toggles, and drives the hints label.
    /// </summary>
    [AddComponentMenu("HelperTools/Model Controller/ModelViewDemoUI")]
    public class ModelViewDemoUI : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private ModelViewController _controller;
        [SerializeField] private Transform           _modelRoot;

        [Header("Buttons")]
        [SerializeField] private Button    _resetButton;
        [SerializeField] private Button    _focusButton;

        [Header("Labels")]
        [SerializeField] private TMP_Text  _statsLabel;
        [SerializeField] private TMP_Text  _hintsLabel;

        [Header("Invert Toggles")]
        [SerializeField] private Toggle _invertOrbitXToggle;
        [SerializeField] private Toggle _invertOrbitYToggle;
        [SerializeField] private Toggle _invertPanXToggle;
        [SerializeField] private Toggle _invertPanYToggle;

        private void Start()
        {
            _resetButton?.onClick.AddListener(OnReset);
            _focusButton?.onClick.AddListener(OnFocus);

            if (_controller != null)
            {
                BindToggle(_invertOrbitXToggle, _controller.InvertOrbitX, v => _controller.InvertOrbitX = v);
                BindToggle(_invertOrbitYToggle, _controller.InvertOrbitY, v => _controller.InvertOrbitY = v);
                BindToggle(_invertPanXToggle,   _controller.InvertPanX,   v => _controller.InvertPanX   = v);
                BindToggle(_invertPanYToggle,   _controller.InvertPanY,   v => _controller.InvertPanY   = v);
            }

            if (_hintsLabel != null)
                _hintsLabel.text = BuildHints();
        }

        private static void BindToggle(Toggle toggle, bool initial, System.Action<bool> onChange)
        {
            if (toggle == null) return;
            toggle.SetIsOnWithoutNotify(initial);
            toggle.onValueChanged.AddListener(v => onChange(v));
        }

        private void Update()
        {
            if (_statsLabel == null || _controller == null) return;
            _statsLabel.text =
                $"Az <color=#88FF88>{_controller.Azimuth:F1}°</color>  " +
                $"El <color=#FFAA44>{_controller.Elevation:F1}°</color>  " +
                $"Dist <color=#AAAAFF>{_controller.Distance:F2}</color>";
        }

        private void OnReset()  => _controller?.ResetView();
        private void OnFocus()  { if (_controller != null && _modelRoot != null) _controller.FocusOn(_modelRoot); }

        private static string BuildHints()
        {
            bool isMobile = Application.isMobilePlatform;
            if (isMobile)
            {
                return "<b>1 Finger</b> — Orbit\n" +
                       "<b>2 Fingers Pinch</b> — Zoom\n" +
                       "<b>2 Fingers Drag</b> — Pan\n" +
                       "<b>Double Tap</b> — Reset";
            }
            return "<b>LMB Drag</b> — Orbit\n" +
                   "<b>RMB / MMB Drag</b> — Pan\n" +
                   "<b>Scroll Wheel</b> — Zoom\n" +
                   "<b>R</b> — Reset view";
        }
    }
}
