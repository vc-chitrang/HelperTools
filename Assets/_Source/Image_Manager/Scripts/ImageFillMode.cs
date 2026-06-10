using UnityEngine;

namespace ImageSystem
{
    /// <summary>
    /// How an image's content is sized relative to its container.
    /// Used by <see cref="AdaptiveImage"/> (UI) and <see cref="ImageResizeUtility"/> (textures).
    /// </summary>
    public enum ImageFillMode
    {
        /// <summary>Scale (keeping aspect) until the container is fully covered. Overflow stays visible.</summary>
        Fill,

        /// <summary>Scale (keeping aspect) until the whole image fits inside the container (letterbox).</summary>
        Fit,

        /// <summary>Distort the image to match the container exactly. Aspect ratio is NOT preserved.</summary>
        Stretch,

        /// <summary>Same as <see cref="Fill"/> but the overflow is cropped by a mask (CSS "cover").</summary>
        Crop,

        /// <summary>Keep the image at its native pixel size, centered. No scaling at all.</summary>
        PreserveAspect
    }

    /// <summary>
    /// Pure, stateless math for fill-mode calculations — no Unity objects are touched,
    /// so the same routine drives both the runtime UI component and texture resizing
    /// (DRY: one source of truth for "how big should the content be").
    /// </summary>
    public static class FillModeMath
    {
        /// <summary>
        /// Returns the size the content should be rendered at so that it relates to
        /// <paramref name="container"/> according to <paramref name="mode"/>.
        /// </summary>
        /// <param name="container">Available area (e.g. RectTransform size or target resolution).</param>
        /// <param name="content">Native size of the image (pixels).</param>
        /// <param name="mode">Desired fill behaviour.</param>
        public static Vector2 GetContentSize(Vector2 container, Vector2 content, ImageFillMode mode)
        {
            // Degenerate inputs: nothing sensible to compute, just match the container.
            if (content.x <= 0f || content.y <= 0f || container.x <= 0f || container.y <= 0f)
                return container;

            switch (mode)
            {
                case ImageFillMode.Stretch:
                    return container;

                case ImageFillMode.PreserveAspect:
                    return content;

                case ImageFillMode.Fit:
                {
                    float scale = Mathf.Min(container.x / content.x, container.y / content.y);
                    return content * scale;
                }

                case ImageFillMode.Fill:
                case ImageFillMode.Crop:
                {
                    float scale = Mathf.Max(container.x / content.x, container.y / content.y);
                    return content * scale;
                }

                default:
                    return container;
            }
        }

        /// <summary>
        /// Returns the target pixel dimensions for resizing a <paramref name="content"/>-sized
        /// image into <paramref name="container"/> using <paramref name="mode"/>, rounded and
        /// clamped to at least 1×1.
        /// </summary>
        public static Vector2Int GetPixelSize(Vector2 container, Vector2 content, ImageFillMode mode)
        {
            Vector2 size = GetContentSize(container, content, mode);
            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(size.x)),
                                  Mathf.Max(1, Mathf.RoundToInt(size.y)));
        }
    }
}
