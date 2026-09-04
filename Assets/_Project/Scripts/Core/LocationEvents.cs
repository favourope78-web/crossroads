namespace Crossroads.Core
{
    // =====================================================================================
    // LOCATION events (world expansion, Gameplay/Locations). Like the campaign layer, the
    // location runtime owns NO state: unlock/unlocked-ness lives in GameState.unlockAreas,
    // the current location in GameState.currentArea, visits in flags, world changes in the
    // existing world-state stores. These events narrate what the derivation concluded so
    // UI/scene/audio can react (fade, move the player, apply the environment profile).
    // =====================================================================================

    /// <summary>A location's unlock rules passed (once); the area unlock itself persists.</summary>
    public struct LocationUnlockedEvent
    {
        public string locationId;
        public string name;
        public string notice;       // the passing rule's text (why it opened)
    }

    /// <summary>Published before state moves - the player is leaving this location.</summary>
    public struct LocationDepartedEvent
    {
        public string locationId;
    }

    /// <summary>Travel completed: state is already at the target; scene/UI may fade + apply env.</summary>
    public struct LocationArrivedEvent
    {
        public string locationId;
        public string name;
        public string sceneKey;
        public string checkpointId;
        public bool firstVisit;

        // environment profile (content data carried through the event so the scene never
        // needs a second copy of it)
        public string envProfile;
        public string envAmbient;
        public string envFog;
        public float envFogDensity;
        public string envSun;
        public float envSunIntensity;
    }

    /// <summary>Coarse availability signal: unlocks/connections/current changed (map refresh).</summary>
    public struct LocationAvailabilityChangedEvent
    {
        public string currentLocationId;
    }
}
