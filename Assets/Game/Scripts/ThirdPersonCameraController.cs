using Crossroads.Core;
using Crossroads.Gameplay.Input;
using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>
    /// Third-person ORBIT follow camera (mobile player experience upgrade of the prototype
    /// follow cam). Improvements over the fixed-offset version:
    ///   - smooth follow (position damp) + smooth rotation (look damp)          [kept]
    ///   - yaw/pitch orbit driven by InputBus look deltas (touch pad / mouse)   [kept]
    ///   - collision avoidance: sphere-probe pull-in, speed-limited ease-out    [kept]
    ///   - sensitivity + distance + smoothing from InputSettings (pause menu)   [kept]
    ///   - indoor behaviour: low headroom lowers the height bias automatically  [kept]
    ///
    /// Polish pass (production):
    ///   - CINEMATIC framing: dialogue / decisions ease the rig into a closer, lower
    ///     over-the-shoulder shot with a slight side offset; combat pulls back and up a
    ///     touch so the telegraph arcs stay readable. Blends are time-based (no snaps).
    ///   - LOCATION ARRIVAL: the rig snaps behind the player's new facing the frame the
    ///     fader teleports her (no 4 m SmoothDamp swoop through walls after travel).
    ///   - PERF: the target is resolved once (tag lookup at 1 Hz until found - no
    ///     FindFirstObjectByType per frame); the headroom probe runs at 10 Hz (it changes
    ///     slowly), the back probe every frame. Zero allocations per frame.
    /// All decisive math lives in CameraRigMath (pure, unit-tested).
    /// </summary>
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [SerializeField] private float lookHeight = 1.45f;   // pivot height above the player
        [SerializeField] private float probeRadius = 0.35f;  // camera collision sphere
        [SerializeField] private float touchDegreesPerPixel = 0.14f;
        [SerializeField] private float pitchDefault = 18f;

        [Header("Cinematic framing (dialogue / decisions)")]
        [SerializeField] private float dialogueDistanceScale = 0.62f;
        [SerializeField] private float dialogueHeightOffset = 0.18f;
        [SerializeField] private float dialogueSideOffset = 0.55f;
        [SerializeField] private float dialogueBlendSeconds = 0.6f;

        [Header("Combat framing")]
        [SerializeField] private float combatDistanceScale = 1.12f;
        [SerializeField] private float combatPitchBias = 6f;

        private Transform _target;
        private Vector3 _posVelocity;
        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _headroom = 4f;
        private float _nextTargetSearch;
        private float _nextHeadroomProbe;
        private float _cinematic;      // 0 = gameplay framing, 1 = dialogue framing
        private float _combatBlend;    // 0 = calm, 1 = combat
        private bool _snapNextFrame;

        // headless-test seams (read by MobileExperienceTests through the stub)
        public float Cinematic { get { return _cinematic; } }
        public float CombatBlend { get { return _combatBlend; } }

        private void Start()
        {
            _pitch = pitchDefault;
            _distance = InputSettingsStore.Current.cameraDistance;
            ResolveTarget();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LocationArrivedEvent>(OnLocationArrived);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationArrivedEvent>(OnLocationArrived);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        private void OnLocationArrived(LocationArrivedEvent e) { _snapNextFrame = true; }
        private void OnStateReset(StateResetEvent e) { _snapNextFrame = true; }

        private void ResolveTarget()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) { _target = player.transform; return; }
            var p = FindFirstObjectByType<Crossroads.Gameplay.PlayerPrototypeController>();
            if (p != null) _target = p.transform;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                if (Time.time < _nextTargetSearch) return;
                _nextTargetSearch = Time.time + 1f;
                ResolveTarget();
                if (_target == null) return;
                _snapNextFrame = true;
            }

            InputSettings s = InputSettingsStore.Current;
            float dt = Time.deltaTime;

            // ---- framing blends: dialogue lock -> cinematic, live enemies -> combat ----
            float cineTarget = InputLock.Active ? 1f : 0f;
            float blendStep = dialogueBlendSeconds > 0.01f ? dt / dialogueBlendSeconds : 1f;
            _cinematic = Mathf.MoveTowards(_cinematic, cineTarget, blendStep);
            bool inCombat = CombatPresence.HasLiveEnemy(Crossroads.Gameplay.CombatDirector.LiveEnemies);
            _combatBlend = Mathf.MoveTowards(_combatBlend, inCombat ? 1f : 0f, dt * 1.5f);

            // ---- rotation input: touch look pad (via bus) + desktop mouse fallback ----
            Vector2 look = InputBus.ConsumeLookDelta();
#if ENABLE_LEGACY_INPUT_MANAGER
            if (look.sqrMagnitude < 0.01f)
            {
                look = new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y"));
            }
#endif
            if (_cinematic < 0.5f) // the player owns the orbit outside cinematics
            {
                float degreesPerPixel = touchDegreesPerPixel * s.lookSensitivity;
                _yaw += look.x * degreesPerPixel;
                _pitch += (s.invertLookY ? look.y : -look.y) * degreesPerPixel;
                _pitch = CameraRigMath.ClampPitch(_pitch);
            }

            // ---- location arrival / reset: settle behind the new facing instantly ----
            if (_snapNextFrame)
            {
                _snapNextFrame = false;
                _yaw = _target.eulerAngles.y;
                _pitch = pitchDefault;
                _posVelocity = Vector3.zero;
                _distance = s.cameraDistance;
            }

            // ---- collision probes (back cast every frame, headroom cast at 10 Hz) ----
            Vector3 pivot = _target.position + Vector3.up * (lookHeight + dialogueHeightOffset * _cinematic);
            float framedPitch = _pitch + combatPitchBias * _combatBlend - 6f * _cinematic;
            float desired = s.cameraDistance * Mathf.Lerp(1f, dialogueDistanceScale, _cinematic) * Mathf.Lerp(1f, combatDistanceScale, _combatBlend);
            float clearance = 0f;
            Vector3 back = -ForwardOfYawPitch(_yaw, framedPitch);
            RaycastHit hit;
            if (Physics.SphereCast(new Ray(pivot, back), probeRadius, out hit, desired + probeRadius))
            {
                clearance = Mathf.Max(hit.distance, 0f);
            }
            if (Time.time >= _nextHeadroomProbe)
            {
                _nextHeadroomProbe = Time.time + 0.1f;
                RaycastHit roof;
                _headroom = Physics.SphereCast(new Ray(pivot, Vector3.up), probeRadius, out roof, 4f) ? roof.distance : 4f;
            }

            // ---- distance policy: pull in instantly, ease out smoothly (CameraRigMath) ----
            _distance = CameraRigMath.ResolveDistance(desired, clearance, _distance, dt, CameraRigMath.DefaultExtendSpeed);

            // ---- position + aim ----
            float heightBias = CameraRigMath.IndoorHeightBias(_headroom, 0.35f);
            Vector3 offset = CameraRigMath.OrbitOffset(_yaw, framedPitch, _distance, heightBias);
            Vector3 desiredPos = pivot + offset;
            if (_cinematic > 0.001f)
            {
                // over-the-shoulder: slide sideways (camera-right) so the NPC reads on the other third
                Vector3 right = Vector3.Cross(Vector3.up, -back);
                desiredPos += right * (dialogueSideOffset * _cinematic);
            }
            float smoothing = Mathf.Lerp(s.cameraSmoothing, s.cameraSmoothing * 1.8f, _cinematic);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _posVelocity, smoothing);

            Vector3 toPivot = pivot - transform.position;
            if (toPivot.sqrMagnitude > 0.001f)
            {
                Quaternion wanted = Quaternion.LookRotation(toPivot.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - smoothing);
            }
        }

        private static Vector3 ForwardOfYawPitch(float yaw, float pitch)
        {
            float y = yaw * Mathf.Deg2Rad;
            float p = CameraRigMath.ClampPitch(pitch) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p));
        }
    }
}
