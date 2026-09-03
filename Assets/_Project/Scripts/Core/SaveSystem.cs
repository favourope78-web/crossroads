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
        public SaveData Load(int slot = 0)
        {
            if (_paths == null) return null;
            string path = _paths.Resolve(string.Format(SlotPrefix, slot));
            return LoadPath(path);
        }

        private SaveData LoadPath(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path, Encoding.UTF8);
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
                    // In-memory migration: v1 -> v2 (progression fields come up defaulted by the
                    // deserializer's field initializers). The upgraded version is stamped on next persist.
                    StoryLog.Log("[CROSSROADS] Save upgraded v" + data.schemaVersion + " -> v" + SaveData.CurrentSchemaVersion);
                    data.schemaVersion = SaveData.CurrentSchemaVersion;
                }
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
            try
            {
                if (_paths != null) System.IO.Directory.CreateDirectory(_paths.Directory);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Replace(tmp, path, null);          // atomic on same volume; throws if target missing
                return SaveReport.Success(path);
            }
            catch (Exception e)
            {
                // First save: target may not exist yet; fall back to plain move
                try
                {
                    File.Copy(path + ".tmp", path, true);
                    File.Delete(path + ".tmp");
                    return SaveReport.Success(path);
                }
                catch (Exception e2)
                {
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
                if (_paths != null)
                {
                    string auto = _paths.Resolve(AutosaveFileName);
                    if (File.Exists(auto)) File.Delete(auto);
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
