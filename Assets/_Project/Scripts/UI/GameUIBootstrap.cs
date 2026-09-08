using UnityEngine;
using Crossroads.Core;
using Crossroads.Gameplay;
using Crossroads.Narrative;

namespace Crossroads.UI
{
    /// <summary>
    /// Scene component that builds the ENTIRE presentation layer at runtime (canvas,
    /// HUD, menus, touch rig) - no UI assets/prefabs, mobile-first (VISUAL_TARGET §4).
    ///
    /// Visual transformation pass layout (top of the stack = last):
    ///   fader / transition             (always top)
    ///   main menu + settings + map     (full-screen, modal)
    ///   pause menu
    ///   gameplay HUD root              (clean HUD - what a player sees)
    ///   touch controls rig             (under the HUD panels)
    ///   DEV overlays                   (StateHUD / full objective list / raw map - gated)
    ///
    /// The gameplay HUD root hides behind the main menu, so the first frame of the game
    /// is a front door, not a cockpit of diagnostic panels.
    /// </summary>
    public class GameUIBootstrap : MonoBehaviour
    {
        private MobileControlsUI _controls;
        private PauseMenuUI _pause;
        private MainMenuUI _mainMenu;
        private SettingsPanelUI _settings;
        private WorldMapUI _worldMap;
        private RectTransform _hudRoot;
        private StateHUD _state;          // dev-only
        private ObjectiveHUD _objectives; // dev-only (full list)
        private MapHUD _map;              // dev-only (raw location list)
        private DevOverlayToggle _devToggle;
        private LoadingScreenUI _loading;

        private void Awake()
        {
            var canvas = RuntimeMenuFactory.CreateRoot("GameUI");
            var safe = RuntimeMenuFactory.CreateSafeArea("SafeArea", canvas.transform);

            // settings first (camera/rig read them during their own Start)
            Crossroads.Gameplay.Input.InputSettingsStore.Load();

            // ---- touch rig builds UNDER everything ----
            _controls = MobileControlsUI.Attach(safe);

            // ---- gameplay HUD (clean, player-facing) ----
            var hudGo = new GameObject("GameplayHUD");
            _hudRoot = hudGo.AddComponent<RectTransform>();
            _hudRoot.SetParent(safe, false);
            _hudRoot.anchorMin = Vector2.zero;
            _hudRoot.anchorMax = Vector2.one;
            _hudRoot.offsetMin = Vector2.zero;
            _hudRoot.offsetMax = Vector2.zero;

            InteractionHUD.Attach(_hudRoot);
            DialogueUI.Attach(_hudRoot);
            ToastUI.Attach(_hudRoot);
            PlayerHUD.Attach(_hudRoot);
            CombatHUD.Attach(_hudRoot);
            CampaignHUD.Attach(_hudRoot);
            ObjectiveTrackerHUD.Attach(_hudRoot);
            MiniMapHUD.Attach(_hudRoot);
            AbilityHUD.Attach(_hudRoot);
            AbilityHotbarHUD.Attach(_hudRoot);
            DamageNumberUI.Attach(_hudRoot);
            hudGo.AddComponent<CombatCameraFeedback>();

            // ---- dev overlays (diagnostic surfaces live behind the gate) ----
            _devToggle = safe.gameObject.AddComponent<DevOverlayToggle>();
            if (UIDebugGate.OverlaysVisible)
            {
                _state = StateHUD.Attach(safe);
                _objectives = ObjectiveHUD.Attach(safe);
                _map = MapHUD.Attach(safe);
            }
            else
            {
                // built hidden so the runtime gate toggle can bring them up without rebuilding
                _state = StateHUD.Attach(safe);
                _objectives = ObjectiveHUD.Attach(safe);
                _map = MapHUD.Attach(safe);
                _state.SetVisible(false);
                _objectives.SetVisible(false);
                _map.SetVisible(false);
            }
            UIDebugGate.OverlaysChanged += OnDevOverlaysChanged;

            // ---- menus (over the HUD) ----
            _pause = PauseMenuUI.Attach(safe);
            _worldMap = WorldMapUI.Attach(safe);
            _settings = SettingsPanelUI.Attach(safe);
            _mainMenu = MainMenuUI.Attach(safe);

            // ---- chapter title cards (non-blocking, over the HUD under the menus) ----
            ChapterTransitionUI.Attach(safe);

            // ---- fader / loading on top ----
            LocationTransitionFader.Attach(safe);

            // ---- boot loading screen (the front door; dismisses once services + menu are up) ----
            _loading = LoadingScreenUI.Attach(safe);

            // main menu owns the screen while open: HUD + controls recede
            MainMenuUI.MenuShown += OnMenuShown;

            Debug.Log("[CROSSROADS] Game UI ready (main menu + clean HUD: player/map/tracker/combat/abilities + touch rig + pause/map/settings + transitions)");
        }

        private void Start()
        {
            // Events published before this UI subscribed (service boot) are replayed by snapshot
            if (_state != null) _state.RefreshFromState();
            if (_objectives != null) _objectives.Refresh();

            // save presence for the main menu CONTINUE state (boot order safety: the
            // StateLoadedEvent fires during StoryModeBootstrap.Awake, before this Start)
            bool hadSave = false;
            if (GameServices.IsInitialized && GameServices.Save != null)
                hadSave = GameServices.Save.Exists();
            _mainMenu.SetSavePresence(hadSave);
            _mainMenu.Open();

            // everything the loading screen covers is now up - let it finish
            _loading.MarkServicesReady();
        }

        private void OnMenuShown(bool open)
        {
            if (_hudRoot != null) _hudRoot.gameObject.SetActive(!open);
            if (_controls != null) _controls.SetVisible(!open);
        }

        private void OnDevOverlaysChanged(bool visible)
        {
            if (_state != null) _state.SetVisible(visible);
            if (_objectives != null) _objectives.SetVisible(visible);
            if (_map != null) _map.SetVisible(visible);
        }

        /// <summary>Map button beside the mini-map opens the travel screen.</summary>
        public void ToggleWorldMap()
        {
            if (_worldMap != null) _worldMap.Toggle();
        }

        private void OnDestroy()
        {
            UIDebugGate.OverlaysChanged -= OnDevOverlaysChanged;
            MainMenuUI.MenuShown -= OnMenuShown;
        }
    }
}
