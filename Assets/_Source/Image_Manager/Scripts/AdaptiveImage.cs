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
    ///  visible bounds). It manages two child graphics and sizes them
    ///  according to the chosen <see cref="ImageFillMode"/>:
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
    ///  Two separate children are used — ContentSprite (Image) and
    ///  ContentTexture (RawImage) — because Unity 6 Graphic base class
    ///  carries [DisallowMultipleComponent], so both cannot live on the
    ///  same GameObject. Only the active child is visible at any time.
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
        private const string SpriteChildName  = "ContentSprite";
        private const string TextureChildName = "ContentTexture";

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

        // Two separate children — Image and RawImage can't share a GameObject in Unity 6
        // because Graphic carries [DisallowMultipleComponent].
        private RectTransform _spriteRT;
        private RectTransform _textureRT;
        private Image      _image;
        private RawImage   _rawImage;
        private RectMask2D _mask;

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
            _sprite  = sprite;
            _texture = null;
            Apply();
        }

        /// <summary>Displays a raw texture (clears any sprite) and re-applies the layout.</summary>
        public void SetTexture(Texture texture)
        {
            _texture = texture;
            _sprite  = null;
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

            bool useSprite  = _sprite != null;
            bool useTexture = !useSprite && _texture != null;

            _spriteRT.gameObject.SetActive(useSprite);
            _textureRT.gameObject.SetActive(useTexture);

            if (useSprite)  _image.sprite    = _sprite;
            if (useTexture) _rawImage.texture = _texture;

            // Crop is the only mode that needs masking.
            _mask.enabled = _fillMode == ImageFillMode.Crop;

            Vector2 contentSize = FillModeMath.GetContentSize(
                ((RectTransform)transform).rect.size, GetNativeSize(), _fillMode);

            _spriteRT.sizeDelta  = contentSize;
            _textureRT.sizeDelta = contentSize;
        }

        // ──────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ──────────────────────────────────────────────────────────────────────

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && _spriteRT != null)
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
            if (_sprite  != null) return _sprite.rect.size;
            if (_texture != null) return new Vector2(_texture.width, _texture.height);
            return Vector2.zero;
        }

        /// <summary>
        /// Finds or creates the two content children and the crop mask.
        /// Separated into ContentSprite (Image) and ContentTexture (RawImage)
        /// because Graphic has [DisallowMultipleComponent] in Unity 6.
        /// </summary>
        private void EnsureContent()
        {
            _spriteRT  = EnsureChild(SpriteChildName,  ref _spriteRT);
            _textureRT = EnsureChild(TextureChildName, ref _textureRT);

            if (_image == null)
            {
                _image = _spriteRT.GetComponent<Image>();
                if (_image == null) _image = _spriteRT.gameObject.AddComponent<Image>();
                _image.raycastTarget = false;
            }

            if (_rawImage == null)
            {
                _rawImage = _textureRT.GetComponent<RawImage>();
                if (_rawImage == null) _rawImage = _textureRT.gameObject.AddComponent<RawImage>();
                _rawImage.raycastTarget = false;
            }

            if (_mask == null)
            {
                _mask = GetComponent<RectMask2D>();
                if (_mask == null) _mask = gameObject.AddComponent<RectMask2D>();
            }
        }

        private RectTransform EnsureChild(string childName, ref RectTransform cached)
        {
            if (cached != null) return cached;

            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                cached = (RectTransform)existing;
            }
            else
            {
                var go = new GameObject(childName, typeof(RectTransform));
                cached = (RectTransform)go.transform;
                cached.SetParent(transform, false);
            }

            // Centered child; size driven purely by sizeDelta.
            cached.anchorMin        = cached.anchorMax = new Vector2(0.5f, 0.5f);
            cached.pivot            = new Vector2(0.5f, 0.5f);
            cached.anchoredPosition = Vector2.zero;
            return cached;
        }
    }
}
