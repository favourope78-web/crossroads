using System;

namespace Crossroads.Core
{
    /// <summary>
    /// Lifecycle of one objective instance (persisted as int in ObjectiveProgressEntry).
    /// Hidden -> Available -> Active -> Completed | Failed | Cancelled.
    /// </summary>
    public enum ObjectivePhase
    {
        Hidden = 0,     // offer conditions not met yet (the player has not reached this path)
        Available = 1,  // offer conditions met, waiting to be tracked (autoActivate=false)
        Active = 2,     // tracked by the objective HUD
        Completed = 3,  // success - consequences applied, follow-ups released
        Failed = 4,     // failure conditions fired - failure consequences applied
        Cancelled = 5   // withdrawn by a later state (e.g. a path was abandoned)
    }

    // ------------------------------------------------------------------ objective events
    /// <summary>
    /// Fired whenever an objective's phase or progress changes (single write path:
    /// StateMutator). Display data is pulled from the ObjectiveManager/definitions;
    /// the payload stays identity + numbers.
    /// </summary>
    public struct ObjectiveChangedEvent
    {
        public string objectiveId;
        public ObjectivePhase phase;
        public ObjectivePhase previousPhase;
        public int progress;        // steps/counter units passed so far
        public int target;          // total units (steps + counter), 0 = no measurable progress
        public bool important;      // offered/completed/failed -> toast + save hook
    }

    // ------------------------------------------------------------------ world-state events
    /// <summary>Fired when an open area is re-sealed (sweep locks the annex back down).</summary>
    public struct AreaClosedEvent
    {
        public string areaId;
    }

    /// <summary>Fired when a closed area is re-opened.</summary>
    public struct AreaReopenedEvent
    {
        public string areaId;
    }

    /// <summary>
    /// Fired when an NPC is relocated by the world state (MoveNpc effect). Scene-side
    /// NpcRelocator applies the position; the location key is persisted in GameState.
    /// </summary>
    public struct NpcRelocatedEvent
    {
        public string npcId;
        public string locationKey;
    }

    /// <summary>
    /// Fired the first time a world interaction's conditions pass (data-driven unlock
    /// registry). Persisted in GameState so two players keep different unlock sets.
    /// </summary>
    public struct InteractionUnlockedEvent
    {
        public string unlockKey;
        public string label;
    }

    /// <summary>
    /// Fired when a generic var changes (objective counters, world counters). Objective
    /// progress reacts to this instead of polling every frame.
    /// </summary>
    public struct VarChangedEvent
    {
        public string key;
        public int value;
    }
}
