using System.Collections.Generic;
using UnityEngine;

// One planned selector box: the bone it attaches to, and the world-space origins its box must
// encapsulate (converted to a bone-local AABB by the caller).
public readonly struct BoneBoxPlan
{
    public readonly Transform     Bone;
    public readonly List<Vector3> WorldOrigins;
    public BoneBoxPlan(Transform bone, List<Vector3> worldOrigins) { Bone = bone; WorldOrigins = worldOrigins; }
}

// Pure, build-time: walks the skeleton from `root` (depth 0). Every bone at depth 0..maxDepth gets a
// plan entry. depth < maxDepth → encapsulate the bone + its DIRECT children. depth == maxDepth →
// encapsulate the bone + ALL descendants. depth > maxDepth → no entry. `bones`, when non-null,
// restricts traversal to that set (the proxied bones); null = every transform child counts as a bone.
public static class BoneSelectorBoxPlanner
{
    public static List<BoneBoxPlan> Plan(Transform root, int maxDepth, HashSet<Transform> bones = null)
    {
        var result = new List<BoneBoxPlan>();
        if (root != null) Walk(root, 0, maxDepth, bones, result);
        return result;
    }

    private static void Walk(Transform bone, int depth, int maxDepth, HashSet<Transform> bones, List<BoneBoxPlan> result)
    {
        if (depth > maxDepth) return;

        var origins = new List<Vector3> { bone.position };
        if (depth < maxDepth)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                var c = bone.GetChild(i);
                if (!IsBone(c, bones)) continue;
                origins.Add(c.position);
            }
            result.Add(new BoneBoxPlan(bone, origins));
            for (int i = 0; i < bone.childCount; i++)
            {
                var c = bone.GetChild(i);
                if (!IsBone(c, bones)) continue;
                Walk(c, depth + 1, maxDepth, bones, result);
            }
        }
        else // depth == maxDepth: swallow the whole remaining subtree
        {
            CollectDescendants(bone, bones, origins);
            result.Add(new BoneBoxPlan(bone, origins));
        }
    }

    private static void CollectDescendants(Transform bone, HashSet<Transform> bones, List<Vector3> origins)
    {
        for (int i = 0; i < bone.childCount; i++)
        {
            var c = bone.GetChild(i);
            if (!IsBone(c, bones)) continue;
            origins.Add(c.position);
            CollectDescendants(c, bones, origins);
        }
    }

    private static bool IsBone(Transform t, HashSet<Transform> bones) => bones == null || bones.Contains(t);
}
