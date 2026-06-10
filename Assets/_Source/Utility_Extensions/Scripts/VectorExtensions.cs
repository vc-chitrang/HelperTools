using UnityEngine;

namespace HelperTools
{
    /// <summary>Vector2 and Vector3 extension methods.</summary>
    public static class VectorExtensions
    {
        // ── Component setters ──────────────────────────────────────────────

        public static Vector3 WithX(this Vector3 v, float x) => new Vector3(x, v.y, v.z);
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
        public static Vector3 WithZ(this Vector3 v, float z) => new Vector3(v.x, v.y, z);

        public static Vector2 WithX(this Vector2 v, float x) => new Vector2(x, v.y);
        public static Vector2 WithY(this Vector2 v, float y) => new Vector2(v.x, y);

        // ── Conversion ─────────────────────────────────────────────────────

        /// <summary>Drops the Z component.</summary>
        public static Vector2 ToVector2(this Vector3 v) => new Vector2(v.x, v.y);

        /// <summary>Promotes to Vector3 with the given Z value (default 0).</summary>
        public static Vector3 ToVector3(this Vector2 v, float z = 0f) => new Vector3(v.x, v.y, z);

        /// <summary>Flat XZ plane: returns <c>(x, 0, y)</c> from a Vector2.</summary>
        public static Vector3 ToXZVector3(this Vector2 v) => new Vector3(v.x, 0f, v.y);

        // ── Math helpers ───────────────────────────────────────────────────

        /// <summary>Component-wise absolute value.</summary>
        public static Vector3 Abs(this Vector3 v) =>
            new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>Component-wise absolute value.</summary>
        public static Vector2 Abs(this Vector2 v) =>
            new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));

        /// <summary>Clamps each component independently.</summary>
        public static Vector3 Clamp(this Vector3 v, float min, float max) =>
            new Vector3(Mathf.Clamp(v.x, min, max),
                        Mathf.Clamp(v.y, min, max),
                        Mathf.Clamp(v.z, min, max));

        /// <summary>Clamps each component independently.</summary>
        public static Vector2 Clamp(this Vector2 v, float min, float max) =>
            new Vector2(Mathf.Clamp(v.x, min, max),
                        Mathf.Clamp(v.y, min, max));

        /// <summary>Returns a new vector with each component rounded to the nearest integer.</summary>
        public static Vector3 Round(this Vector3 v) =>
            new Vector3(Mathf.Round(v.x), Mathf.Round(v.y), Mathf.Round(v.z));

        /// <summary>True if the squared distance is within <paramref name="sqrDistance"/>.</summary>
        public static bool IsWithinSqr(this Vector3 v, Vector3 other, float sqrDistance) =>
            (v - other).sqrMagnitude <= sqrDistance;

        // ── Direction helpers ──────────────────────────────────────────────

        /// <summary>Returns a random direction on the XZ plane as a normalized Vector3.</summary>
        public static Vector3 RandomXZ() =>
            new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
    }
}
