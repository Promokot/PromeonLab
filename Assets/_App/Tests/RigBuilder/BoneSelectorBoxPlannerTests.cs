using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class BoneSelectorBoxPlannerTests
{
    // root(0) → a(1) → b(2) → c(3) → d(4). maxDepth 3 → entries for root,a,b,c; c (depth 3)
    // encapsulates d; no entry for d.
    [Test]
    public void Plan_StopsAtDepth_AndDepth3SwallowsSubtree()
    {
        var t = new Transform[5];
        for (int i = 0; i < 5; i++) t[i] = new GameObject($"b{i}").transform;
        for (int i = 1; i < 5; i++) t[i].SetParent(t[i - 1]);
        t[4].position = new Vector3(0, 0, 10f); // distinct so encapsulation is observable
        try
        {
            var plan = BoneSelectorBoxPlanner.Plan(t[0], maxDepth: 3);

            var bones = plan.Select(p => p.Bone).ToList();
            Assert.Contains(t[0], bones);
            Assert.Contains(t[1], bones);
            Assert.Contains(t[2], bones);
            Assert.Contains(t[3], bones);
            Assert.IsFalse(bones.Contains(t[4]), "depth 4 bone gets no own entry");

            var depth3 = plan.First(p => p.Bone == t[3]);
            Assert.IsTrue(depth3.WorldOrigins.Contains(t[4].position),
                "depth-3 box must encapsulate the entire remaining subtree (the depth-4 origin)");
        }
        finally
        {
            // Destroying the chain root cascades to its children, so null-guard the rest
            // (a destroyed Transform reports as Unity-null).
            for (int i = 0; i < 5; i++)
                if (t[i] != null) Object.DestroyImmediate(t[i].gameObject);
        }
    }
}
