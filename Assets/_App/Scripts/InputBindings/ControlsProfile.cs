using UnityEngine;

[CreateAssetMenu(menuName = "PromeonLab/ControlsProfile")]
public class ControlsProfile : ScriptableObject
{
    [SerializeField] private int             _schemaVersion = 1;
    [SerializeField] private ControlBinding[] _bindings;

    public int             SchemaVersion => _schemaVersion;
    public ControlBinding[] Bindings     => _bindings;
}
