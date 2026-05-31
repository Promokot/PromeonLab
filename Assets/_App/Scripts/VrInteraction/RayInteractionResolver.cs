using System.Collections.Generic;
using UnityEngine;

/// Scene-scoped. Given an interactor ray, returns the prioritized winning collider:
/// among everything the ray passes through on the interaction layers, the highest-priority
/// layer wins (GizmoHandles > BoneProxies > SceneObjects), with distance breaking ties
/// within a layer. Returns null when the ray hits nothing on those layers.
public class RayInteractionResolver
{
    private const int MaxHits = 32;
    private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
    private readonly List<int>      _priorities = new List<int>(MaxHits);
    private readonly List<float>    _distances  = new List<float>(MaxHits);
    private readonly List<Collider> _colliders  = new List<Collider>(MaxHits);

    public Collider ResolvePrimary(Ray ray, float maxDistance)
    {
        int count = Physics.RaycastNonAlloc(
            ray, _hits, maxDistance, InteractionLayers.Mask, QueryTriggerInteraction.Ignore);

        _priorities.Clear();
        _distances.Clear();
        _colliders.Clear();

        for (int i = 0; i < count; i++)
        {
            var col = _hits[i].collider;
            if (col == null) continue;
            if (!InteractionLayers.TryGetPriority(col.gameObject.layer, out var priority)) continue;
            _priorities.Add(priority);
            _distances.Add(_hits[i].distance);
            _colliders.Add(col);
        }

        int idx = InteractionLayers.PickWinnerIndex(_priorities, _distances);
        return idx < 0 ? null : _colliders[idx];
    }
}
