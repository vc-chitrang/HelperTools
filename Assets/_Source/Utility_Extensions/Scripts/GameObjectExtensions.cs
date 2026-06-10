using UnityEngine;

namespace HelperTools
{
    /// <summary>GameObject extension methods.</summary>
    public static class GameObjectExtensions
    {
        // ── Component helpers ──────────────────────────────────────────────

        /// <summary>
        /// Gets the component of type <typeparamref name="T"/>, adding it if absent.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            if (!go.TryGetComponent<T>(out var c))
                c = go.AddComponent<T>();
            return c;
        }

        /// <summary>
        /// Returns <c>true</c> and outputs the component if it exists; avoids a second lookup.
        /// </summary>
        public static bool TryGetComponentInParent<T>(this GameObject go, out T component) where T : Component
        {
            component = go.GetComponentInParent<T>();
            return component != null;
        }

        // ── Layer helpers ──────────────────────────────────────────────────

        /// <summary>Sets the layer on this GameObject and all its children.</summary>
        public static void SetLayerRecursive(this GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                child.gameObject.SetLayerRecursive(layer);
        }

        /// <summary>Sets the layer by name on this GameObject and all its children.</summary>
        public static void SetLayerRecursive(this GameObject go, string layerName) =>
            go.SetLayerRecursive(LayerMask.NameToLayer(layerName));

        // ── Visibility ─────────────────────────────────────────────────────

        /// <summary>Shows or hides every Renderer on this GameObject and its children.</summary>
        public static void SetVisible(this GameObject go, bool visible)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(includeInactive: true))
                r.enabled = visible;
        }

        // ── Scene / active checks ──────────────────────────────────────────

        /// <summary>Returns <c>true</c> if the object exists and is in a valid scene (not a prefab asset).</summary>
        public static bool IsInScene(this GameObject go) =>
            go != null && go.scene.IsValid();

        // ── Hierarchy ─────────────────────────────────────────────────────

        /// <summary>Returns <c>true</c> if <paramref name="candidate"/> is an ancestor of this GameObject.</summary>
        public static bool IsChildOf(this GameObject go, GameObject candidate)
        {
            var t = go.transform.parent;
            while (t != null)
            {
                if (t.gameObject == candidate) return true;
                t = t.parent;
            }
            return false;
        }

        // ── Destruction ────────────────────────────────────────────────────

        /// <summary>
        /// Destroys in play mode (<c>Object.Destroy</c>) or edit mode (<c>Object.DestroyImmediate</c>).
        /// </summary>
        public static void SafeDestroy(this GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else                       Object.DestroyImmediate(go);
        }
    }
}
