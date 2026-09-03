using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Scene bootstrapper for story mode (FirstLocation):
    ///   1. Init GameServices (state, decisions, saves, encounters) with the authored
    ///      StoryContentLibrary asset (falls back to the code-built content).
    ///   2. Load the previous save -> decisions/flags/affinities/world state restored
    ///      (persistence proof: stop Play, Play again - the choice is still there).
    ///   3. Ensure the player carries PlayerInteraction.
    ///   4. Autosave on mobile lifecycle events (app pause/focus, design §12.3).
    /// Editor note: FirstLocationBootstrap spawns Ari in the Editor; this component also
    /// runs in device builds and wires the player if the scene provides one.
    /// </summary>
    public class StoryModeBootstrap : MonoBehaviour
    {
        [Tooltip("Authored content asset (Assets/_Project/Data/Decisions). Leave null to use the code-built fallback.")]
        [SerializeField] private StoryContentLibraryAsset contentLibrary;

        [SerializeField] private int saveSlot = 0;
        [SerializeField] private string sceneKey = "FirstLocation";
        [SerializeField] private string checkpointId = "hall_spawn";

        [Header("Dev helpers")]
        [Tooltip("Wipes the save file every time Play is pressed (for repeating the encounter).")]
        [SerializeField] private bool devClearSaveOnStart = false;

        private void Awake()
        {
            StoryLog.Info = Debug.Log;
            StoryLog.Warn = Debug.LogWarning;
            StoryLog.Error = Debug.LogError;

            IEncounterSource content = contentLibrary != null
                ? (IEncounterSource)contentLibrary
                : new RuntimeContentSource();

            if (devClearSaveOnStart)
            {
                var probe = new SaveSystem(new UnityJsonSerializer(), new PersistentDataPathProvider("crossroads"));
                probe.Delete(saveSlot);
                Debug.Log("[CROSSROADS] devClearSaveOnStart: save wiped");
            }

            GameServices.Init(new UnityJsonSerializer(), new PersistentDataPathProvider("crossroads"),
                content, sceneKey, checkpointId, saveSlot, loadExisting: !devClearSaveOnStart);

            // Power system clock: cooldowns run on real time (test rigs inject their own clock).
            if (GameServices.Abilities != null) GameServices.Abilities.Now = () => Time.time;
        }

        private void Start()
        {
            EnsurePlayerInteraction();
        }

        private void EnsurePlayerInteraction()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                var controller = FindFirstObjectByType<Crossroads.Gameplay.PlayerPrototypeController>();
                if (controller != null) player = controller.gameObject;
            }
            if (player == null)
            {
                Debug.LogWarning("[CROSSROADS] No Player found - run the CROSSROADS > Prototype > Build Ari Prefab menu once, then Play.");
                return;
            }
            if (player.GetComponent<PlayerInteraction>() == null)
                player.AddComponent<PlayerInteraction>();
            // Power feedback: world-side effect (pulse burst) listens to AbilityUsedEvent.
            if (player.GetComponent<AbilityPulseVFX>() == null)
                player.AddComponent<AbilityPulseVFX>();
            Debug.Log("[CROSSROADS] PlayerInteraction ready on " + player.name);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) GameServices.PersistNow(autosaveMirror: true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) GameServices.PersistNow(autosaveMirror: true);
        }

        private void OnDestroy()
        {
            GameServices.Shutdown();
        }
    }
}
