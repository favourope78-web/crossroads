using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>Data-driven relocation: persisted location key -> scene placement.</summary>
    [Serializable]
    public class NpcRelocationBinding
    {
        [Tooltip("NPC the relocation applies to (must match the NpcAgent npcId).")]
        public string npcId = "";
        [Tooltip("Location key as persisted by the MoveNpc effect / world state.")]
        public string locationKey = "";
        [Tooltip("Where the NPC stands while this location is active.")]
        public Transform target;
        [Tooltip("One-liner shown the first time the relocation applies live.")]
        public string notice = "";
    }

    /// <summary>
    /// Applies PERSISTED NPC locations (world-state tracking: "NPC locations/states")
    /// to the scene: at start (after the save is loaded - relocations survive restarts)
    /// and live on NpcRelocatedEvent (a decision/objective just moved someone).
    /// The authoritative key lives in GameState (npcLocations); this component only
    /// maps keys to transforms and delegates the actual move to NpcAgent.RelocateTo
    /// (which also pins the routine so the NPC stays put).
    /// </summary>
    public class NpcRelocator : MonoBehaviour
    {
        [SerializeField] private List<NpcRelocationBinding> bindings = new List<NpcRelocationBinding>();
        [SerializeField] private bool toastOnLiveMove = true;

        private readonly Dictionary<string, NpcAgent> _agents = new Dictionary<string, NpcAgent>();
        private readonly HashSet<string> _announced = new HashSet<string>();

        private void Start()
        {
            CacheAgents();
            ApplyFromState();
            EventBus.Subscribe<NpcRelocatedEvent>(OnNpcRelocated);
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<NpcRelocatedEvent>(OnNpcRelocated);
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        private void CacheAgents()
        {
            _agents.Clear();
            NpcAgent[] found = FindObjectsByType<NpcAgent>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null && !string.IsNullOrEmpty(found[i].NpcId))
                    _agents[found[i].NpcId] = found[i];
        }

        private void OnStateLoaded(StateLoadedEvent e) { ApplyFromState(); }
        private void OnStateReset(StateResetEvent e) { ApplyFromState(); }

        private void OnNpcRelocated(NpcRelocatedEvent e)
        {
            ApplyBinding(e.npcId, e.locationKey, live: true);
        }

        /// <summary>Replays the persisted npcLocations onto the scene (boot/restart proof).</summary>
        public void ApplyFromState()
        {
            if (!GameServices.IsInitialized) return;
            for (int i = 0; i < bindings.Count; i++)
            {
                NpcRelocationBinding b = bindings[i];
                if (b == null || string.IsNullOrEmpty(b.npcId) || b.target == null) continue;
                string current = GameServices.State.GetNpcLocation(b.npcId, "");
                if (current == b.locationKey) ApplyBinding(b.npcId, b.locationKey, live: false);
            }
            StoryLog.Log("[CROSSROADS] NPC relocations applied (" + bindings.Count + " binding(s))");
        }

        private void ApplyBinding(string npcId, string locationKey, bool live)
        {
            NpcRelocationBinding match = null;
            for (int i = 0; i < bindings.Count; i++)
                if (bindings[i] != null && bindings[i].npcId == npcId && bindings[i].locationKey == locationKey)
                { match = bindings[i]; break; }
            if (match == null || match.target == null) return;

            NpcAgent agent;
            if (!_agents.TryGetValue(npcId, out agent) || agent == null)
            {
                CacheAgents();
                _agents.TryGetValue(npcId, out agent);
            }
            if (agent == null) return;

            Vector3 pos = match.target.position;
            agent.RelocateTo(new Point3(pos.x, pos.y, pos.z));
            if (live && toastOnLiveMove && !string.IsNullOrEmpty(match.notice) && _announced.Add(npcId + ":" + locationKey))
                EventBus.Publish(new NoticeRequestEvent { text = match.notice });
            StoryLog.Log("[CROSSROADS] Relocated NPC " + npcId + " -> " + locationKey);
        }
    }
}
