using NUnit.Framework;
using UnityEngine;

public class RigEntityBuilderRecipeTests
{
    [Test]
    public void RecipeFromInstance_Rig_ExtractsSkeleton_FoldsAxis_SetsBoneBoxes()
    {
        var root = new GameObject("rig");
        var bone = new GameObject("pelvis");
        bone.transform.SetParent(root.transform);
        var smr = root.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new[] { bone.transform };
        try
        {
            var recipe = RigEntityBuilder.RecipeFromInstance(root, TerminalBoneAxis.X, invert: true);

            Assert.AreEqual(AssetType.Rig, recipe.type);
            Assert.IsTrue(recipe.HasRig, "skeleton with one bone must populate recipe.rig");
            Assert.AreEqual("pelvis", recipe.rig.Bones[0].BoneName);
            Assert.AreEqual(TerminalBoneAxis.X, recipe.rig.TerminalBonesAxis);
            Assert.IsTrue(recipe.rig.InvertTerminalBonesAxis);
            Assert.AreEqual(ColliderKind.BoneBoxes, recipe.colliderKind);
            Assert.AreEqual(3, recipe.boneColliderDepth);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void RecipeFromInstance_Rig_NoSkeleton_FallsBackToConvexMesh()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // no SkinnedMeshRenderer
        try
        {
            var recipe = RigEntityBuilder.RecipeFromInstance(go, TerminalBoneAxis.Auto, invert: false);
            Assert.IsFalse(recipe.HasRig, "no skeleton → recipe.rig stays null");
            Assert.AreEqual(ColliderKind.ConvexMesh, recipe.colliderKind, "skeleton-less rig is a static mesh");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
