using UnityEngine;
using UnityEngine.UI;

namespace ImageSystem
{
    /// <summary>
    /// ══════════════════════════════════════════════════════════════
    ///  ADAPTIVE IMAGE — uGUI image with CSS-like fill modes
    /// ══════════════════════════════════════════════════════════════
    ///
    ///  Put this on an empty RectTransform (the FRAME that defines the
    ///  visible bounds). It manages a child "Content" graphic and sizes
    ///  it according to the chosen <see cref="ImageFillMode"/>:
    ///
    ///    Fill           → cover the frame, overflow visible
    ///    Fit            → letterbox inside the frame
    ///    Stretch        → distort to match the frame exactly
    ///    Crop           → cover the frame, overflow masked away (CSS "cover")
    ///    PreserveAspect → native pixel size, centered, no scaling
    ///
    ///  Usage from code:
    ///    var img = GetComponent&lt;AdaptiveImage&gt;();
    ///    img.SetSprite(mySprite);                  // or img.SetTexture(myTexture)
    ///    img.FillMode = ImageFillMode.Crop;
    ///
    ///  Re-applies automatically when the frame is resized (layout groups,
    ///  screen rotation) — sizing math lives in <see cref="FillModeMath"/>.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("HelperTools/Image/AdaptiveImage")]
    public class AdaptiveImage : MonoBehaviour
    {
        private const string ContentName = "Content";

        [Tooltip("How the image content is sized relative to this RectTransform (the frame):\n" +
                 "• Fill — scale to cover the frame, overflow stays visible\n" +
                 "• Fit — scale to fit entirely inside the frame (letterbox)\n" +
                 "• Stretch — distort to match the frame exactly\n" +
                 "• Crop — scale to cover, overflow cut off by a RectMask2D\n" +
                 "• PreserveAspect — native pixel size, centered, no scaling")]
        [SerializeField] private ImageFillMode _fillMode = ImageFillMode.Fit;

        [Tooltip("Sprite content. Leave empty if assigning a raw Texture instead.\n" +
                 "Set from code with SetSprite(sprite).")]
        [SerializeField] private Sprite _sprite;

        [Tooltip("Raw texture content (e.g. a downloaded Texture2D or a RenderTexture).\n" +
                 "Used only when Sprite is empty. Set from code with SetTexture(texture).")]
        [SerializeField] private Texture _texture;

        private RectTransform _content;
        private Image _image;          // used when content is a Sprite
        private RawImage _rawImage;    // used when content is a Texture
        private RectMask2D _mask;      // enabled only in Crop mode

        // ──────────────────────────────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Current fill mode; setting it re-applies the layout immediately.</summary>
        public ImageFillMode FillMode
        {
            get => _fillMode;
            set { _fillMode = value; Apply(); }
        }

        /// <summary>Displays a sprite (clears any raw texture) and re-applies the layout.</summary>
        public void SetSprite(Sprite sprite)
        {
            _sprite = sprite;
            _texture = null;
            Apply();
        }

        /// <summary>Displays a raw texture (clears any sprite) and re-applies the layout.</summary>
        public void SetTexture(Texture texture)
        {
            _texture = texture;
            _sprite = null;
            Apply();
        }

        /// <summary>
        /// Recomputes the content size for the current frame size, fill mode, and
        /// content. Called automatically on enable, resize, and content change.
        /// </summary>
        public void Apply()
        {
            if (this == null || !gameObject.scene.IsValid())
                return;

            EnsureContent();

            // Route content to the right graphic; hide the other one.
            bool useSprite = _sprite != null;
            _image.enabled = useSprite;
            _rawImage.enabled = !useSprite && _texture != null;
            _image.sprite = useSprite ? _sprite : null;
            _rawImage.texture = useSprite ? null : _texture;

            // Crop is the only mode that needs masking.
            _mask.enabled = _fillMode == ImageFillMode.Crop;

            Vector2 contentNativeSize = GetNativeSize();
            Vector2 frameSize = ((RectTransform)transform).rect.size;
            _content.sizeDelta = FillModeMath.GetContentSize(frameSize, contentNativeSize, _fillMode);
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange()
        {
            // Fired whenever the frame is resized (layout pass, rotation, etc.).
            if (isActiveAndEnabled && _content != null)
                Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Hierarchy mutation is not allowed inside OnValidate — defer one tick.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                    Apply();
            };
        }
#endif

        // ──────────────────────────────────────────────────────────────────────
        //  Internals
        // ──────────────────────────────────────────────────────────────────────

        private Vector2 GetNativeSize()
        {
            if (_sprite != null)
                return _sprite.rect.size;
            if (_texture != null)
                return new Vector2(_texture.width, _texture.height);
            return Vector2.zero;
        }

        /// <summary>Finds or creates the child content graphic and the crop mask.</summary>
        private void EnsureContent()
        {
            if (_content == null)
            {
                Transform existing = transform.Find(ContentName);
                if (existing != null)
                {
                    _content = (RectTransform)existing;
                }
                else
                {
                    var go = new GameObject(ContentName, typeof(RectTransform));
                    _content = (RectTransform)go.transform;
                    _content.SetParent(transform, false);
                }

                // Centered child; size is driven purely by sizeDelta.
                _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
                _content.pivot = new Vector2(0.5f, 0.5f);
                _content.anchoredPosition = Vector2.zero;
            }

            if (_image == null)
            {
                _image = _content.GetComponent<Image>();
                if (_image == null) _image = _content.gameObject.AddComponent<Image>();
                _image.raycastTarget = false;
            }

            if (_rawImage == null)
            {
                _rawImage = _content.GetComponent<RawImage>();
                if (_rawImage == null) _rawImage = _content.gameObject.AddComponent<RawImage>();
                _rawImage.raycastTarget = false;
            }

            if (_mask == null)
            {
                _mask = GetComponent<RectMask2D>();
                if (_mask == null) _mask = gameObject.AddComponent<RectMask2D>();
            }
        }
    }
}
