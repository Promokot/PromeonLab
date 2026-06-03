using NUnit.Framework;
using UnityEngine;

public class OutlineMaterialsTests
{
    private static Material NewMat() => new Material(Shader.Find("Universal Render Pipeline/Unlit"));

    [Test]
    public void WithOutlineMaterials_AppendsMaskAndFillOnce()
    {
        var baseMat = NewMat();
        var mask    = NewMat();
        var fill    = NewMat();

        var result = Outline.WithOutlineMaterials(new[] { baseMat }, mask, fill);

        CollectionAssert.AreEqual(new[] { baseMat, mask, fill }, result);

        Object.DestroyImmediate(baseMat);
        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(fill);
    }

    [Test]
    public void WithOutlineMaterials_IsIdempotent_NoAccumulation()
    {
        var baseMat = NewMat();
        var mask    = NewMat();
        var fill    = NewMat();

        var once  = Outline.WithOutlineMaterials(new[] { baseMat }, mask, fill);
        var twice = Outline.WithOutlineMaterials(once, mask, fill);

        // Re-applying must NOT add a second mask/fill pair (the flat-fill accumulation bug).
        CollectionAssert.AreEqual(new[] { baseMat, mask, fill }, twice);

        Object.DestroyImmediate(baseMat);
        Object.DestroyImmediate(mask);
        Object.DestroyImmediate(fill);
    }
}
