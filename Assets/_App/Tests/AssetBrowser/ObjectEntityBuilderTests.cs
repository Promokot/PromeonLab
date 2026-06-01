using NUnit.Framework;
using UnityEngine;

public partial class ObjectEntityBuilderTests
{
    [Test]
    public void HandledTypes_AreDistinct()
    {
        var obj = new ObjectEntityBuilder(null, null, null);
        var rig = new RigEntityBuilder(null, null, null);
        Assert.AreEqual(AssetType.Object, obj.HandledType);
        Assert.AreEqual(AssetType.Rig,    rig.HandledType);
    }
}

public partial class ObjectEntityBuilderTests
{
    [Test]
    public void RecipeFromInstance_Object_MeasuresBoxAndSetsCapability()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); // 1x1x1, centered
        try
        {
            var recipe = ObjectEntityBuilder.RecipeFromInstance(cube, new BoundsBoxColliderStrategy(), AssetType.Object);

            Assert.AreEqual(AssetType.Object, recipe.type);
            Assert.IsTrue(recipe.selectable);
            Assert.AreEqual(InteractionLayer.SceneObjects, recipe.interactionLayer);
            Assert.AreEqual(ColliderKind.Box, recipe.colliderKind);
            Assert.That(recipe.colliderSize.x, Is.EqualTo(1f).Within(0.05f));
            Assert.That(recipe.colliderSize.y, Is.EqualTo(1f).Within(0.05f));
        }
        finally { Object.DestroyImmediate(cube); }
    }
}
