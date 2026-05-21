using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ModeTransitionGraph", menuName = "PromeonLab/ModeTransitionGraph")]
public class ModeTransitionGraph : ScriptableObject
{
    [System.Serializable]
    public struct Transition { public AppMode From; public AppMode To; }

    [SerializeField] private List<Transition> _allowed = new()
    {
        new Transition { From = AppMode.MainMenu,  To = AppMode.VrEditing  },
        new Transition { From = AppMode.VrEditing, To = AppMode.MainMenu   },
        new Transition { From = AppMode.MainMenu,  To = AppMode.Sandbox    },
        new Transition { From = AppMode.Sandbox,   To = AppMode.MainMenu   },
    };

    public bool IsAllowed(AppMode from, AppMode to)
    {
        foreach (var t in _allowed)
            if (t.From == from && t.To == to) return true;
        return false;
    }
}
