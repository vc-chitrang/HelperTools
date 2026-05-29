// ============================================================
//  GizmosShowcase.cs
//  A single MonoBehaviour demonstrating EVERY Gizmos API
//  member available in Unity 2022.3:
//
//  Static Properties : color, matrix, exposure, probeSize
//  Static Methods    : DrawLine, DrawLineList, DrawLineStrip,
//                      DrawRay, DrawWireSphere, DrawSphere,
//                      DrawWireCube, DrawCube, DrawFrustum,
//                      DrawMesh, DrawWireMesh, DrawIcon,
//                      DrawGUITexture
//
//  Drop this script onto any GameObject in the Scene.
//  All shapes are visible in the Scene view at all times
//  via OnDrawGizmos; the selected variants only appear when
//  the GameObject is selected.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any GameObject. Tweak the public fields in the
/// Inspector to explore every Gizmos drawing API.
/// </summary>
[ExecuteAlways]
public class GizmosShowcase : MonoBehaviour
{
    // --------------------------------------------------------
    // Inspector-exposed configuration
    // --------------------------------------------------------

    [Header("=== Global Settings ===")]
    [Tooltip("Master color used as the base for most gizmos.")]
    public Color baseColor = Color.green;

    [Tooltip("When true the Gizmos.matrix is set to this " +
             "transform's local-to-world matrix so every " +
             "shape is drawn in local space.")]
    public bool useLocalMatrix = true;

    // --------------------------------------------------------
    // DrawLine / DrawLineList / DrawLineStrip
    // --------------------------------------------------------
    [Header("=== Lines ===")]
    public Color lineColor = Color.cyan;

    [Tooltip("World-space start of the simple line.")]
    public Vector3 lineStart = new Vector3(-2f, 0f, 0f);

    [Tooltip("World-space end of the simple line.")]
    public Vector3 lineEnd = new Vector3(2f, 0f, 0f);

    [Tooltip("Points used for DrawLineStrip (connected chain).")]
    public Vector3[] lineStripPoints = new Vector3[]
    {
        new Vector3(-3f, 1f, 0f),
        new Vector3(-1f, 3f, 0f),
        new Vector3( 1f, 1f, 0f),
        new Vector3( 3f, 3f, 0f),
    };

    [Tooltip("Pairs of points for DrawLineList (isolated segments).")]
    public Vector3[] lineListPoints = new Vector3[]
    {
        new Vector3(-3f, -1f, 0f), new Vector3(-1f, -1f, 0f),
        new Vector3( 1f, -1f, 0f), new Vector3( 3f, -1f, 0f),
    };

    // --------------------------------------------------------
    // DrawRay
    // --------------------------------------------------------
    [Header("=== Ray ===")]
    public Color rayColor = Color.yellow;
    public Vector3 rayOrigin = Vector3.zero;
    public Vector3 rayDirection = new Vector3(0f, 3f, 0f);

    // --------------------------------------------------------
    // DrawWireSphere / DrawSphere
    // --------------------------------------------------------
    [Header("=== Spheres ===")]
    public Color wireSphereColor = Color.white;
    public Color solidSphereColor = new Color(1f, 0.5f, 0f, 0.4f);
    public Vector3 sphereCenter = new Vector3(4f, 0f, 0f);
    [Range(0.1f, 5f)]
    public float sphereRadius = 1f;

    // --------------------------------------------------------
    // DrawWireCube / DrawCube
    // --------------------------------------------------------
    [Header("=== Cubes ===")]
    public Color wireCubeColor = Color.magenta;
    public Color solidCubeColor = new Color(0.5f, 0f, 1f, 0.35f);
    public Vector3 cubeCenter = new Vector3(-4f, 0f, 0f);
    public Vector3 cubeSize = Vector3.one * 2f;

    // --------------------------------------------------------
    // DrawFrustum
    // --------------------------------------------------------
    [Header("=== Frustum ===")]
    public Color frustumColor = Color.red;

    [Tooltip("Field-of-view in degrees for the frustum gizmo.")]
    [Range(10f, 170f)]
    public float frustumFOV = 60f;

    [Range(0.01f, 2f)]
    public float frustumNearClip = 0.1f;

    [Range(1f, 50f)]
    public float frustumFarClip = 10f;

    [Range(0.1f, 4f)]
    public float frustumAspect = 1.78f; // 16:9

    // --------------------------------------------------------
    // DrawMesh / DrawWireMesh  (uses a procedural mesh)
    // --------------------------------------------------------
    [Header("=== Mesh ===")]
    public Color meshColor = new Color(0f, 1f, 0.5f, 0.5f);
    public Color wireMeshColor = Color.white;

    [Tooltip("Optional mesh to draw. If null a procedural " +
             "triangle is generated at runtime.")]
    public Mesh gizmoMesh;

    public Vector3 meshPosition = new Vector3(0f, 3f, 0f);
    public Vector3 meshScale = Vector3.one;

    // --------------------------------------------------------
    // DrawIcon
    // --------------------------------------------------------
    [Header("=== Icon ===")]
    [Tooltip("Name of an icon in Assets/Gizmos/ folder, " +
             "e.g. \"MyIcon.png\".  Leave empty to skip.")]
    public string iconName = "";
    public Vector3 iconPosition = new Vector3(0f, -3f, 0f);
    public bool iconAllowScaling = true;

    // --------------------------------------------------------
    // DrawGUITexture
    // --------------------------------------------------------
    [Header("=== GUI Texture ===")]
    [Tooltip("Texture shown in the Scene via DrawGUITexture. " +
             "Assign any texture asset.")]
    public Texture guiTexture;

    [Tooltip("Screen-space rectangle (x, y, width, height) in " +
             "pixels from the top-left of the Scene view.")]
    public Rect guiTextureRect = new Rect(10f, 10f, 128f, 128f);

    // --------------------------------------------------------
    // Private state
    // --------------------------------------------------------
    private Mesh _proceduralMesh;

    // ============================================================
    //  Unity messages
    // ============================================================

    private void OnValidate()
    {
        // Rebuild the procedural mesh whenever Inspector values change
        _proceduralMesh = null;
    }

    // ------------------------------------------------------------------
    //  OnDrawGizmos  — drawn every Scene repaint, always visible.
    // ------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        // -----------------------------------------------------------
        // 1. GIZMOS.MATRIX
        //    Setting the matrix makes every subsequent draw call
        //    operate in the object's local space.  This is essential
        //    for prefabs that are rotated / scaled in the world.
        // -----------------------------------------------------------
        Matrix4x4 previousMatrix = Gizmos.matrix;

        if (useLocalMatrix)
            Gizmos.matrix = transform.localToWorldMatrix;
        // You can also compose arbitrary matrices:
        //   Gizmos.matrix = Matrix4x4.TRS(pos, rot, scale);

        // -----------------------------------------------------------
        // 2. DrawLine
        //    The most basic primitive — a line between two points.
        // -----------------------------------------------------------
        Gizmos.color = lineColor;
        Gizmos.DrawLine(lineStart, lineEnd);

        // -----------------------------------------------------------
        // 3. DrawLineStrip
        //    Draws a continuous polyline through an array of points.
        //    Ideal for paths, splines, or debug trajectories.
        // -----------------------------------------------------------
        if (lineStripPoints != null && lineStripPoints.Length >= 2)
        {
            Gizmos.color = new Color(lineColor.r, lineColor.g,
                                     lineColor.b, 0.6f);
            Gizmos.DrawLineStrip(
                new System.ReadOnlySpan<Vector3>(lineStripPoints),
                looped: false);

            // Looped version (closes the shape back to first point):
            // Gizmos.DrawLineStrip(lineStripPoints, looped: true);
        }

        // -----------------------------------------------------------
        // 4. DrawLineList
        //    Draws isolated line segments from pairs of points.
        //    Points[0]→Points[1], Points[2]→Points[3], etc.
        // -----------------------------------------------------------
        if (lineListPoints != null && lineListPoints.Length >= 2)
        {
            Gizmos.color = Color.Lerp(lineColor, Color.white, 0.5f);
            Gizmos.DrawLineList(
                new System.ReadOnlySpan<Vector3>(lineListPoints));
        }

        // -----------------------------------------------------------
        // 5. DrawRay
        //    Convenience wrapper for DrawLine(origin, origin+dir).
        //    Perfect for visualising physics raycasts / forces.
        // -----------------------------------------------------------
        Gizmos.color = rayColor;
        Gizmos.DrawRay(rayOrigin, rayDirection);

        // DrawRay also accepts a Ray struct:
        //   Gizmos.DrawRay(new Ray(rayOrigin, rayDirection));

        // -----------------------------------------------------------
        // 6. DrawWireSphere
        //    Hollow sphere outline.  Great for trigger radii,
        //    detection ranges, audio falloff zones.
        // -----------------------------------------------------------
        Gizmos.color = wireSphereColor;
        Gizmos.DrawWireSphere(sphereCenter, sphereRadius);

        // -----------------------------------------------------------
        // 7. DrawSphere  (solid / filled)
        //    Use with a semi-transparent color to show volume
        //    without obscuring underlying geometry.
        // -----------------------------------------------------------
        Gizmos.color = solidSphereColor;
        Gizmos.DrawSphere(sphereCenter, sphereRadius * 0.5f);

        // -----------------------------------------------------------
        // 8. DrawWireCube
        //    Axis-aligned box outline.  Common for collider
        //    bounds, spawn areas, occlusion volumes.
        // -----------------------------------------------------------
        Gizmos.color = wireCubeColor;
        Gizmos.DrawWireCube(cubeCenter, cubeSize);

        // -----------------------------------------------------------
        // 9. DrawCube  (solid / filled)
        //    A filled box — overlay at low alpha for regions.
        // -----------------------------------------------------------
        Gizmos.color = solidCubeColor;
        Gizmos.DrawCube(cubeCenter, cubeSize * 0.8f);

        // -----------------------------------------------------------
        // 10. DrawFrustum
        //     Draws a camera frustum using Gizmos.matrix for its
        //     transform.  Essential for custom camera visualisers.
        //     NOTE: The matrix MUST include the camera position &
        //     rotation.  Here we use the component transform.
        // -----------------------------------------------------------
        Gizmos.color = frustumColor;
        // DrawFrustum uses Gizmos.matrix for position & rotation.
        Gizmos.DrawFrustum(
            Vector3.zero,       // center in local space (origin when matrix is set)
            frustumFOV,
            frustumFarClip,
            frustumNearClip,
            frustumAspect);

        // -----------------------------------------------------------
        // 11. DrawMesh / DrawWireMesh
        //     Draw any mesh at an arbitrary TRS in the Scene.
        //     Useful for previewing procedurally placed assets.
        // -----------------------------------------------------------
        Mesh mesh = GetOrBuildMesh();
        if (mesh != null)
        {
            // Solid mesh
            Gizmos.color = meshColor;
            Gizmos.DrawMesh(
                mesh,
                submeshIndex: 0,
                position: meshPosition,
                rotation: Quaternion.identity,
                scale: meshScale);

            // Wire mesh (overlay)
            Gizmos.color = wireMeshColor;
            Gizmos.DrawWireMesh(
                mesh,
                submeshIndex: 0,
                position: meshPosition,
                rotation: Quaternion.identity,
                scale: meshScale);
        }

        // -----------------------------------------------------------
        // 12. DrawIcon
        //     Places a 2D icon in the Scene view, always facing
        //     the camera.  The texture must live in Assets/Gizmos/.
        // -----------------------------------------------------------
        if (!string.IsNullOrEmpty(iconName))
        {
            // allowScaling = true  → icon shrinks with distance
            // allowScaling = false → fixed pixel size (like a label)
            Gizmos.DrawIcon(iconPosition, iconName, iconAllowScaling);

            // Tinted variant:
            // Gizmos.DrawIcon(iconPosition, iconName,
            //                 iconAllowScaling, Color.red);
        }

        // -----------------------------------------------------------
        // 13. DrawGUITexture
        //     Renders a texture into the Scene view in 2D screen
        //     space.  Useful for overlaying legend / watermark art.
        // -----------------------------------------------------------
        if (guiTexture != null)
        {
            // The Rect is in GUI / screen pixels (not world space).
            Gizmos.DrawGUITexture(guiTextureRect, guiTexture);

            // With a custom border width and tint:
            // Gizmos.DrawGUITexture(guiTextureRect, guiTexture,
            //                       leftBorder: 0, rightBorder: 0,
            //                       topBorder: 0, bottomBorder: 0,
            //                       color: Color.white);
        }

        // -----------------------------------------------------------
        // 14. Restore Gizmos.matrix
        //     Always restore after you're done so other gizmos in
        //     the scene are not affected by your custom matrix.
        // -----------------------------------------------------------
        Gizmos.matrix = previousMatrix;
    }

    // ------------------------------------------------------------------
    //  OnDrawGizmosSelected  — only drawn when this object is selected.
    //  Use for detailed / verbose gizmos that would clutter the view.
    // ------------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        // -----------------------------------------------------------
        // GIZMOS.COLOR  — set per-shape for rich multi-color gizmos
        // -----------------------------------------------------------

        // Axis-aligned bounding box of the transform
        Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(localBounds.center, localBounds.size);

        // Velocity / forward direction ray
        Gizmos.matrix = Matrix4x4.identity; // back to world space
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 5f);

        // Detection radius ring (wire sphere at eye height)
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.6f,
                              sphereRadius);

        // -----------------------------------------------------------
        // Demonstrate building a custom debug grid with DrawLineList
        // -----------------------------------------------------------
        DrawDebugGrid(transform.position, 5, 1f, new Color(0.3f, 0.3f, 1f, 0.5f));

        // Reset matrix
        Gizmos.matrix = Matrix4x4.identity;
    }

    // ============================================================
    //  Helper utilities
    // ============================================================

    /// <summary>
    /// Draws a flat XZ-plane grid centred at <paramref name="origin"/>.
    /// </summary>
    private static void DrawDebugGrid(Vector3 origin,
                                      int halfCount,
                                      float cellSize,
                                      Color color)
    {
        Gizmos.color = color;
        float extent = halfCount * cellSize;

        // Build the line-list pairs directly (no allocation on hot path
        // because this is editor-only code and runs infrequently).
        var lines = new List<Vector3>();

        for (int i = -halfCount; i <= halfCount; i++)
        {
            float offset = i * cellSize;

            // Lines along Z axis
            lines.Add(origin + new Vector3(offset, 0f, -extent));
            lines.Add(origin + new Vector3(offset, 0f,  extent));

            // Lines along X axis
            lines.Add(origin + new Vector3(-extent, 0f, offset));
            lines.Add(origin + new Vector3( extent, 0f, offset));
        }

        Gizmos.DrawLineList(
            new System.ReadOnlySpan<Vector3>(lines.ToArray()));
    }

    /// <summary>
    /// Returns the user-assigned mesh or lazily creates a simple
    /// procedural triangle so the mesh examples always have something
    /// to render without requiring an asset assignment.
    /// </summary>
    private Mesh GetOrBuildMesh()
    {
        if (gizmoMesh != null)
            return gizmoMesh;

        if (_proceduralMesh != null)
            return _proceduralMesh;

        _proceduralMesh = new Mesh { name = "GizmoProceduralTriangle" };
        _proceduralMesh.vertices = new Vector3[]
        {
            new Vector3(-1f, 0f, 0f),
            new Vector3( 0f, 2f, 0f),
            new Vector3( 1f, 0f, 0f),
        };
        _proceduralMesh.triangles = new int[] { 0, 1, 2 };
        _proceduralMesh.RecalculateNormals();
        return _proceduralMesh;
    }

    // ============================================================
    //  USAGE REFERENCE — quick copy-paste snippets
    // ============================================================
    /*
    ─────────────────────────────────────────────────────────────
    PROPERTY          USAGE
    ─────────────────────────────────────────────────────────────
    Gizmos.color      Gizmos.color = Color.red;
    Gizmos.matrix     Gizmos.matrix = transform.localToWorldMatrix;
    Gizmos.exposure   Gizmos.exposure = myRenderTexture;
    Gizmos.probeSize  Gizmos.probeSize = 0.5f;

    ─────────────────────────────────────────────────────────────
    METHOD                  SIGNATURE
    ─────────────────────────────────────────────────────────────
    DrawLine            (Vector3 from, Vector3 to)
    DrawLineStrip       (ReadOnlySpan<Vector3> points, bool looped)
    DrawLineList        (ReadOnlySpan<Vector3> points)
    DrawRay             (Vector3 from, Vector3 direction)
                        (Ray ray)
    DrawWireSphere      (Vector3 center, float radius)
    DrawSphere          (Vector3 center, float radius)
    DrawWireCube        (Vector3 center, Vector3 size)
    DrawCube            (Vector3 center, Vector3 size)
    DrawFrustum         (Vector3 center, float fov,
                         float maxRange, float minRange, float aspect)
    DrawMesh            (Mesh mesh, int submeshIndex,
                         Vector3 position, Quaternion rotation, Vector3 scale)
    DrawWireMesh        (Mesh mesh, int submeshIndex,
                         Vector3 position, Quaternion rotation, Vector3 scale)
    DrawIcon            (Vector3 center, string name, bool allowScaling)
                        (Vector3 center, string name, bool allowScaling, Color color)
    DrawGUITexture      (Rect screenRect, Texture texture)
                        (Rect screenRect, Texture texture,
                         int left, int right, int top, int bottom, Material mat)
    ─────────────────────────────────────────────────────────────

    TIPS
    ─────────────────────────────────────────────────────────────
    • OnDrawGizmos      → always visible, pickable in Scene view
    • OnDrawGizmosSelected → only visible when object is selected
    • Use semi-transparent colors (alpha < 1) for solid shapes
    • Always restore Gizmos.matrix after using a custom one
    • Gizmos are editor-only; stripped from runtime builds
    • Icons live in Assets/Gizmos/ — no subfolder path needed
    • DrawFrustum relies entirely on Gizmos.matrix for placement
    */
}
