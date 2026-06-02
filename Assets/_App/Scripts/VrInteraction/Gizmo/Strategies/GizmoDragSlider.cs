using UnityEngine;

/// <summary>
/// Self-establishing 1D slider for gizmo drags. The first controller movement past the deadzone locks a
/// reference direction; afterwards it reports the signed displacement along that direction, baselined so
/// the value is 0 at the moment of lock (no pop). Replaces pivot-relative magnitude math so the rate is
/// uniform and there is no blow-up near the object center.
/// </summary>
public struct GizmoDragSlider
{
    private Vector3 _start;
    private Vector3 _refDir;
    private float   _deadzone;
    private bool    _locked;

    public void Begin(Vector3 handPos, float deadzone)
    {
        _start    = handPos;
        _deadzone = Mathf.Max(0f, deadzone);
        _refDir   = Vector3.zero;
        _locked   = false;
    }

    /// Returns false while still inside the deadzone (direction not yet established → apply no change).
    public bool TryGetSignedDisplacement(Vector3 handPos, out float s)
    {
        var delta = handPos - _start;
        if (!_locked)
        {
            if (delta.magnitude <= _deadzone) { s = 0f; return false; }
            _refDir = delta.normalized;
            _locked = true;
        }
        s = Vector3.Dot(handPos - _start, _refDir) - _deadzone;
        return true;
    }
}
