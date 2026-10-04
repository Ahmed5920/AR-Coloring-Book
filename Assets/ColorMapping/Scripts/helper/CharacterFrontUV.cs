using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds UVs at runtime by "photographing" the character from one side
/// (front by default) and stretching that flat view over a rectangle of the marker picture.
/// Put this on the ROOT of the character prefab. It only touches renderers that have the
/// "coloring" tag, and it never modifies your original mesh asset (it works on a copy).
/// </summary>
public class CharacterFrontUV : MonoBehaviour
{
    public enum ViewSide
    {
        Front,   // viewer looks at the character's face (character faces +Z in the prefab)
        Back,    // use this if "Front" looks mirrored / the model faces the other way
        Right,
        Left,
        Top,
        Bottom
    }

    [Header("Which renderers get new UVs")]
    [Tooltip("Same tag the color-mapping script looks for.")]
    public string coloringTag = "coloring";

    [Header("Projection")]
    public ViewSide viewFrom = ViewSide.Front;
    public bool flipHorizontal = false;
    public bool flipVertical = false;

    [Header("Where the figure sits on the marker picture")]
    [Tooltip("UV space 0-1, origin = bottom-left of the picture. (0,0,1,1) = whole picture. " +
             "Set it to the box that surrounds the drawn figure.")]
    public Rect figureRect = new Rect(0f, 0f, 1f, 1f);

    [Header("Editor preview only (ignored on the phone)")]
    [Tooltip("Drag the marker image here, press Play, and tune the values above until the drawing lines up with the model.")]
    public Texture previewTexture;

    private class Entry
    {
        public Renderer renderer;
        public Mesh mesh;        // our private copy of the mesh
        public Vector3[] verts;  // original vertices, converted to this object's local space
    }

    private readonly List<Entry> entries = new List<Entry>();
    private bool built;

    void Awake()
    {
        Build();
    }

    void OnDestroy()
    {
        foreach (Entry e in entries)
            if (e.mesh != null) Destroy(e.mesh);
    }

    // Live tuning while the game is running in the editor
    void OnValidate()
    {
        if (Application.isPlaying) Rebuild();
    }

    private void Build()
    {
        entries.Clear();

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!r.CompareTag(coloringTag)) continue;

            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            MeshFilter mf = (smr == null) ? r.GetComponent<MeshFilter>() : null;
            Mesh src = (smr != null) ? smr.sharedMesh : (mf != null ? mf.sharedMesh : null);
            if (src == null) continue;

            if (!src.isReadable)
            {
                Debug.LogError("[CharacterFrontUV] Mesh '" + src.name + "' is not readable. " +
                               "Select the model file in Project > Model tab > enable 'Read/Write' > Apply.");
                continue;
            }

            Mesh copy = Instantiate(src);
            copy.name = src.name + " (auto UV)";
            if (smr != null) smr.sharedMesh = copy; else mf.sharedMesh = copy;

            Vector3[] v = src.vertices;
            Matrix4x4 toRoot = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            for (int i = 0; i < v.Length; i++)
                v[i] = toRoot.MultiplyPoint3x4(v[i]);

            entries.Add(new Entry { renderer = r, mesh = copy, verts = v });
        }

        built = true;
        Rebuild();
    }

    public void Rebuild()
    {
        if (!built || entries.Count == 0) return;

        // 1) size of the character as seen from the chosen side (all parts together)
        Vector2 min = new Vector2(Mathf.Infinity, Mathf.Infinity);
        Vector2 max = new Vector2(Mathf.NegativeInfinity, Mathf.NegativeInfinity);
        foreach (Entry e in entries)
        {
            foreach (Vector3 v in e.verts)
            {
                Vector2 p = Project(v);
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
            }
        }

        Vector2 size = max - min;
        if (size.x < 1e-6f) size.x = 1f;
        if (size.y < 1e-6f) size.y = 1f;

        // 2) turn each vertex into a UV inside figureRect
        foreach (Entry e in entries)
        {
            if (e.mesh == null) continue;

            Vector2[] uv = new Vector2[e.verts.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                Vector2 p = Project(e.verts[i]);
                float nx = (p.x - min.x) / size.x;
                float ny = (p.y - min.y) / size.y;
                if (flipHorizontal) nx = 1f - nx;
                if (flipVertical) ny = 1f - ny;

                uv[i] = new Vector2(figureRect.x + nx * figureRect.width,
                                    figureRect.y + ny * figureRect.height);
            }
            e.mesh.uv = uv;

            if (Application.isEditor && previewTexture != null && e.renderer != null)
                e.renderer.material.mainTexture = previewTexture;
        }
    }

    private Vector2 Project(Vector3 p)
    {
        switch (viewFrom)
        {
            case ViewSide.Front: return new Vector2(-p.x, p.y);
            case ViewSide.Back: return new Vector2(p.x, p.y);
            case ViewSide.Right: return new Vector2(p.z, p.y);
            case ViewSide.Left: return new Vector2(-p.z, p.y);
            case ViewSide.Top: return new Vector2(p.x, p.z);
            default: return new Vector2(-p.x, p.z); // Bottom
        }
    }
}