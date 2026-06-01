using NUnit.Framework;
using UnityEngine;

public class ReferenceEntityFactoryQuadTests
{
    [Test]
    public void BuildCenteredQuad_IsUnitCentered_FourVertsTwoTris()
    {
        var mesh = ReferenceEntityFactory.BuildCenteredQuad();
        try
        {
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            Assert.AreEqual(new Vector3(-0.5f, -0.5f, 0f), mesh.vertices[0]);
            Assert.AreEqual(new Vector3( 0.5f, -0.5f, 0f), mesh.vertices[1]);
            Assert.AreEqual(new Vector3( 0.5f,  0.5f, 0f), mesh.vertices[2]);
            Assert.AreEqual(new Vector3(-0.5f,  0.5f, 0f), mesh.vertices[3]);
            Assert.AreEqual(new Vector2(0f, 0f), mesh.uv[0]); // UV order tracks vertex order
        }
        finally { Object.DestroyImmediate(mesh); }
    }
}
