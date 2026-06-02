using NUnit.Framework;
using UnityEngine;

public class ThumbnailRendererFrameTests
{
    [Test]
    public void FrameDistance_UnitCubeAt60Fov_FitsBoundingSphere()
    {
        // unit cube: extents (0.5,0.5,0.5) -> bounding-sphere radius ~0.86603
        // d = radius / sin(fov/2) = 0.86603 / sin(30deg) = 0.86603 / 0.5 = 1.73205
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        var d = ThumbnailRenderer.FrameDistance(bounds, 60f);
        Assert.AreEqual(1.73205f, d, 0.001f);
    }

    [Test]
    public void FrameDistance_LargerBounds_GivesLargerDistance()
    {
        var small = ThumbnailRenderer.FrameDistance(new Bounds(Vector3.zero, Vector3.one), 60f);
        var big   = ThumbnailRenderer.FrameDistance(new Bounds(Vector3.zero, Vector3.one * 4f), 60f);
        Assert.Greater(big, small);
    }
}
