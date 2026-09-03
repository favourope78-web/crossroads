using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Prototype;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>Scene-level visual variant: player state -> body material (first match wins).</summary>
    [Serializable]
    public class NpcVisualVariantBinding
    {
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
        public Material material;
    }

    /// <summary>
    /// The complete NPC runtime (GAME_DESIGN §9.2: one character = one prefab + a state
    /// driver). Data comes from the content definition (NpcDefinitionData in the story
    /// library); this component only bridges it to the scene:
    ///   - identity/name/title/interactions  -> NpcBrain (data + live GameStateManager)
    ///   - behaviour FSM (idle/walk/talk/routine/react) -> NpcLogic + INpcWorld (transform)
    ///   - visuals: base material + per-state trim variants; optional real avatar prefab
    ///     slot (when the CHARACTER_REFERENCE meshes land, one canonical prefab per NPC)
    ///   - dialogue: runs the data-driven encounter via EncounterFlow (existing system)
    /// Live updates: on any state event (bond/flag/decision/item/rep/skill/area/load/reset)
    /// the brain/behaviour/title/interactions re-resolve - so an earlier decision visibly
    /// changes how this NPC behaves, what it says and what the INTERACT button says.
    /// </summary>
    public class NpcAgent : MonoBehaviour
    {
        [SerializeField] private string npcId = "";
        [Tooltip("Optional override; falls back to the content definition display name.")]
        [SerializeField] private string baseTitle = "";
        [Tooltip("Optional explicit reference; falls back to the object tagged Player.")]
        [SerializeField] private GameObject playerRef;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Material baseMaterial;
        [Tooltip("Character prefab per CHARACTER_REFERENCE sheet. Placed when real meshes exist.")]
        [SerializeField] private GameObject avatarPrefab;
        [Tooltip("Per-state material trims (line colours etc.): first matching conditions win.")]
        [SerializeField] private List<NpcVisualVariantBinding> visualVariants = new List<NpcVisualVariantBinding>();

        private NpcBrain _brain;
        private NpcLogic _logic;
        private AgentWorld _world;
        private bool _talking;
        private string _activeEncounterId = "";
        private string _lastTitle = "";
        private int _lastBond = int.MinValue;
        private float _nextPlayerSearch;
        private GameObject _avatarInstance;

        public string NpcId { get { return npcId; } }
        public bool BrainReady { get { return _brain != null; } }
        public string CurrentTitle { get { return _brain != null ? _brain.CurrentTitle : (!string.IsNullOrEmpty(baseTitle) ? baseTitle : npcId); } }
        public NpcMoodState BehaviourState { get { return _logic != null ? _logic.State : NpcMoodState.Idle; } }

        private void Start()
        {
            ResolvePlayer();
            if (_brain == null) BuildBrain();
            TrySpawnAvatar();
            Subscribe(true);
            Apply(silent: false, live: false);
        }

        private void OnDestroy()
        {
            Subscribe(false);
        }

        private void Subscribe(bool on)
        {
            if (on)
            {
                EventBus.Subscribe<BondChangedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<AffinityChangedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<FlagChangedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<DecisionResolvedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<ItemChangedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<ReputationChangedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<SkillChangedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<AbilityUnlockedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<AreaUnlockedEvent>(OnLiveStateEvent);
                EventBus.Subscribe<StateLoadedEvent>(OnLoadOrReset);
                EventBus.Subscribe<StateResetEvent>(OnLoadOrReset);
                EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
                EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
            }
            else
            {
                EventBus.Unsubscribe<BondChangedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<AffinityChangedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<FlagChangedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<DecisionResolvedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<ItemChangedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<ReputationChangedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<SkillChangedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<AbilityUnlockedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<AreaUnlockedEvent>(OnLiveStateEvent);
                EventBus.Unsubscribe<StateLoadedEvent>(OnLoadOrReset);
                EventBus.Unsubscribe<StateResetEvent>(OnLoadOrReset);
                EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
                EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
            }
        }

        private void OnLiveStateEvent(BondChangedEvent e) { Apply(silent: false, live: true); }
        private void OnLiveStateEvent(AffinityChangedEvent e) { Apply(silent: false, live: true); }
        private void OnLiveStateEvent(FlagChangedEvent e) { Apply(silent: false, live: true); }
        private void OnLiveStateEvent(DecisionResolvedEvent e) { Apply(silent: false, live: true); }
        private void OnLiveStateEvent(ItemChangedEvent e) { Apply(silent: false, live: true); }
        private void OnLiveStateEvent(ReputationChangedEvent e) { Apply(silent: false, live: true); }
        private void OnLiveStateEvent(SkillChangedEvent e) { Apply(silent: false, live: true); }
        private void OnLiveStateEvent(AbilityUnlockedEvent e) { Apply(silent: false, live: true); }
        private void OnLiveStateEvent(AreaUnlockedEvent e) { Apply(silent: false, live: true); }
        private void OnLoadOrReset(StateLoadedEvent e) { Apply(silent: false, live: false); }
        private void OnLoadOrReset(StateResetEvent e) { Apply(silent: false, live: false); }

        private void OnDialogueStarted(DialogueStartedEvent e)
        {
            if (!string.IsNullOrEmpty(_activeEncounterId) && e.encounterId == _activeEncounterId)
            {
                _talking = true;
                if (_logic != null) _logic.Reset();
            }
        }

        private void OnDialogueEnded(DialogueEndedEvent e)
        {
            if (!string.IsNullOrEmpty(_activeEncounterId) && e.encounterId == _activeEncounterId)
            {
                _talking = false;
                _activeEncounterId = "";
            }
        }

        private void BuildBrain()
        {
            if (!GameServices.IsInitialized || GameServices.Content == null || GameServices.Content.Content == null) return;
            NpcDefinitionData def = GameServices.Content.Content.FindNpc(npcId);
            if (def == null)
            {
                Debug.LogWarning("[CROSSROADS] NpcAgent '" + npcId + "' has no definition in the story content.");
                return;
            }
            _brain = new NpcBrain(def, GameServices.Progress);
            _logic = new NpcLogic(def.routine);
            _world = new AgentWorld(transform, this);
            _lastTitle = "";
            _lastBond = int.MinValue;
        }

        /// <summary>Re-resolves brain state + visuals against the current game state.</summary>
        public void Apply(bool silent, bool live)
        {
            if (_brain == null || !GameServices.IsInitialized) return;

            bool stateChanged = _brain.Reapply();

            // visuals: base material or first matching trim variant
            Material chosen = baseMaterial;
            for (int i = 0; i < visualVariants.Count; i++)
                if (ConditionEvaluator.Evaluate(visualVariants[i].conditions, GameServices.State))
                {
                    chosen = visualVariants[i].material != null ? visualVariants[i].material : baseMaterial;
                    break;
                }
            if (bodyRenderer != null && chosen != null) bodyRenderer.sharedMaterial = chosen;

            if (stateChanged && _logic != null) _logic.Reset();

            // publish only when something the UI could show actually changed
            bool bondChanged = _lastBond != _brain.Bond;
            bool titleChanged = _lastTitle != _brain.CurrentTitle;
            _lastBond = _brain.Bond;
            _lastTitle = _brain.CurrentTitle;
            if (!silent && (stateChanged || bondChanged || titleChanged))
            {
                EventBus.Publish(new NpcStatusChangedEvent
                {
                    npcId = npcId,
                    title = _brain.CurrentTitle,
                    bond = _brain.Bond,
                    bondTier = _brain.BondTier,
                    moodLine = _brain.MoodLine
                });
                if (live && stateChanged && !string.IsNullOrEmpty(_brain.MoodLine))
                {
                    EventBus.Publish(new NoticeRequestEvent { text = _brain.MoodLine });
                }
            }
        }

        /// <summary>Current INTERACT label (first available interaction; "" = none).</summary>
        public string PromptLabel()
        {
            return _brain != null ? _brain.PromptLabel() : "Talk";
        }

        /// <summary>Available interactions right now (data-driven, condition-gated).</summary>
        public List<NpcInteractionData> AvailableInteractions()
        {
            return _brain != null ? _brain.AvailableInteractions() : new List<NpcInteractionData>();
        }

        /// <summary>Relationship value with the player (-100..100, persisted).</summary>
        public int Bond { get { return _brain != null ? _brain.Bond : 0; } }

        /// <summary>Runs the data-driven conversation (existing EncounterFlow) for the first
        /// available interaction, freezing the NPC while the dialogue is open.</summary>
        public void Interact(GameObject player)
        {
            if (!GameServices.IsInitialized || _brain == null) return;
            NpcInteractionData interaction = _brain.DefaultInteraction();
            if (interaction == null) return;
            _activeEncounterId = interaction.encounterId;
            _talking = true;
            if (_logic != null) _logic.Reset();
            GameServices.Encounters.Run(interaction.encounterId, _brain.CurrentTitle);
            Debug.Log("[CROSSROADS] NPC " + npcId + " interaction '" + interaction.id + "' -> " + interaction.encounterId);
        }

        private void Update()
        {
            if (_logic == null || _world == null) return;
            ResolvePlayerLazily();

            Point3 playerPos = PlayerPoint();
            bool playerActive = _player != null;
            _logic.Tick(_world, Time.deltaTime, playerPos, playerActive, _brain != null ? _brain.Profile : new NpcProfile(), _talking);
        }

        // ---------------------------------------------------------------- player lookup
        private GameObject _player;

        private void ResolvePlayer()
        {
            if (playerRef != null) { _player = playerRef; return; }
            _player = GameObject.FindGameObjectWithTag("Player");
        }

        private void ResolvePlayerLazily()
        {
            if (_player != null) return;
            if (Time.time < _nextPlayerSearch) return;
            _nextPlayerSearch = Time.time + 1f;
            ResolvePlayer();
        }

        private Point3 PlayerPoint()
        {
            return _player != null ? new Point3(_player.transform.position.x, _player.transform.position.y, _player.transform.position.z) : new Point3(0f, 0f, 0f);
        }

        // ---------------------------------------------------------------- avatar
        private void TrySpawnAvatar()
        {
            if (avatarPrefab == null) return;
            if (_avatarInstance == null)
            {
                _avatarInstance = (GameObject)Instantiate(avatarPrefab);
                _avatarInstance.name = "Avatar_" + npcId;
                _avatarInstance.transform.SetParent(transform, false);
                _avatarInstance.transform.localPosition = Vector3.zero;
            }
            // hide the placeholder primitives (Body/Head) once a real mesh exists
            foreach (var child in transform.GetComponentsInChildren<Renderer>(true))
            {
                if (child == bodyRenderer) continue;
                if (child.name.StartsWith("Body") || child.name.StartsWith("Head"))
                    child.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- movement sink
        /// <summary>Unity implementation of INpcWorld: transform movement + optional animator.</summary>
        private class AgentWorld : INpcWorld
        {
            private readonly Transform _transform;
            private readonly NpcAgent _agent;
            private Animator _animator;
            private static readonly int SpeedHash = Animator.StringToHash("Speed");

            public AgentWorld(Transform transform, NpcAgent agent)
            {
                _transform = transform;
                _agent = agent;
                _animator = transform != null ? transform.GetComponentInChildren<Animator>() : null;
            }

            public Point3 NpcPosition
            {
                get
                {
                    Vector3 p = _transform.position;
                    return new Point3(p.x, p.y, p.z);
                }
            }

            public void NpcMoveTowards(Point3 target, float speed, float dt)
            {
                Vector3 pos = _transform.position;
                _transform.position = Vector3.MoveTowards(pos, new Vector3(target.x, target.y, target.z), speed * dt);
                SetMoving(true);
            }

            public void NpcFaceTowards(Point3 target, float turnSpeed, float dt)
            {
                Vector3 pos = _transform.position;
                Vector3 dir = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
                if (dir.sqrMagnitude < 0.0001f) return;
                Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                _transform.rotation = Quaternion.Slerp(_transform.rotation, look, Mathf.Clamp01(turnSpeed * dt));
            }

            private void SetMoving(bool moving)
            {
                if (_animator != null) _animator.SetFloat(SpeedHash, moving ? 1f : 0f, 0.2f, Time.deltaTime);
            }
        }
    }
}
