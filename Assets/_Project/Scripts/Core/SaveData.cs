using System;

namespace Crossroads.Core
{
    /// <summary>Save-file payload, mirrors GAME_DESIGN §12.2 (schema-versioned JSON).
    /// v2 adds the progression attributes (reputation/abilities/items/skills/area unlocks);
    /// v3 adds the power system (ability levels + blocked abilities);
    /// v4 adds the world/mission state (objective phases, NPC locations, interaction
    /// unlocks, re-sealed areas). Older files are upgraded in memory on load (missing
    /// collections are normalized to empty).</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentSchemaVersion = 5; // v5: + campaign beats/branches/chapters/journal

        public int schemaVersion = CurrentSchemaVersion;
        public SaveMeta meta = new SaveMeta();
        public SaveSceneLocation scene = new SaveSceneLocation();
        public GameState gameState = new GameState();
    }

    [Serializable]
    public class SaveMeta
    {
        public string slotName = "";
        public string timestamp = "";
        public int playtimeSec;
    }

    [Serializable]
    public class SaveSceneLocation
    {
        public string sceneKey = "";
        public string checkpointId = "";
    }

    /// <summary>Result of a save/delete operation (UI toast + debug log).</summary>
    public class SaveReport
    {
        public bool ok;
        public string path = "";
        public string error = "";

        public static SaveReport Success(string path) { return new SaveReport { ok = true, path = path }; }
        public static SaveReport Failure(string path, string error) { return new SaveReport { ok = false, path = path, error = error }; }
    }
}
