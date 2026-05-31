using NUnit.Framework;

public class AssetTypeBinaryCompatTests
{
    // JsonUtility serializes enums as their underlying int. These values MUST NOT change:
    // old data was Model=0 / Rig=1 / Texture=2 → maps to Object=0 / Rig=1 / Reference=2.
    [Test] public void Object_IsZero()    => Assert.AreEqual(0, (int)AssetType.Object);
    [Test] public void Rig_IsOne()        => Assert.AreEqual(1, (int)AssetType.Rig);
    [Test] public void Reference_IsTwo()  => Assert.AreEqual(2, (int)AssetType.Reference);
}
