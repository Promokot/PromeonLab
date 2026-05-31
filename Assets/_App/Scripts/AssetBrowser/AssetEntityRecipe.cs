using System;
using UnityEngine;

// The Build→Restore contract. Built once (at import or editor bake), applied verbatim at every
// spawn/scene-load so the entity's representation never drifts. JsonUtility-friendly (flat).
[Serializable]
public class AssetEntityRecipe
{
    public int              schemaVersion = 1;
    public AssetType        type;

    // Generic interaction capability.
    public bool             selectable = true;
    public InteractionLayer interactionLayer = InteractionLayer.SceneObjects;

    // Collider (local space).
    public ColliderKind     colliderKind = ColliderKind.Box;
    public Vector3          colliderCenter;
    public Vector3          colliderSize = Vector3.one;

    // Initial placement: world-space offset applied ONCE at fresh spawn (reload uses the saved pos).
    public Vector3          spawnOffset;

    // Reference-specific.
    public float            referenceAspect = 1f;
    public float            referenceBottomGap = 0.5f;
    public bool             referenceTwoSided = true;
}
