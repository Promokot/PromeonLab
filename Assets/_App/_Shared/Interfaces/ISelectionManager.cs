public interface ISelectionManager
{
    string SelectedNodeId { get; }
    void Select(string nodeId);
}
