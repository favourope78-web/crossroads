using System;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Strongly-typed app facade over AppServices (GAME_DESIGN §13.4 service architecture).
    /// Owns the single GameState + StateMutator, DecisionManager, SaveSystem and EncounterFlow,
    /// and wires the design rules that cross them:
    ///   - decisions autosave immediately after resolution (§12.3: "after every decision node resolution")
    ///   - mobile lifecycle saves (app pause/focus) are driven by the scene bootstrapper
    ///   - boot loads the last save and applies it to the world (persistence proof on restart)
    /// Pure C# - headless-testable (serializer / path provider / content are injected).
    /// </summary>
    public static class GameServices
    {
        public static bool IsInitialized { get; private set; }

        public static StateMutator State { get; private set; }
        public static DecisionManager Decisions { get; private set; }
        public static SaveSystem Save { get; private set; }
        public static EncounterFlow Encounters { get; private set; }
        public static IEncounterSource Content { get; private set; }

        /// <summary>Player-current-state façade (reputation, bonds, skills, unlocks, areas, attributes).</summary>
        public static GameStateManager Progress { get; private set; }

        /// <summary>Power/ability runtime: access states, activation, cooldowns (data-driven).</summary>
        public static Crossroads.Narrative.AbilityManager Abilities { get; private set; }

        public static string SceneKey { get; private set; }
        public static string CheckpointId { get; private set; }

        /// <summary>
        /// Boots the run: fresh state -> register content -> load save (if any) ->
        /// start a save session bound to the live state.
        /// </summary>
        public static void Init(IJsonSerializer json, IPathProvider paths, IEncounterSource content,
                                string sceneKey = "FirstLocation", string checkpointId = "hall_spawn",
                                int slot = 0, bool loadExisting = true)
        {
            Shutdown(silent: true);

            Content = content ?? new RuntimeContentSource();
            State = new StateMutator(new GameState());
            Progress = null;
            Save = new SaveSystem(json ?? new UnityJsonSerializer(), paths);
            Decisions = new DecisionManager(State);
            Decisions.RegisterAll(Content.Content != null ? Content.Content.decisions : null);
            Progress = new GameStateManager(State, Content);
            Decisions.Index = Progress.Index;
            Abilities = new Crossroads.Narrative.AbilityManager(
                Content.Content != null ? Content.Content.progression.abilities : null, Progress);
            Encounters = new EncounterFlow(Content, Decisions, State);

            SceneKey = sceneKey;
            CheckpointId = checkpointId;
            Save.StartSession("Ari - " + sceneKey, sceneKey, checkpointId, State.State, slot);

            bool hadSave = false;
            string path = Save.SavePath;
            if (loadExisting)
            {
                SaveData data = Save.Load(slot);
                if (data != null)
                {
                    State.LoadFrom(data.gameState);
                    hadSave = true;
                    path = Save.SavePath;
                    StoryLog.Log("[CROSSROADS] Save loaded (" + State.State.decisions.Count + " decision(s)) from " + path);
                }
            }

            Decisions.Resolved += OnDecisionResolved;
            EventBus.Subscribe<AreaUnlockedEvent>(OnAreaUnlocked);
            EventBus.Subscribe<AreaChangedEvent>(OnAreaChanged);

            IsInitialized = true;
            EventBus.Publish(new StateLoadedEvent { hadSave = hadSave, path = path });
        }

        /// <summary>Autosave hook: fires right after any decision resolution (§12.3).</summary>
        private static void OnDecisionResolved(string decisionId, string optionId)
        {
            PersistNow(autosaveMirror: true);
        }

        private static void OnAreaUnlocked(AreaUnlockedEvent e) { PersistNow(autosaveMirror: true); }
        private static void OnAreaChanged(AreaChangedEvent e) { PersistNow(autosaveMirror: true); }

        /// <summary>Writes the live state to disk (decisions, flags, affinities, world state...).</summary>
        public static SaveReport PersistNow(bool autosaveMirror = true)
        {
            if (Save == null || !Save.HasSession) return SaveReport.Failure("", "not initialized");
            return Save.Persist(autosaveMirror);
        }

        /// <summary>DEV/testing helper: wipes the slot + autosave and reboots with a fresh state.</summary>
        public static SaveReport ResetRun(int slot = 0)
        {
            SaveReport report = null;
            if (Save != null) report = Save.Delete(slot);
            var json = new UnityJsonSerializer();
            var paths = new PersistentDataPathProvider("crossroads");
            Init(json, paths, Content, SceneKey, CheckpointId, slot, loadExisting: false);
            EventBus.Publish(new StateResetEvent());
            StoryLog.Log("[CROSSROADS] Run reset (decisions cleared)");
            return report ?? SaveReport.Failure("", "save system not ready");
        }

        public static void Shutdown(bool silent = false)
        {
            if (Decisions != null) Decisions.Resolved -= OnDecisionResolved;
            EventBus.Unsubscribe<AreaUnlockedEvent>(OnAreaUnlocked);
            EventBus.Unsubscribe<AreaChangedEvent>(OnAreaChanged);
            Abilities = null;
            AppServices.Clear();
            InputLock.Clear();
            IsInitialized = false;
            if (!silent) StoryLog.Log("[CROSSROADS] services shut down");
        }
    }
}
