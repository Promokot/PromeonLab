using UnityEngine;

/// Prefab-authored interaction-layer assignment. Applies its layer to this
/// GameObject at runtime (Awake) and in the editor (OnValidate) for visibility.
[AddComponentMenu("PromeonLab/Interaction Layer Tag")]
public class InteractionLayerTag : MonoBehaviour
{
    [SerializeField] private InteractionLayer _layer = InteractionLayer.SceneObjects;

    private void Awake() => gameObject.SetInteractionLayer(_layer);

    private void OnValidate()
    {
        // Editor-time convenience: keep the GameObject's layer in sync while authoring.
        // Only assigns if the named layer exists (NameToLayer >= 0); harmless no-op otherwise.
        if (InteractionLayers.UnityLayer(_layer) >= 0)
            gameObject.SetInteractionLayer(_layer);
    }
}
