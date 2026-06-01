using NUnit.Framework;
using UnityEngine;

public class RigEntityBuilderRecipeTests
{
    [Test]
    public void RecipeFromInstance_Rig_ExtractsSkeleton_AndFoldsAxis()
    {
        var root = new GameObject("rig");
        var bone = new GameObject("pelvis");
        bone.transform.SetParent(root.transform);
        var smr = root.AddComponent<SkinnedMeshRenderer>();
        smr.bones = new[] { bone.transform };
        try
        {
            var recipe = RigEntityBuilder.RecipeFromInstance(
                root, new BoundsBoxColliderStrategy(), TerminalBoneAxis.X, invert: true);

            Assert.AreEqual(AssetType.Rig, recipe.type);
            Assert.IsTrue(recipe.HasRig, "skeleton with one bone must populate recipe.rig");
            Assert.AreEqual("pelvis", recipe.rig.Bones[0].BoneName, "bone names must be extracted from smr.bones");
            Assert.AreEqual(TerminalBoneAxis.X, recipe.rig.TerminalBonesAxis);
            Assert.IsTrue(recipe.rig.InvertTerminalBonesAxis);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void RecipeFromInstance_Rig_NoSkeleton_LeavesRigNull()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // no SkinnedMeshRenderer
        try
        {
            var recipe = RigEntityBuilder.RecipeFromInstance(
                go, new BoundsBoxColliderStrategy(), TerminalBoneAxis.Auto, invert: false);
            Assert.IsFalse(recipe.HasRig, "no skeleton → recipe.rig stays null (graceful static object)");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
