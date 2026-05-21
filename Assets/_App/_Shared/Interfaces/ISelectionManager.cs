public interface ISelectionManager
{
    string SelectedNodeId { get; }     // null = nothing selected
    void Select(string nodeId);        // null = clear
}
