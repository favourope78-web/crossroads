using System.Collections.Generic;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>What a tracked world object should look like on the mini-map.</summary>
    public enum MarkerKind
    {
        Npc = 0,             // cyan dot - someone to talk to
        Enemy = 1,           // red dot - only while alive
        PointOfInterest = 2  // gold dot - doors, story markers, world actions
    }

    /// <summary>
    /// Zero-allocation registry of everything the mini-map plots (visual pass). NpcAgent,
    /// EnemyAgent and the interactable family register on enable and leave on destroy;
    /// the MiniMapHUD iterates the list at 5 Hz - no FindObjectsByType scans, no per-frame
    /// work, no garbage. Entries are only created/destroyed when spawns happen.
    /// </summary>
    public static class WorldMarkers
    {
        public sealed class Entry
        {
            public Transform transform;
            public MarkerKind kind;
            public string id;
        }

        private static readonly List<Entry> _entries = new List<Entry>(96);

        public static int Count { get { return _entries.Count; } }

        public static void Register(Component owner, MarkerKind kind)
        {
            if (owner == null) return;
            string id = owner.GetInstanceID().ToString();
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].id == id) return; // already tracked
            _entries.Add(new Entry { transform = owner.transform, kind = kind, id = id });
        }

        public static void Unregister(Component owner)
        {
            if (owner == null) return;
            string id = owner.GetInstanceID().ToString();
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].id == id) { _entries.RemoveAt(i); return; }
        }

        /// <summary>Indexer-style access for allocation-free iteration by the mini-map.</summary>
        public static Entry Get(int index) { return _entries[index]; }

        /// <summary>Tests: clear the registry between cases.</summary>
        public static void ClearForTests() { _entries.Clear(); }
    }
}
