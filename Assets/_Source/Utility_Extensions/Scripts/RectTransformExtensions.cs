using UnityEngine;

namespace HelperTools
{
    /// <summary>RectTransform extension methods for UI layout.</summary>
    public static class RectTransformExtensions
    {
        // ── Size ───────────────────────────────────────────────────────────

        public static void SetWidth(this RectTransform rt, float width) =>
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);

        public static void SetHeight(this RectTransform rt, float height) =>
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

        public static void SetSize(this RectTransform rt, float width, float height) =>
            rt.sizeDelta = new Vector2(width, height);

        public static void SetSize(this RectTransform rt, Vector2 size) =>
            rt.sizeDelta = size;

        // ── Anchored position ──────────────────────────────────────────────

        public static void SetAnchoredX(this RectTransform rt, float x) =>
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);

        public static void SetAnchoredY(this RectTransform rt, float y) =>
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);

        // ── Stretch presets ────────────────────────────────────────────────

        /// <summary>Stretches to fill parent (anchors all at 0/1, offsets zeroed).</summary>
        public static void StretchToParent(this RectTransform rt)
        {
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = Vector2.zero;
            rt.offsetMax  = Vector2.zero;
        }

        /// <summary>Centers with a fixed size; anchors at (0.5, 0.5).</summary>
        public static void CenterWithSize(this RectTransform rt, Vector2 size)
        {
            rt.anchorMin    = new Vector2(0.5f, 0.5f);
            rt.anchorMax    = new Vector2(0.5f, 0.5f);
            rt.sizeDelta    = size;
            rt.anchoredPosition = Vector2.zero;
        }

        // ── World / screen conversion ──────────────────────────────────────

        /// <summary>
        /// Returns the world-space corners (0=BL, 1=TL, 2=TR, 3=BR) as an array.
        /// </summary>
        public static Vector3[] GetWorldCorners(this RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return corners;
        }

        /// <summary>Returns the center of the RectTransform in world space.</summary>
        public static Vector3 GetWorldCenter(this RectTransform rt)
        {
            var c = rt.GetWorldCorners();
            return (c[0] + c[2]) * 0.5f;
        }

        /// <summary>
        /// Converts a screen-space point to local RectTransform space.
        /// Returns <c>true</c> on success.
        /// </summary>
        public static bool ScreenToLocalPoint(this RectTransform rt,
            Vector2 screenPoint, Camera camera, out Vector2 localPoint) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, camera, out localPoint);

        // ── Containment ────────────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if the screen-space point lies inside the RectTransform.</summary>
        public static bool ContainsScreenPoint(this RectTransform rt, Vector2 screenPoint, Camera camera) =>
            RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, camera);
    }
}
