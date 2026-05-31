//
//  Outline.cs
//  QuickOutline
//
//  Created by Chris Nolet on 3/30/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]

public class Outline : MonoBehaviour {
  private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

  public enum Mode {
    OutlineAll,
    OutlineVisible,
    OutlineHidden,
    OutlineAndSilhouette,
    SilhouetteOnly
  }

  public Mode OutlineMode {
    get { return outlineMode; }
    set {
      outlineMode = value;
      needsUpdate = true;
    }
  }

  public Color OutlineColor {
    get { return outlineColor; }
    set {
      outlineColor = value;
      needsUpdate = true;
    }
  }

  public float OutlineWidth {
    get { return outlineWidth; }
    set {
      outlineWidth = value;
      needsUpdate = true;
    }
  }

  // Supplies the source materials (forked PromeonLab/Outline* shaders). Called by app code that
  // owns the OutlineConfig ScriptableObject. Safe to call repeatedly; rebuilds only once.
  public void SetOutlineMaterials(Material maskSource, Material fillSource) {
    maskMaterialSource = maskSource;
    fillMaterialSource = fillSource;
    // Runtime-added components miss the OnEnable build (sources were null then); build now if possible.
    if (isActiveAndEnabled && outlineMaskMaterial == null && TryBuildMaterials())
      AppendMaterials();
  }

  public int RenderPriority {
    get { return renderPriority; }
    set {
      renderPriority = value;
      needsUpdate = true;
    }
  }

  [Serializable]
  private class ListVector3 {
    public List<Vector3> data;
  }

  [SerializeField]
  private Mode outlineMode;

  [SerializeField]
  private Color outlineColor = Color.white;

  [SerializeField, Range(0f, 10f)]
  private float outlineWidth = 2f;

  [Header("Optional")]

  [SerializeField, Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. "
  + "Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
  private bool precomputeOutline;

  [SerializeField, HideInInspector]
  private List<Mesh> bakeKeys = new List<Mesh>();

  [SerializeField, HideInInspector]
  private List<ListVector3> bakeValues = new List<ListVector3>();

  [SerializeField]
  private Material maskMaterialSource;

  [SerializeField]
  private Material fillMaterialSource;

  [SerializeField]
  private int renderPriority;

  // Per-instance stencil ref so overlapping outlines never clip each other's fill.
  // The active URP renderers (Assets/Settings/Mobile_Renderer.asset, PC_Renderer.asset) declare
  // no renderer features and overrideStencilState=0, so the whole 1..250 range is free.
  private const int STENCIL_MIN = 1;
  private const int STENCIL_MAX = 250;
  private const int QUEUE_STEP  = 20; // renderQueue gap between priority levels (mask+fill fit in one step)
  private static int nextStencilRef = STENCIL_MIN;
  private int stencilRef;

  private Renderer[] renderers;
  private Material outlineMaskMaterial;
  private Material outlineFillMaterial;

  private bool needsUpdate;

  void Awake() {

    // Cache renderers
    renderers = GetComponentsInChildren<Renderer>();

    // Ensure each renderer has at least one material per submesh, so QuickOutline's CombineSubmeshes
    // runs and the appended mask/fill cover the WHOLE mesh (not just the last submesh) on assets
    // whose mesh has more submeshes than materials.
    EnsureMaterialPerSubmesh();

    // Retrieve or generate smooth normals
    LoadSmoothNormals();

    // Apply material properties immediately
    needsUpdate = true;
  }

  void EnsureMaterialPerSubmesh() {
    foreach (var renderer in renderers) {
      if (renderer == null) continue;

      Mesh mesh = null;
      if (renderer is SkinnedMeshRenderer smr) {
        mesh = smr.sharedMesh;
      } else {
        var meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null) mesh = meshFilter.sharedMesh;
      }
      if (mesh == null) continue;

      var materials = renderer.sharedMaterials;
      if (materials.Length >= mesh.subMeshCount) continue; // already enough — no-op

      var padded = new Material[mesh.subMeshCount];
      for (int i = 0; i < padded.Length; i++) {
        padded[i] = materials[Mathf.Min(i, materials.Length - 1)];
      }
      renderer.sharedMaterials = padded;
    }
  }

  void OnEnable() {
    if (!TryBuildMaterials()) return; // SO not assigned yet (runtime-added); the setter will append later
    AppendMaterials();
  }

  void OnValidate() {

    // Update material properties
    needsUpdate = true;

    // Clear cache when baking is disabled or corrupted
    if (!precomputeOutline && bakeKeys.Count != 0 || bakeKeys.Count != bakeValues.Count) {
      bakeKeys.Clear();
      bakeValues.Clear();
    }

    // Generate smooth normals when baking is enabled
    if (precomputeOutline && bakeKeys.Count == 0) {
      Bake();
    }
  }

  void Update() {
    if (needsUpdate && outlineMaskMaterial != null) {
      needsUpdate = false;

      UpdateMaterialProperties();
    }
  }

  void OnDisable() {
    if (outlineMaskMaterial == null) return;
    foreach (var renderer in renderers) {

      // Remove outline shaders
      var materials = renderer.sharedMaterials.ToList();

      materials.Remove(outlineMaskMaterial);
      materials.Remove(outlineFillMaterial);

      renderer.materials = materials.ToArray();
    }
  }

  void OnDestroy() {

    // Destroy material instances
    if (outlineMaskMaterial != null) Destroy(outlineMaskMaterial);
    if (outlineFillMaterial != null) Destroy(outlineFillMaterial);
  }

  private bool TryBuildMaterials() {
    if (outlineMaskMaterial != null) return true;                 // already built
    if (maskMaterialSource == null || fillMaterialSource == null) // no sources yet
      return false;

    outlineMaskMaterial = Instantiate(maskMaterialSource);
    outlineFillMaterial = Instantiate(fillMaterialSource);

    outlineMaskMaterial.name = "OutlineMask (Instance)";
    outlineFillMaterial.name = "OutlineFill (Instance)";

    // Unique stencil ref per instance (cycles within the free range)
    stencilRef = nextStencilRef;
    nextStencilRef++;
    if (nextStencilRef > STENCIL_MAX) nextStencilRef = STENCIL_MIN;
    return true;
  }

  private void AppendMaterials() {
    foreach (var renderer in renderers) {
      var materials = renderer.sharedMaterials.ToList();
      materials.Add(outlineMaskMaterial);
      materials.Add(outlineFillMaterial);
      renderer.materials = materials.ToArray();
    }
    needsUpdate = true;
  }

  void Bake() {

    // Generate smooth normals for each mesh
    var bakedMeshes = new HashSet<Mesh>();

    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>()) {

      // Skip meshfilters without an assigned mesh (proxy/runtime GOs may briefly exist in this state).
      if (meshFilter.sharedMesh == null) {
        continue;
      }

      // Skip duplicates
      if (!bakedMeshes.Add(meshFilter.sharedMesh)) {
        continue;
      }

      // Skip non-readable meshes — vertex/normal access throws on isReadable=false
      if (!meshFilter.sharedMesh.isReadable) {
        continue;
      }

      // Serialize smooth normals
      var smoothNormals = SmoothNormals(meshFilter.sharedMesh);

      bakeKeys.Add(meshFilter.sharedMesh);
      bakeValues.Add(new ListVector3() { data = smoothNormals });
    }
  }

  void LoadSmoothNormals() {

    // Retrieve or generate smooth normals
    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>()) {

      // Skip meshfilters without an assigned mesh (proxy/runtime GOs may briefly exist in this state).
      if (meshFilter.sharedMesh == null) {
        continue;
      }

      // Skip if smooth normals have already been adopted
      if (!registeredMeshes.Add(meshFilter.sharedMesh)) {
        continue;
      }

      // Skip non-readable meshes — UV writes will throw on isReadable=false
      if (!meshFilter.sharedMesh.isReadable) {
        continue;
      }

      // Retrieve or generate smooth normals
      var index = bakeKeys.IndexOf(meshFilter.sharedMesh);
      var smoothNormals = (index >= 0) ? bakeValues[index].data : SmoothNormals(meshFilter.sharedMesh);

      // Store smooth normals in UV3
      meshFilter.sharedMesh.SetUVs(3, smoothNormals);

      // Combine submeshes
      var renderer = meshFilter.GetComponent<Renderer>();

      if (renderer != null) {
        CombineSubmeshes(meshFilter.sharedMesh, renderer.sharedMaterials);
      }
    }

    // Clear UV3 on skinned mesh renderers
    foreach (var skinnedMeshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {

      // Skip skinned meshes without an assigned mesh (NullReferenceException otherwise on .isReadable).
      if (skinnedMeshRenderer.sharedMesh == null) {
        continue;
      }

      // Skip if UV3 has already been reset
      if (!registeredMeshes.Add(skinnedMeshRenderer.sharedMesh)) {
        continue;
      }

      // Skip non-readable meshes — uv4 assignment will throw on isReadable=false
      if (!skinnedMeshRenderer.sharedMesh.isReadable) {
        continue;
      }

      // Clear UV3
      skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];

      // Combine submeshes
      CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
    }
  }

  List<Vector3> SmoothNormals(Mesh mesh) {

    // Group vertices by location
    var groups = mesh.vertices.Select((vertex, index) => new KeyValuePair<Vector3, int>(vertex, index)).GroupBy(pair => pair.Key);

    // Copy normals to a new list
    var smoothNormals = new List<Vector3>(mesh.normals);

    // Average normals for grouped vertices
    foreach (var group in groups) {

      // Skip single vertices
      if (group.Count() == 1) {
        continue;
      }

      // Calculate the average normal
      var smoothNormal = Vector3.zero;

      foreach (var pair in group) {
        smoothNormal += smoothNormals[pair.Value];
      }

      smoothNormal.Normalize();

      // Assign smooth normal to each vertex
      foreach (var pair in group) {
        smoothNormals[pair.Value] = smoothNormal;
      }
    }

    return smoothNormals;
  }

  void CombineSubmeshes(Mesh mesh, Material[] materials) {

    // Skip meshes with a single submesh
    if (mesh.subMeshCount == 1) {
      return;
    }

    // Skip if submesh count exceeds material count
    if (mesh.subMeshCount > materials.Length) {
      return;
    }

    // Append combined submesh
    mesh.subMeshCount++;
    mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
  }

  void UpdateMaterialProperties() {

    // Apply properties according to mode
    outlineFillMaterial.SetColor("_OutlineColor", outlineColor);

    switch (outlineMode) {
      case Mode.OutlineAll:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineVisible:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineHidden:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.OutlineAndSilhouette:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;

      case Mode.SilhouetteOnly:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
        break;
    }

    // Per-instance stencil ref: same value on mask (Replace) and fill (NotEqual) so the fill rim
    // tests against THIS instance's silhouette only — overlapping outlines no longer clip each other.
    outlineMaskMaterial.SetFloat("_StencilRef", stencilRef);
    outlineFillMaterial.SetFloat("_StencilRef", stencilRef);

    // Layered priority: higher RenderPriority paints later (on top). Selection=0, bones=1, gizmo=2.
    outlineMaskMaterial.renderQueue = 3100 + renderPriority * QUEUE_STEP;
    outlineFillMaterial.renderQueue = 3110 + renderPriority * QUEUE_STEP;
  }
}
