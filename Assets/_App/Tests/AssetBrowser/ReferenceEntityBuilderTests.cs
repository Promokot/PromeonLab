using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

public class ReferenceEntityBuilderTests
{
    [Test]
    public void BuildAsync_FromImage_SetsAspectAndColliderFromDimensions()
    {
        var tex = new Texture2D(4, 2);
        var png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        var path = Path.Combine(Application.temporaryCachePath, "ref_test.png");
        File.WriteAllBytes(path, png);

        var builder = new ReferenceEntityBuilder(null, null);
        var recipe  = builder.BuildAsync(path, AssetType.Reference, CancellationToken.None).Result;

        Assert.AreEqual(AssetType.Reference, recipe.type);
        Assert.That(recipe.referenceAspect, Is.EqualTo(2f).Within(0.01f));
        Assert.That(recipe.colliderCenter.y, Is.EqualTo(1f).Within(0.01f));
        Assert.That(recipe.colliderSize.x, Is.EqualTo(2f).Within(0.01f));
        Assert.IsTrue(recipe.selectable);
    }
}
