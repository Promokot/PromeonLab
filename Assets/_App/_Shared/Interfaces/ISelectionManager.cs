using System.Collections.Generic;

public interface ISelectionManager
{
    string               SelectedNodeId { get; }      // back-compat = ActiveId
    string               ActiveId       { get; }
    IReadOnlyList<string> SelectedIds   { get; }

    void Select(string nodeId);     // single-select; clears + adds (back-compat)
    void Toggle(string nodeId);     // multi-select add/remove
    void Clear();
}
