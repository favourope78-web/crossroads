using System;
using System.IO;
using System.Text;

namespace Crossroads.Core
{
    /// <summary>Resolves a file name to a full path (Unity impl = Application.persistentDataPath).</summary>
    public interface IPathProvider
    {
        string Directory { get; }
        string Resolve(string fileName);
    }

    /// <summary>JSON abstraction so SaveSystem is headless-testable (Unity impl = JsonUtility).</summary>
    public interface IJsonSerializer
    {
        string ToJson(object o, bool prettyPrint);
        T FromJson<T>(string json);
    }

    /// <summary>
    /// Slot-based JSON save system (GAME_DESIGN §12.1): atomic writes (.tmp -> replace),
    /// schemaVersion on the file, autosave mirror, mobile lifecycle hooks driven by the
    /// scene bootstrapper. Pure C# / System.IO only.
    /// </summary>
    public class SaveSystem
    {
        public const string SlotPrefix = "save_slot_{0}.json";
        public const string AutosaveFileName = "autosave.json";

        private readonly IJsonSerializer _json;
        private readonly IPathProvider _paths;
        private string _fileName;

        public SaveData Current { get; private set; }
        public string SavePath { get { return _paths != null ? _paths.Resolve(_fileName ?? AutosaveFileName) : ""; } }
        public bool HasSession { get { return Current != null; } }

        public SaveSystem(IJsonSerializer json, IPathProvider paths)
        {
            _json = json;
            _paths = paths;
        }

        /// <summary>Begin a save session bound to the live GameState (all decisions write through it).</summary>
        public void StartSession(string slotName, string sceneKey, string checkpointId, GameState state, int slot = 0)
        {
            _fileName = string.Format(SlotPrefix, slot);
            Current = new SaveData
            {
                schemaVersion = SaveData.CurrentSchemaVersion,
                meta = new SaveMeta { slotName = slotName, timestamp = DateTime.UtcNow.ToString("s") },
                scene = new SaveSceneLocation { sceneKey = sceneKey, checkpointId = checkpointId },
                gameState = state
            };
        }

        /// <summary>True when the given slot's file exists on disk.</summary>
        public bool Exists(int slot = 0)
        {
            if (_paths == null) return false;
            return File.Exists(_paths.Resolve(string.Format(SlotPrefix, slot)));
        }

        /// <summary>Loads a slot. Returns null when missing/corrupt (corrupt -> logged, caller decides).</summary>
        /// <summary>Which file the last successful Load came from ("slot", "backup", "autosave" or "").</summary>
        public string LastLoadSource { get; private set; }

        /// <summary>
        /// Load order (production hardening): slot file -> slot .bak (previous good write,
        /// rotated by Persist) -> autosave mirror. A torn/corrupt primary therefore costs the
        /// player at most one save step instead of the whole run. Each candidate is parsed and
        /// sanity-checked independently; failures are logged, never thrown.
        /// </summary>
        public SaveData Load(int slot = 0)
        {
            if (_paths == null) return null;
            LastLoadSource = "";
            string path = _paths.Resolve(string.Format(SlotPrefix, slot));
            SaveData data = LoadPath(path);
            if (data != null) { LastLoadSource = "slot"; return data; }

            bool primaryExists = File.Exists(path);
            data = LoadPath(path + BackupSuffix);
            if (data != null)
            {
                LastLoadSource = "backup";
                if (primaryExists) StoryLog.LogWarning("[CROSSROADS] Save slot " + slot + " unreadable - recovered from " + BackupSuffix);
                _fileName = Path.GetFileName(path); // keep persisting to the slot, not the backup
                return data;
            }
            data = LoadPath(_paths.Resolve(AutosaveFileName));
            if (data != null)
            {
                LastLoadSource = "autosave";
                if (primaryExists) StoryLog.LogWarning("[CROSSROADS] Save slot " + slot + " unreadable - recovered from autosave mirror");
                _fileName = Path.GetFileName(path);
                return data;
            }
            return null;
        }

        public const string BackupSuffix = ".bak";

        private SaveData LoadPath(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path, Encoding.UTF8);
                // torn write / zero-byte file (battery pull mid-write): reject before parsing
                if (json == null || json.Length < 8 || json.TrimStart()[0] != '{' || json.TrimEnd()[json.TrimEnd().Length - 1] != '}')
                {
                    StoryLog.LogWarning("[CROSSROADS] Save file is truncated or not JSON - ignoring " + path);
                    return null;
                }
                SaveData data = _json.FromJson<SaveData>(json);
                if (data == null || data.schemaVersion < 1 || data.schemaVersion > SaveData.CurrentSchemaVersion)
                {
                    // Future-proof: keep the file, refuse silently (SaveMigrator table arrives with schema v3+)
                    StoryLog.LogWarning("[CROSSROADS] Save schema v" + (data != null ? data.schemaVersion.ToString() : "?") +
                                                 " not supported (current " + SaveData.CurrentSchemaVersion + ") - ignoring " + path);
                    return null;
                }
                if (data.schemaVersion < SaveData.CurrentSchemaVersion)
                {
                    // In-memory migration table: v1 -> v2 -> v3. Every migration step:
                    //   - keeps all legacy fields untouched
                    //   - normalizes any collection the file may lack to an empty list
                    // The upgraded version is stamped on the next persist.
                    StoryLog.Log("[CROSSROADS] Save upgraded v" + data.schemaVersion + " -> v" + SaveData.CurrentSchemaVersion);
                    data.schemaVersion = SaveData.CurrentSchemaVersion;
                }
                Normalize(data);
                _fileName = Path.GetFileName(path);
                Current = data;
                return data;
            }
            catch (Exception e)
            {
                StoryLog.LogError("[CROSSROADS] Failed to load save " + path + ": " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Makes a loaded save structurally safe: any collection missing from the JSON
        /// becomes an empty list (schema v1/v2 files predate newer fields, and some
        /// serializers may not run field initializers). Never drops existing data.
        /// </summary>
        private static void Normalize(SaveData data)
        {
            if (data == null || data.gameState == null) return;
            GameState g = data.gameState;
            if (g.flags == null) g.flags = new System.Collections.Generic.List<StringEntry>();
            if (g.worldStates == null) g.worldStates = new System.Collections.Generic.List<StringEntry>();
            if (g.entities == null) g.entities = new System.Collections.Generic.List<StringBoolEntry>();
            if (g.vars == null) g.vars = new System.Collections.Generic.List<StringIntEntry>();
            if (g.bonds == null) g.bonds = new System.Collections.Generic.List<StringIntEntry>();
            if (g.reputation == null) g.reputation = new System.Collections.Generic.List<StringIntEntry>();
            if (g.abilities == null) g.abilities = new System.Collections.Generic.List<StringEntry>();
            if (g.blockedAbilities == null) g.blockedAbilities = new System.Collections.Generic.List<StringEntry>();
            if (g.abilityLevels == null) g.abilityLevels = new System.Collections.Generic.List<StringIntEntry>();
            if (g.items == null) g.items = new System.Collections.Generic.List<StringEntry>();
            if (g.skills == null) g.skills = new System.Collections.Generic.List<StringIntEntry>();
            if (g.unlockAreas == null) g.unlockAreas = new System.Collections.Generic.List<StringEntry>();
            if (g.decisions == null) g.decisions = new System.Collections.Generic.List<ResolvedDecisionEntry>();
            if (g.codex == null) g.codex = new System.Collections.Generic.List<string>();
            if (g.objectives == null) g.objectives = new System.Collections.Generic.List<ObjectiveProgressEntry>();
            if (g.npcLocations == null) g.npcLocations = new System.Collections.Generic.List<StringEntry>();
            if (g.interactionUnlocks == null) g.interactionUnlocks = new System.Collections.Generic.List<StringEntry>();
            if (g.closedAreas == null) g.closedAreas = new System.Collections.Generic.List<StringEntry>();
            if (g.campaignBeats == null) g.campaignBeats = new System.Collections.Generic.List<string>();
            if (g.campaignBranches == null) g.campaignBranches = new System.Collections.Generic.List<string>();
            if (g.campaignChapters == null) g.campaignChapters = new System.Collections.Generic.List<string>();
            if (g.campaignJournal == null) g.campaignJournal = new System.Collections.Generic.List<string>();
        }

        /// <summary>Writes Current atomically (.tmp -> replace) and mirrors an autosave copy.</summary>
        public SaveReport Persist(bool autosaveMirror = true)
        {
            if (_json == null || _paths == null || Current == null)
                return SaveReport.Failure(SavePath, "save system not ready");

            Current.meta.timestamp = DateTime.UtcNow.ToString("s");
            string json = _json.ToJson(Current, true);

            SaveReport report = WriteAtomic(SavePath, json);
            if (!report.ok) return report;

            if (autosaveMirror)
            {
                SaveReport mirror = WriteAtomic(_paths.Resolve(AutosaveFileName), json);
                report = report.ok ? mirror : report;
            }

            EventBus.Publish(new SaveCompletedEvent
            {
                ok = report.ok,
                path = report.path,
                decisionCount = Current.gameState != null ? Current.gameState.decisions.Count : 0,
                error = report.error
            });
            return report;
        }

        private SaveReport WriteAtomic(string path, string json)
        {
            string tmp = path + ".tmp";
            try
            {
                if (_paths != null) System.IO.Directory.CreateDirectory(_paths.Directory);
                File.WriteAllText(tmp, json, Encoding.UTF8);
                if (File.Exists(path))
                {
                    // atomic swap that also rotates the previous good file into .bak (same volume)
                    File.Replace(tmp, path, path + BackupSuffix, true);
                }
                else
                {
                    File.Move(tmp, path);              // first save: nothing to rotate
                }
                return SaveReport.Success(path);
            }
            catch (Exception e)
            {
                // Replace/Move unsupported on this volume (some Android external storage): copy path
                try
                {
                    if (File.Exists(path)) File.Copy(path, path + BackupSuffix, true);
                    File.Copy(tmp, path, true);
                    File.Delete(tmp);
                    return SaveReport.Success(path);
                }
                catch (Exception e2)
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
                    return SaveReport.Failure(path, e.Message + " / " + e2.Message);
                }
            }
        }

        /// <summary>Deletes the slot (and any autosave mirror).</summary>
        public SaveReport Delete(int slot = 0)
        {
            string path = _paths != null ? _paths.Resolve(string.Format(SlotPrefix, slot)) : "";
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + BackupSuffix)) File.Delete(path + BackupSuffix);
                if (_paths != null)
                {
                    string auto = _paths.Resolve(AutosaveFileName);
                    if (File.Exists(auto)) File.Delete(auto);
                    if (File.Exists(auto + BackupSuffix)) File.Delete(auto + BackupSuffix);
                }
                return SaveReport.Success(path);
            }
            catch (Exception e)
            {
                return SaveReport.Failure(path, e.Message);
            }
        }
    }
}
