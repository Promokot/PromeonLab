using System.Collections.Generic;
using UnityEngine;

public class TimelineLanesView : MonoBehaviour
{
    [SerializeField] private RectTransform     _root;
    [SerializeField] private TimelineLaneView  _lanePrefab;

    private readonly List<TimelineLaneView> _lanePool = new();

    public IReadOnlyList<TimelineLaneView> Lanes => _lanePool;

    public void Rebuild(IReadOnlyList<(string TrackNodeId, bool IsBone)> tracks)
    {
        foreach (var l in _lanePool) if (l != null) l.gameObject.SetActive(false);

        if (_root == null || _lanePrefab == null) return;

        for (int i = 0; i < tracks.Count; i++)
        {
            var lane = GetOrCreate(i);
            lane.Bind(tracks[i].TrackNodeId, tracks[i].IsBone);
            lane.gameObject.SetActive(true);
        }
    }

    public TimelineLaneView FindLane(string trackNodeId)
    {
        foreach (var l in _lanePool)
            if (l != null && l.gameObject.activeSelf && l.TrackNodeId == trackNodeId) return l;
        return null;
    }

    private TimelineLaneView GetOrCreate(int idx)
    {
        while (_lanePool.Count <= idx)
        {
            var l = Instantiate(_lanePrefab, _root);
            l.gameObject.SetActive(false);
            _lanePool.Add(l);
        }
        return _lanePool[idx];
    }
}
