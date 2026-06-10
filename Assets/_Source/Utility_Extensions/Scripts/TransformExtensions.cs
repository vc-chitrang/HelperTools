using UnityEngine;

namespace HelperTools
{
    /// <summary>Transform and RectTransform extension methods.</summary>
    public static class TransformExtensions
    {
        // ── Reset ──────────────────────────────────────────────────────────

        /// <summary>Resets local position, rotation, and scale to identity.</summary>
        public static void ResetLocal(this Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;
        }

        // ── Position setters ───────────────────────────────────────────────

        public static void SetX(this Transform t, float x) =>
            t.position = new Vector3(x, t.position.y, t.position.z);

        public static void SetY(this Transform t, float y) =>
            t.position = new Vector3(t.position.x, y, t.position.z);

        public static void SetZ(this Transform t, float z) =>
            t.position = new Vector3(t.position.x, t.position.y, z);

        public static void SetLocalX(this Transform t, float x) =>
            t.localPosition = new Vector3(x, t.localPosition.y, t.localPosition.z);

        public static void SetLocalY(this Transform t, float y) =>
            t.localPosition = new Vector3(t.localPosition.x, y, t.localPosition.z);

        public static void SetLocalZ(this Transform t, float z) =>
            t.localPosition = new Vector3(t.localPosition.x, t.localPosition.y, z);

        // ── Hierarchy helpers ──────────────────────────────────────────────

        /// <summary>Sets parent and resets the local transform to identity.</summary>
        public static void SetParentAndReset(this Transform t, Transform parent)
        {
            t.SetParent(parent, worldPositionStays: false);
            t.ResetLocal();
        }

        /// <summary>Destroys all direct children (play mode: Destroy; edit mode: DestroyImmediate).</summary>
        public static void DestroyChildren(this Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(child);
                else                       Object.DestroyImmediate(child);
            }
        }

        // ── Component helpers ──────────────────────────────────────────────

        /// <summary>
        /// Gets the component of type <typeparamref name="T"/> on the GameObject,
        /// adding it first if it doesn't exist.
        /// </summary>
        public static T GetOrAddComponent<T>(this Transform t) where T : Component =>
            t.gameObject.GetOrAddComponent<T>();

        // ── Look helpers ───────────────────────────────────────────────────

        /// <summary>Rotates the transform to face <paramref name="target"/> in the XZ plane (Y = 0).</summary>
        public static void LookAtFlat(this Transform t, Vector3 target)
        {
            target.y = t.position.y;
            if (target != t.position)
                t.LookAt(target);
        }
    }
}
