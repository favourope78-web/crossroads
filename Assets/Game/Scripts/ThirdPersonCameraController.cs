using Crossroads.Gameplay.Input;
using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>
    /// Third-person ORBIT follow camera (mobile player experience upgrade of the prototype
    /// follow cam). Improvements over the fixed-offset version:
    ///   - smooth follow (position damp) + smooth rotation (look damp)          [kept]
    ///   - yaw/pitch orbit driven by InputBus look deltas (touch pad / mouse)   [new]
    ///   - collision avoidance: sphere-probe pull-in, speed-limited ease-out    [new]
    ///   - sensitivity + distance + smoothing from InputSettings (pause menu)   [new]
    ///   - indoor behaviour: low headroom lowers the height bias automatically  [new]
    /// All decisive math lives in CameraRigMath (pure, unit-tested). Two physics
    /// probes per frame, zero allocations.
    /// </summary>
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [SerializeField] private float lookHeight = 1.45f;   // pivot height above the player
        [SerializeField] private float probeRadius = 0.35f;  // camera collision sphere
        [SerializeField] private float touchDegreesPerPixel = 0.14f;
        [SerializeField] private float pitchDefault = 18f;

        private Transform _target;
        private Vector3 _posVelocity;
        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _headroom = 4f;

        private void Start()
        {
            _pitch = pitchDefault;
            _distance = InputSettingsStore.Current.cameraDistance;
            var p = FindFirstObjectByType<Crossroads.Gameplay.PlayerPrototypeController>();
            if (p != null) _target = p.transform;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                var p = FindFirstObjectByType<Crossroads.Gameplay.PlayerPrototypeController>();
                if (p != null) _target = p.transform; else return;
            }

            InputSettings s = InputSettingsStore.Current;

            // ---- rotation input: touch look pad (via bus) + desktop mouse fallback ----
            Vector2 look = InputBus.ConsumeLookDelta();
#if ENABLE_LEGACY_INPUT_MANAGER
            if (look.sqrMagnitude < 0.01f)
            {
                look = new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y"));
            }
#endif
            float degreesPerPixel = touchDegreesPerPixel * s.lookSensitivity;
            _yaw += look.x * degreesPerPixel;
            _pitch += (s.invertLookY ? look.y : -look.y) * degreesPerPixel;
            _pitch = CameraRigMath.ClampPitch(_pitch);

            // ---- collision probes (back cast + headroom cast) ----
            Vector3 pivot = _target.position + Vector3.up * lookHeight;
            float desired = s.cameraDistance;
            float clearance = 0f;
            Vector3 back = -ForwardOfYawPitch(_yaw, _pitch);
            RaycastHit hit;
            if (Physics.SphereCast(new Ray(pivot, back), probeRadius, out hit, desired + probeRadius))
            {
                clearance = Mathf.Max(hit.distance, 0f);
            }
            RaycastHit roof;
            if (Physics.SphereCast(new Ray(pivot, Vector3.up), probeRadius, out roof, 4f))
            {
                _headroom = roof.distance;
            }
            else
            {
                _headroom = 4f;
            }

            // ---- distance policy: pull in instantly, ease out smoothly (CameraRigMath) ----
            _distance = CameraRigMath.ResolveDistance(desired, clearance, _distance,
                Time.deltaTime, CameraRigMath.DefaultExtendSpeed);

            // ---- position + aim ----
            float heightBias = CameraRigMath.IndoorHeightBias(_headroom, 0.35f);
            Vector3 offset = CameraRigMath.OrbitOffset(_yaw, _pitch, _distance, heightBias);
            Vector3 desiredPos = pivot + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _posVelocity, s.cameraSmoothing);

            Vector3 toPivot = pivot - transform.position;
            if (toPivot.sqrMagnitude > 0.001f)
            {
                Quaternion wanted = Quaternion.LookRotation(toPivot.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - s.cameraSmoothing);
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
