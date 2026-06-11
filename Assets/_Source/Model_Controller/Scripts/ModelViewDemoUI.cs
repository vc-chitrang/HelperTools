using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModelController
{
    /// <summary>
    /// Demo UI controller for the Model Controller scene.
    /// Wires Reset / Focus buttons and drives the hints label.
    /// </summary>
    [AddComponentMenu("HelperTools/Model Controller/ModelViewDemoUI")]
    public class ModelViewDemoUI : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private ModelViewController _controller;
        [SerializeField] private Transform           _modelRoot;

        [Header("UI")]
        [SerializeField] private Button    _resetButton;
        [SerializeField] private Button    _focusButton;
        [SerializeField] private TMP_Text  _statsLabel;
        [SerializeField] private TMP_Text  _hintsLabel;

        private void Start()
        {
            _resetButton?.onClick.AddListener(OnReset);
            _focusButton?.onClick.AddListener(OnFocus);

            if (_hintsLabel != null)
                _hintsLabel.text = BuildHints();
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
