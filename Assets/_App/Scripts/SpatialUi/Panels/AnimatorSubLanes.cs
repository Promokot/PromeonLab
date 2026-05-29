using System.Collections.Generic;
using UnityEngine;

public class AnimatorSubLanes : MonoBehaviour
{
    [SerializeField] private RectTransform _root;
    [SerializeField] private TimelineLane  _lanePrefab;

    private readonly List<TimelineLane> _lanePool = new();

    public IReadOnlyList<TimelineLane> Lanes => _lanePool;

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

    public TimelineLane FindLane(string trackNodeId)
    {
        foreach (var l in _lanePool)
            if (l != null && l.gameObject.activeSelf && l.TrackNodeId == trackNodeId) return l;
        return null;
    }

    private TimelineLane GetOrCreate(int idx)
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
