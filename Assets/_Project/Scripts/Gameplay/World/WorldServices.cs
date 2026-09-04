using System;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Owns the world-state + objective runtimes and wires them into the service
    /// lifecycle. Lives in the Gameplay assembly (the systems depend on the Narrative
    /// condition/effect whitelists, and GameServices - Narrative - must not depend on
    /// Gameplay, so this facade sits one layer up instead of inside GameServices).
    ///
    /// Lifecycle (mirrors GameServices, nothing there changed):
    ///   - StoryModeBootstrap.Awake: GameServices.Init(...) then WorldServices.Init()
    ///   - WorldServices.Init reads the SAME live StateMutator/GameStateManager/content,
    ///     binds the event-driven managers, syncs interaction unlocks and evaluates
    ///     objective offers against the loaded save (completed missions stay completed)
    ///   - important changes autosave immediately (§12.3), like decisions do
    ///   - GameServices.ResetRun publishes StateResetEvent -> this facade re-inits
    ///     against the fresh state automatically
    /// Pure C# - the headless tests boot it exactly like the scene does.
    /// </summary>
    public static class WorldServices
    {
        public static bool IsInitialized { get; private set; }

        /// <summary>World-state runtime: areas, objects, NPC locations, interaction unlocks.</summary>
        public static WorldStateSystem World { get; private set; }

        /// <summary>Objective/mission runtime: offers, progress, completion, failure, follow-ups.</summary>
        public static ObjectiveManager Objectives { get; private set; }

        private static bool _autosaveHooked;

        /// <summary>Boots the world/objective systems over the initialized GameServices.</summary>
        public static void Init()
        {
            Shutdown(silent: true);
            if (!GameServices.IsInitialized || GameServices.Content == null)
            {
                StoryLog.LogWarning("[CROSSROADS] WorldServices.Init: GameServices not initialized");
                return;
            }

            StoryContentData content = GameServices.Content.Content;
            World = new WorldStateSystem(GameServices.State, GameServices.Progress,
                content != null ? content.worldInteractions : null);
            Objectives = new ObjectiveManager(content != null ? content.objectives : null, GameServices.State);

            World.BindEvents();
            Objectives.BindEvents();

            if (!_autosaveHooked)
            {
                _autosaveHooked = true;
                EventBus.Subscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
                EventBus.Subscribe<NpcRelocatedEvent>(OnWorldStateChanged);
                EventBus.Subscribe<InteractionUnlockedEvent>(OnWorldStateChanged);
                EventBus.Subscribe<AreaClosedEvent>(OnWorldStateChanged);
                EventBus.Subscribe<AreaReopenedEvent>(OnWorldStateChanged);
                EventBus.Subscribe<EntityStateChangedEvent>(OnWorldStateChanged); // consumed/hidden world objects
                EventBus.Subscribe<ItemChangedEvent>(OnWorldStateChanged);        // pickups outside decisions
                EventBus.Subscribe<StateResetEvent>(OnStateReset);
            }

            // post-load pass: unlock registry from the restored state, then objective offers
            World.SyncInteractionUnlocks();
            Objectives.Evaluate();

            IsInitialized = true;
            StoryLog.Log("[CROSSROADS] World services ready ("
                + (Objectives != null ? Objectives.RegisteredCount : 0) + " objectives, "
                + World.Describe() + ")");
        }

        /// <summary>Autosave hook: objective/world changes are run-shaping (§12.3).</summary>
        private static void OnObjectiveChanged(ObjectiveChangedEvent e) { GameServices.PersistNow(autosaveMirror: true); }
        private static void OnWorldStateChanged<T>(T e) { GameServices.PersistNow(autosaveMirror: true); }

        /// <summary>GameServices.ResetRun wipes the slot and re-inits: follow with fresh systems.</summary>
        private static void OnStateReset(StateResetEvent e) { Init(); }

        public static void Shutdown(bool silent = false)
        {
            if (World != null) World.UnbindEvents();
            if (Objectives != null) Objectives.UnbindEvents();
            if (_autosaveHooked)
            {
                _autosaveHooked = false;
                EventBus.Unsubscribe<ObjectiveChangedEvent>(OnObjectiveChanged);
                EventBus.Unsubscribe<NpcRelocatedEvent>(OnWorldStateChanged);
                EventBus.Unsubscribe<InteractionUnlockedEvent>(OnWorldStateChanged);
                EventBus.Unsubscribe<AreaClosedEvent>(OnWorldStateChanged);
                EventBus.Unsubscribe<AreaReopenedEvent>(OnWorldStateChanged);
                EventBus.Unsubscribe<EntityStateChangedEvent>(OnWorldStateChanged);
                EventBus.Unsubscribe<ItemChangedEvent>(OnWorldStateChanged);
                EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
            }
            World = null;
            Objectives = null;
            IsInitialized = false;
            if (!silent) StoryLog.Log("[CROSSROADS] world services shut down");
        }
    }
}
