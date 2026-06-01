using UnityEditor;
using UnityEngine;

// Editor-only: turns a built-in Reference entry's Texture2D into asset-backed quad mesh + material +
// prefab, and returns the matching recipe. Mirrors ReferenceEntityBuilder.BuildAsync's recipe values
// and ReferenceEntityFactory's geometry/material so runtime and built-in references look identical.
public static class ReferenceImagePrefabGenerator
{
    private const string Dir      = "Assets/_App/Content/Generated/References";
    private const string MeshPath = Dir + "/ReferenceQuad.mesh";

    public static GameObject Generate(string id, Texture2D image, out AssetEntityRecipe recipe)
    {
        EnsureFolderExists(Dir);

        float aspect = image.height != 0 ? (float)image.width / image.height : 1f;

        // Shared centered-quad mesh asset (created once, reused by every reference prefab).
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null)
        {
            mesh = ReferenceEntityFactory.BuildCenteredQuad();
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }

        // Per-entry material asset (overwrite in place on re-generate).
        var profile = LoadRenderProfile();
        var matPath = $"{Dir}/{id}_Mat.mat";
        var mat = ReferenceEntityFactory.BuildMaterial(image, twoSided: true, profile);
        var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existingMat != null)
        {
            existingMat.shader      = mat.shader;
            existingMat.mainTexture = image;
            if (mat.HasProperty("_Cull")) existingMat.SetFloat("_Cull", mat.GetFloat("_Cull"));
            existingMat.doubleSidedGI = mat.doubleSidedGI;
            Object.DestroyImmediate(mat);
            mat = existingMat;
            EditorUtility.SetDirty(mat);
        }
        else
        {
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // Build the quad GameObject and save as a prefab.
        var go = new GameObject($"Ref_{id}");
        go.transform.localScale = new Vector3(aspect, 1f, 1f);
        go.AddComponent<MeshFilter>().sharedMesh       = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;

        var prefabPath = $"{Dir}/{id}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        if (prefab == null)
            Debug.LogError($"ReferenceImagePrefabGenerator: failed to save prefab at '{prefabPath}'.");

        // Persist the mesh/material/prefab assets so they survive without a manual Save Project.
        AssetDatabase.SaveAssets();

        const float h = 1f, gap = 0.5f;
        recipe = new AssetEntityRecipe
        {
            type               = AssetType.Reference,
            selectable         = true,
            interactionLayer   = InteractionLayer.SceneObjects,
            colliderKind       = ColliderKind.Box,
            colliderCenter     = Vector3.zero,
            colliderSize       = new Vector3(1f, h, 0.02f),
            spawnOffset        = new Vector3(0f, gap + h * 0.5f, 0f),
            referenceAspect    = aspect,
            referenceBottomGap = gap,
            referenceTwoSided  = true,
        };

        return prefab;
    }

    private static ImportRenderProfile LoadRenderProfile()
    {
        var guids = AssetDatabase.FindAssets("t:ImportRenderProfile");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<ImportRenderProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // AssetDatabase.CreateAsset needs every parent folder registered in the database (a plain
    // Directory.CreateDirectory leaves the DB unaware → CreateAsset fails on a clean project).
    private static void EnsureFolderExists(string path)
    {
        var parts   = path.Split('/'); // "Assets/_App/Content/Generated/References"
        var current = parts[0];        // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
