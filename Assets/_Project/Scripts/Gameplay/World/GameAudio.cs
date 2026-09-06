using System.Collections.Generic;
using Crossroads.Core;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Event-driven audio director (production polish pass). One scene component, zero
    /// per-frame allocations, reacts to the existing EventBus traffic - no gameplay system
    /// knows audio exists:
    ///
    ///   SFX      combat (swing/hit/dodge/hurt/defeat/alert/windup), abilities (per line
    ///            palette), UI (dialogue open, decision lock, save), travel transition
    ///   AMBIENT  one looping bed per location environment profile (hall / wind / water /
    ///            hollow), crossfaded over ambientCrossfade seconds on LocationArrivedEvent
    ///   MUSIC    calm -> tension (enemy alert) -> combat (live enemy) -> calm, crossfaded
    ///
    /// Mobile budget: 6 pooled one-shot sources (voice-stealing: oldest), 2 ambient + 2
    /// music sources for crossfades = 10 AudioSources total. Clips are placeholders
    /// synthesised by scripts/gen_audio.py; swap the AudioClip references in the scene
    /// generator when final audio lands - nothing here changes.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [Header("Combat")]
        public AudioClip attackSwing;
        public AudioClip attackHit;
        public AudioClip dodge;
        public AudioClip playerHurt;
        public AudioClip enemyDefeat;
        public AudioClip enemyAlert;
        public AudioClip enemyWindup;

        [Header("Abilities")]
        public AudioClip abilityEmber;
        public AudioClip abilityTide;
        public AudioClip abilityStone;
        public AudioClip abilityHollow;

        [Header("UI / story")]
        public AudioClip uiTap;
        public AudioClip uiConfirm;
        public AudioClip decisionLock;
        public AudioClip saveDone;
        public AudioClip transition;
        public AudioClip dialogueOpen;
        public AudioClip footstep;
        public AudioClip objectiveChime;     // objective started / completed
        public AudioClip abilityUnlock;      // new ability line unlocked

        [Header("Ambient beds (looping)")]
        public AudioClip ambHall;
        public AudioClip ambWind;
        public AudioClip ambWater;
        public AudioClip ambHollow;

        [Header("Music beds (looping placeholders)")]
        public AudioClip musicCalm;
        public AudioClip musicTension;
        public AudioClip musicCombat;

        [Header("Mix")]
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
        [Range(0f, 1f)] public float ambientVolume = 0.55f;
        [Range(0f, 1f)] public float musicVolume = 0.45f;
        public float ambientCrossfade = 1.6f;
        public float musicCrossfade = 1.2f;
        [Tooltip("Seconds of calm after the last live enemy before music drops back to the calm bed.")]
        public float combatCooldown = 4f;

        private const int OneShotVoices = 6;
        private readonly AudioSource[] _voices = new AudioSource[OneShotVoices];
        private readonly float[] _voiceStarted = new float[OneShotVoices];
        private AudioSource[] _ambient = new AudioSource[2];
        private AudioSource[] _music = new AudioSource[2];
        private int _ambientActive;
        private int _musicActive;
        private float _ambientBlend = 1f;   // 0..1 progress of the current ambient crossfade
        private float _musicBlend = 1f;
        private float _lastSwing;
        private float _combatUntil;
        private float _nextPresencePoll;
        private MusicState _musicState = MusicState.None;
        private AudioClip _pendingAmbient;

        public enum MusicState { None, Calm, Tension, Combat }
        public MusicState CurrentMusic { get { return _musicState; } }

        // ---------------------------------------------------------------- lifecycle
        private void Awake()
        {
            for (int i = 0; i < OneShotVoices; i++) _voices[i] = MakeSource("SFX_" + i, false);
            for (int i = 0; i < 2; i++) { _ambient[i] = MakeSource("Ambient_" + i, true); _music[i] = MakeSource("Music_" + i, true); }
        }

        private AudioSource MakeSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;   // 2D mix: the camera is the listener, positions are noise on a phone speaker
            src.volume = 0f;
            return src;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Subscribe<CombatantDefeatedEvent>(OnDefeated);
            EventBus.Subscribe<EnemyStateChangedEvent>(OnEnemyState);
            EventBus.Subscribe<AbilityUsedEvent>(OnAbility);
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<ObjectiveChangedEvent>(OnObjective);
            EventBus.Subscribe<AbilityUnlockedEvent>(OnAbilityUnlocked);
            EventBus.Subscribe<DecisionResolvedEvent>(OnDecisionResolved);
            EventBus.Subscribe<DecisionPromptEvent>(OnDecisionPrompt);
            EventBus.Subscribe<SaveCompletedEvent>(OnSaved);
            EventBus.Subscribe<LocationArrivedEvent>(OnArrived);
            EventBus.Subscribe<LocationDepartedEvent>(OnDeparted);
            EventBus.Subscribe<PlayerActionEvent>(OnPlayerAction);
            EventBus.Subscribe<StateResetEvent>(OnReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Unsubscribe<CombatantDefeatedEvent>(OnDefeated);
            EventBus.Unsubscribe<EnemyStateChangedEvent>(OnEnemyState);
            EventBus.Unsubscribe<AbilityUsedEvent>(OnAbility);
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnObjective);
            EventBus.Unsubscribe<AbilityUnlockedEvent>(OnAbilityUnlocked);
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnDecisionResolved);
            EventBus.Unsubscribe<DecisionPromptEvent>(OnDecisionPrompt);
            EventBus.Unsubscribe<SaveCompletedEvent>(OnSaved);
            EventBus.Unsubscribe<LocationArrivedEvent>(OnArrived);
            EventBus.Unsubscribe<LocationDepartedEvent>(OnDeparted);
            EventBus.Unsubscribe<PlayerActionEvent>(OnPlayerAction);
            EventBus.Unsubscribe<StateResetEvent>(OnReset);
        }

        private void Start()
        {
            SetMusic(MusicState.Calm, true);
            if (_pendingAmbient == null) _pendingAmbient = ambHall;
            PlayAmbient(_pendingAmbient, true);
        }

        // ---------------------------------------------------------------- SFX
        /// <summary>Plays a one-shot on the pool (steals the oldest voice when all 6 are busy).</summary>
        public void PlayOneShot(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            int slot = -1;
            float oldest = float.MaxValue;
            for (int i = 0; i < OneShotVoices; i++)
            {
                if (_voices[i] == null) continue;
                if (!_voices[i].isPlaying) { slot = i; break; }
                if (_voiceStarted[i] < oldest) { oldest = _voiceStarted[i]; slot = i; }
            }
            if (slot < 0) return;
            var src = _voices[slot];
            src.Stop();
            src.clip = clip;
            src.volume = volume * sfxVolume;
            src.pitch = pitch;
            src.Play();
            _voiceStarted[slot] = Time.unscaledTime;
        }

        private static float Vary(float amount) { return 1f + (UnityEngine.Random.value * 2f - 1f) * amount; }

        private void OnDamaged(CombatantDamagedEvent e)
        {
            if (e.isPlayer)
            {
                if (e.dodged) PlayOneShot(dodge, 0.8f, Vary(0.06f));
                else PlayOneShot(playerHurt, 1f, Vary(0.05f));
                _combatUntil = Time.unscaledTime + combatCooldown;
                return;
            }
            if (e.dodged) { PlayOneShot(attackSwing, 0.7f, Vary(0.08f)); return; }
            PlayOneShot(attackHit, 0.95f, Vary(0.07f));
            _combatUntil = Time.unscaledTime + combatCooldown;
        }

        private void OnDefeated(CombatantDefeatedEvent e)
        {
            if (!e.isPlayer) { PlayOneShot(enemyDefeat, 0.9f, Vary(0.05f)); return; }
            // player down: heavy low hit + the music falls back to calm (the respawn plays the transition)
            PlayOneShot(playerHurt, 1f, 0.72f);
            PlayOneShot(enemyDefeat, 0.6f, 0.6f);
            SetMusic(MusicState.Calm, false);
        }

        private void OnDecisionPrompt(DecisionPromptEvent e)
        {
            // a decision opens: soft confirm ping (timed prompts get a slightly higher, urgent pitch)
            PlayOneShot(uiConfirm, 0.55f, e.timeLimitSeconds > 0f ? 1.12f : 1f);
        }

        private void OnEnemyState(EnemyStateChangedEvent e)
        {
            switch (e.state)
            {
                case Crossroads.Gameplay.EnemyState.Alert:
                    PlayOneShot(enemyAlert, 0.7f, Vary(0.05f));
                    if (_musicState == MusicState.Calm) SetMusic(MusicState.Tension, false);
                    break;
                case Crossroads.Gameplay.EnemyState.AttackWindup:
                    PlayOneShot(enemyWindup, 0.65f, Vary(0.04f));
                    break;
                case Crossroads.Gameplay.EnemyState.Approach:
                case Crossroads.Gameplay.EnemyState.AttackRecover:
                    _combatUntil = Time.unscaledTime + combatCooldown;
                    break;
            }
        }

        private void OnPlayerAction(PlayerActionEvent e)
        {
            switch (e.action)
            {
                case PlayerAction.Attack:
                    if (Time.unscaledTime - _lastSwing < 0.08f) return; // one swing per press
                    _lastSwing = Time.unscaledTime;
                    PlayOneShot(attackSwing, 0.85f, Vary(0.08f));
                    break;
                case PlayerAction.Dodge: PlayOneShot(dodge, 0.8f, Vary(0.06f)); break;
                case PlayerAction.Interact: PlayOneShot(uiTap, 0.6f, 1f); break;
                case PlayerAction.Respawn: PlayOneShot(transition, 0.6f, 0.85f); break;
                case PlayerAction.Footstep: PlayOneShot(footstep, 0.35f, Vary(0.12f)); break;
            }
        }

        private void OnAbility(AbilityUsedEvent e)
        {
            AudioClip clip = ClipForAbility(e.abilityId);
            PlayOneShot(clip, 1f, Vary(0.03f));
        }

        /// <summary>Line palette by id prefix (mirrors AbilityPulseVFX.ColorFor).</summary>
        public AudioClip ClipForAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return uiConfirm;
            if (abilityId.StartsWith("ember")) return abilityEmber;
            if (abilityId.StartsWith("tide")) return abilityTide;
            if (abilityId.StartsWith("stone")) return abilityStone;
            if (abilityId.StartsWith("hollow") || abilityId.StartsWith("echo")) return abilityHollow;
            return uiConfirm;
        }

        private void OnDialogueStarted(DialogueStartedEvent e) { PlayOneShot(dialogueOpen, 0.7f, 1f); }
        private void OnAbilityUnlocked(AbilityUnlockedEvent e) { PlayOneShot(abilityUnlock, 0.9f, 1f); }
        private void OnObjective(ObjectiveChangedEvent e)
        {
            if (!ObjectiveChimes(e.phase, e.previousPhase)) return;
            PlayOneShot(objectiveChime, 0.7f, e.phase == ObjectivePhase.Completed ? 1f : 0.92f);
        }

        /// <summary>Only phase entries the player should notice chime: a new objective (Active) and
        /// its completion. Progress ticks and failures stay silent here (the HUD + decision-lock
        /// cover them) so back-to-back state churn never stacks stings.</summary>
        public static bool ObjectiveChimes(ObjectivePhase phase, ObjectivePhase previous)
        {
            if (phase == previous) return false;
            return phase == ObjectivePhase.Active || phase == ObjectivePhase.Completed;
        }
        private void OnDecisionResolved(DecisionResolvedEvent e) { PlayOneShot(decisionLock, 0.9f, 1f); }
        private void OnSaved(SaveCompletedEvent e) { if (e.ok) PlayOneShot(saveDone, 0.5f, 1f); }
        private void OnDeparted(LocationDepartedEvent e) { PlayOneShot(transition, 0.7f, 1f); }
        private void OnReset(StateResetEvent e) { SetMusic(MusicState.Calm, false); }

        // ---------------------------------------------------------------- ambient
        private void OnArrived(LocationArrivedEvent e)
        {
            AudioClip bed = AmbientForProfile(e.envProfile);
            if (_ambient[0] == null) { _pendingAmbient = bed; return; } // before Awake (boot order)
            PlayAmbient(bed, false);
        }

        /// <summary>Environment profile (content) -> ambient bed. Unknown profiles fall back to the hall hum.</summary>
        public AudioClip AmbientForProfile(string profile)
        {
            if (string.IsNullOrEmpty(profile)) return ambHall;
            if (profile.Contains("tide") || profile.Contains("docks") || profile.Contains("sanctum")) return ambWater;
            if (profile.Contains("dusk") || profile.Contains("summer") || profile.Contains("wall") || profile.Contains("market") || profile.Contains("after")) return ambWind;
            if (profile.Contains("fracture") || profile.Contains("spire") || profile.Contains("ascent") || profile.Contains("heart")) return ambHollow;
            return ambHall;
        }

        private void PlayAmbient(AudioClip clip, bool instant)
        {
            if (clip == null) return;
            var cur = _ambient[_ambientActive];
            if (cur != null && cur.clip == clip && cur.isPlaying) return;
            int next = 1 - _ambientActive;
            var src = _ambient[next];
            if (src == null) return;
            src.clip = clip;
            src.volume = instant ? ambientVolume : 0f;
            src.Play();
            if (instant && cur != null) cur.Stop();
            _ambientActive = next;
            _ambientBlend = instant ? 1f : 0f;
        }

        // ---------------------------------------------------------------- music
        public void SetMusic(MusicState state, bool instant)
        {
            if (state == _musicState) return;
            AudioClip clip = state == MusicState.Combat ? musicCombat : state == MusicState.Tension ? musicTension : musicCalm;
            _musicState = state;
            if (clip == null) return;
            int next = 1 - _musicActive;
            var src = _music[next];
            var cur = _music[_musicActive];
            if (src == null) return;
            src.clip = clip;
            src.volume = instant ? musicVolume : 0f;
            src.Play();
            if (instant && cur != null) cur.Stop();
            _musicActive = next;
            _musicBlend = instant ? 1f : 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // crossfades (equal-power)
            if (_ambientBlend < 1f)
            {
                _ambientBlend = Mathf.Min(1f, _ambientBlend + dt / Mathf.Max(ambientCrossfade, 0.01f));
                ApplyCrossfade(_ambient, _ambientActive, _ambientBlend, ambientVolume);
            }
            if (_musicBlend < 1f)
            {
                _musicBlend = Mathf.Min(1f, _musicBlend + dt / Mathf.Max(musicCrossfade, 0.01f));
                ApplyCrossfade(_music, _musicActive, _musicBlend, musicVolume);
            }

            // music state: live enemy -> combat; nothing for combatCooldown -> calm (4 Hz poll)
            if (Time.unscaledTime >= _nextPresencePoll)
            {
                _nextPresencePoll = Time.unscaledTime + 0.25f;
                bool live = Crossroads.Gameplay.Input.CombatPresence.HasLiveEnemy(CombatDirector.LiveEnemies);
                if (live && _combatUntil > Time.unscaledTime && _musicState != MusicState.Combat) SetMusic(MusicState.Combat, false);
                else if (!live || _combatUntil <= Time.unscaledTime)
                {
                    if (_musicState == MusicState.Combat) SetMusic(live ? MusicState.Tension : MusicState.Calm, false);
                    else if (_musicState == MusicState.Tension && !live) SetMusic(MusicState.Calm, false);
                }
            }
        }

        private static void ApplyCrossfade(AudioSource[] pair, int active, float blend, float target)
        {
            float tIn = Mathf.Sin(blend * Mathf.PI * 0.5f);
            float tOut = Mathf.Cos(blend * Mathf.PI * 0.5f);
            if (pair[active] != null) pair[active].volume = target * tIn;
            var other = pair[1 - active];
            if (other != null)
            {
                other.volume = target * tOut;
                if (blend >= 1f) other.Stop();
            }
        }
    }
}
