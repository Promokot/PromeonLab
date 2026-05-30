using NUnit.Framework;
using UnityEngine;

public class UserPanelLockModeTests
{
    [Test]
    public void CycleLockMode_WrapsThroughThreeModes()
    {
        var go = new GameObject("UserPanel");          // inactive-safe: Awake/Start not called in EditMode
        var panel = go.AddComponent<UserPanel>();

        Assert.AreEqual(UserPanel.LockMode.Follow, panel.CurrentLockMode);
        panel.CycleLockMode();
        Assert.AreEqual(UserPanel.LockMode.LockPosition, panel.CurrentLockMode);
        panel.CycleLockMode();
        Assert.AreEqual(UserPanel.LockMode.LockPositionRotation, panel.CurrentLockMode);
        panel.CycleLockMode();
        Assert.AreEqual(UserPanel.LockMode.Follow, panel.CurrentLockMode);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ResetPosition_ReturnsToFollow()
    {
        var go = new GameObject("UserPanel");
        var panel = go.AddComponent<UserPanel>();
        panel.CycleLockMode();                          // -> LockPosition
        panel.ResetPosition();
        Assert.AreEqual(UserPanel.LockMode.Follow, panel.CurrentLockMode);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void MoveTo_AppliesOnlyWhileDragging()
    {
        var go = new GameObject("UserPanel");
        var panel = go.AddComponent<UserPanel>();

        var target = new Vector3(3f, 4f, 5f);
        panel.MoveTo(target);                           // not dragging -> ignored
        Assert.AreNotEqual(target, panel.transform.position);

        panel.SetDragging(true);
        panel.MoveTo(target);                           // dragging -> applied
        Assert.AreEqual(target, panel.transform.position);

        panel.SetDragging(false);
        panel.MoveTo(new Vector3(9f, 9f, 9f));          // released -> ignored, holds last
        Assert.AreEqual(target, panel.transform.position);

        Object.DestroyImmediate(go);
    }
}
