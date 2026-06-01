using NUnit.Framework;
using UnityEngine;

public partial class ObjectEntityBuilderTests
{
    [Test]
    public void HandledTypes_AreDistinct()
    {
        var obj = new ObjectEntityBuilder(null, null);
        var rig = new RigEntityBuilder(null, null);
        Assert.AreEqual(AssetType.Object, obj.HandledType);
        Assert.AreEqual(AssetType.Rig,    rig.HandledType);
    }

    [Test]
    public void RecipeFromInstance_Object_SetsConvexMesh()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var recipe = ObjectEntityBuilder.RecipeFromInstance(cube, AssetType.Object);
            Assert.AreEqual(AssetType.Object, recipe.type);
            Assert.IsTrue(recipe.selectable);
            Assert.AreEqual(InteractionLayer.SceneObjects, recipe.interactionLayer);
            Assert.AreEqual(ColliderKind.ConvexMesh, recipe.colliderKind);
        }
        finally { Object.DestroyImmediate(cube); }
    }
}
