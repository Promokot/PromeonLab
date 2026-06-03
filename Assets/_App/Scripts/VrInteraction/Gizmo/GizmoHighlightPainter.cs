using System.Collections.Generic;
using UnityEngine;

// Owns the gizmo's per-handle highlight state: the instanced native per-axis materials plus a
// SilhouetteOnly outline per renderer, and the hover-darken / grab-recolor / restore transitions.
// Extracted from GizmoDriver (A2) so the activator stays focused on spawn/visibility/drag
// orchestration. Pure helper (no MonoBehaviour) — the activator builds one per spawned instance.
public class GizmoHighlightPainter
{
    // One per gizmo mesh renderer. The gizmo ships its own per-axis materials (the emissive
    // Gizmo_Emissive* set) — including the scale center, whose single mesh carries 4 submeshes
    // (body + 3 axis legs). We keep those native materials (instanced so tinting never touches the
    // shared assets) and capture BOTH the base AND emission color of every submesh so hover/grab can
    // recolor and exactly restore them. The visible color of an emissive material is its emission, so
    // touching only _BaseColor (the old bug) left the highlight broken — we now handle both.
    private class GizmoPart
    {
        public Material[]   Materials;    // instanced native materials (one per submesh)
        public bool[]       HasBase;      // submesh has a _BaseColor/_Color slot
        public Color[]      BaseColor;    // captured base color per submesh
        public bool[]       HasEmis;      // submesh has an _EmissionColor slot
        public Color[]      EmisColor;    // captured emission color per submesh
        public Outline      Outline;
        public Color        OutlineBase;
        public GizmoHandle  Handle;       // null for centers (move-center has no handle)
    }

    private static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId         = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const float HOVER_DARKEN = 0.75f;

    private readonly GizmoConfig   _config;
    private readonly OutlineConfig _outlineConfig;

    // The grabbed-handle look, read once from GizmoConfig.ActiveMaterial (Gizmo_EmissiveSelected) at spawn.
    private bool  _hasActiveBase, _hasActiveEmis;
    private Color _activeBase, _activeEmis;

    private readonly List<GizmoPart>                          _parts         = new();
    private readonly Dictionary<GizmoHandle, List<GizmoPart>> _partsByHandle = new();

    public GizmoHighlightPainter(OutlineConfig outlineConfig, GizmoConfig config)
    {
        _outlineConfig = outlineConfig;
        _config        = config;
    }

    // Outline tint applied to the grabbed handle; the drag session reads this on grab.
    public Color GrabOutlineColor => _outlineConfig != null ? _outlineConfig.GizmoActiveColor : Color.cyan;

    // Capture the active-look colors then build one GizmoPart per mesh renderer of a fresh instance.
    public void Build(GameObject instance)
    {
        CacheActiveColors();
        BuildParts(instance);
    }

    public void Clear()
    {
        _parts.Clear();
        _partsByHandle.Clear();
    }

    // Read the grabbed-handle look once from the configured active material (Gizmo_EmissiveSelected).
    private void CacheActiveColors()
    {
        _hasActiveBase = _hasActiveEmis = false;
        var fallback = _outlineConfig != null ? _outlineConfig.GizmoActiveColor : Color.cyan;
        var m = _config != null ? _config.ActiveMaterial : null;
        if (m != null)
        {
            if (m.HasProperty(BaseColorId))     { _hasActiveBase = true; _activeBase = m.GetColor(BaseColorId); }
            else if (m.HasProperty(ColorId))    { _hasActiveBase = true; _activeBase = m.GetColor(ColorId); }
            if (m.HasProperty(EmissionColorId)) { _hasActiveEmis = true; _activeEmis = m.GetColor(EmissionColorId); }
        }
        if (!_hasActiveBase) { _hasActiveBase = true; _activeBase = fallback; }
        if (!_hasActiveEmis) { _hasActiveEmis = true; _activeEmis = fallback; }
    }

    // One GizmoPart per mesh renderer. We keep the gizmo's native per-axis materials (instanced so
    // tinting never touches the shared assets) and add a SilhouetteOnly outline — see-through silhouette
    // behind occluders, like the rig bones, while the native solid mesh is the always-visible front
    // highlight (depth-tested, so overlapping handles never flicker as the OutlineAll mode did).
    // Multi-submesh renderers (the scale center: body + 3 axis legs) keep every submesh's own color.
    private void BuildParts(GameObject instance)
    {
        _parts.Clear();
        _partsByHandle.Clear();

        foreach (var mr in instance.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            // includeInactive is REQUIRED: at spawn only the active-mode group is enabled (the others are
            // hidden by ShowMode). Without it, GetComponentInParent skips inactive GOs → handle resolves
            // null for the hidden Rotate/Scale groups → their outlines go white and never map into
            // _partsByHandle (so hover/grab never reaches them). Move worked only because it's active.
            var handle = mr.GetComponentInParent<GizmoHandle>(includeInactive: true);

            // Accessing .materials instantiates per-renderer copies of the native gizmo materials and
            // assigns them back — capture BEFORE installing the Outline (which appends mask/fill passes).
            var mats    = mr.materials;
            var hasBase = new bool[mats.Length];
            var baseCol = new Color[mats.Length];
            var hasEmis = new bool[mats.Length];
            var emisCol = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.HasProperty(BaseColorId))     { hasBase[i] = true; baseCol[i] = m.GetColor(BaseColorId); }
                else if (m.HasProperty(ColorId))    { hasBase[i] = true; baseCol[i] = m.GetColor(ColorId); }
                if (m.HasProperty(EmissionColorId)) { hasEmis[i] = true; emisCol[i] = m.GetColor(EmissionColorId); }
            }

            var outlineColor = PartColor(handle);
            var outline      = InstallHandleOutline(mr.gameObject, outlineColor);

            var part = new GizmoPart
            {
                Materials   = mats,
                HasBase     = hasBase,
                BaseColor   = baseCol,
                HasEmis     = hasEmis,
                EmisColor   = emisCol,
                Outline     = outline,
                OutlineBase = outlineColor,
                Handle      = handle,
            };
            _parts.Add(part);
            if (handle != null)
            {
                if (!_partsByHandle.TryGetValue(handle, out var list))
                    _partsByHandle[handle] = list = new List<GizmoPart>();
                list.Add(part);
            }
        }
    }

    // Outline tint: per-axis handles get their axis color; centers/uniform-scale and handle-less meshes
    // get white. The mesh itself keeps its authored per-submesh colors; this only colors the silhouette.
    private Color PartColor(GizmoHandle handle)
    {
        if (handle == null) return Color.white;
        if (handle.Kind == HandleKind.ScaleUniform) return Color.white;
        return AxisColor(handle.Axis);
    }

    // SilhouetteOnly = see-through silhouette behind occluders only, like the rig bones.
    private Outline InstallHandleOutline(GameObject go, Color color)
    {
        var outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.AddComponent<Outline>();
        if (_outlineConfig != null)
            outline.SetOutlineMaterials(_outlineConfig.MaskMaterial, _outlineConfig.FillMaterial);
        outline.OutlineMode    = Outline.Mode.SilhouetteOnly;
        outline.OutlineColor   = color;
        outline.OutlineWidth   = 3f;
        outline.RenderPriority = 2; // over selection (0) and bones (1)
        return outline;
    }

    private Color AxisColor(AxisKind axis)
    {
        if (_outlineConfig == null) return Color.white;
        switch (axis)
        {
            case AxisKind.X: return _outlineConfig.AxisColorX;
            case AxisKind.Y: return _outlineConfig.AxisColorY;
            case AxisKind.Z: return _outlineConfig.AxisColorZ;
            default:         return Color.white;
        }
    }

    private static void SetBase(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId))     m.SetColor(ColorId, c);
    }

    private static void SetEmis(Material m, Color c)
    {
        m.EnableKeyword("_EMISSION");
        m.SetColor(EmissionColorId, c);
    }

    private static Color Scale(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

    // Grab: every submesh of the handle adopts the active material's look (base + emission); outline too.
    public void Recolor(GizmoHandle handle, Color outlineColor)
    {
        if (handle == null || !_partsByHandle.TryGetValue(handle, out var list)) return;
        foreach (var part in list)
        {
            for (int i = 0; i < part.Materials.Length; i++)
            {
                var m = part.Materials[i];
                if (m == null) continue;
                if (part.HasBase[i] && _hasActiveBase) SetBase(m, _activeBase);
                if (part.HasEmis[i] && _hasActiveEmis) SetEmis(m, _activeEmis);
            }
            if (part.Outline != null) part.Outline.OutlineColor = outlineColor;
        }
    }

    // Hover: darken each submesh toward its own captured base + emission (keeps per-axis distinction).
    public void Darken(GizmoHandle handle)
    {
        if (handle == null || !_partsByHandle.TryGetValue(handle, out var list)) return;
        foreach (var part in list)
        {
            for (int i = 0; i < part.Materials.Length; i++)
            {
                var m = part.Materials[i];
                if (m == null) continue;
                if (part.HasBase[i]) SetBase(m, Scale(part.BaseColor[i], HOVER_DARKEN));
                if (part.HasEmis[i]) SetEmis(m, Scale(part.EmisColor[i], HOVER_DARKEN));
            }
            if (part.Outline != null) part.Outline.OutlineColor = Scale(part.OutlineBase, HOVER_DARKEN);
        }
    }

    public void Restore(GizmoHandle handle)
    {
        if (handle == null || !_partsByHandle.TryGetValue(handle, out var list)) return;
        foreach (var part in list)
        {
            for (int i = 0; i < part.Materials.Length; i++)
            {
                var m = part.Materials[i];
                if (m == null) continue;
                if (part.HasBase[i]) SetBase(m, part.BaseColor[i]);
                if (part.HasEmis[i]) SetEmis(m, part.EmisColor[i]);
            }
            if (part.Outline != null) part.Outline.OutlineColor = part.OutlineBase;
        }
    }
}
