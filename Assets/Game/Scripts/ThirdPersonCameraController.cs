using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>Smooth third-person follow camera (prototype).
    /// Replaced by Cinemachine rig in Phase 1 per GAME_DESIGN §13.4.</summary>
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.1f, -4.4f);
        [SerializeField] private float positionDamping = 0.18f;
        [SerializeField] private float lookDamping = 0.12f;
        [SerializeField] private float lookHeight = 1.45f;

        private Transform _target;
        private Vector3 _velocity;
        private Vector3 _lookVel;

        private void Start()
        {
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
            Vector3 desired = _target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, positionDamping);
            Vector3 lookTarget = _target.position + Vector3.up * lookHeight;
            Vector3 current = transform.forward;
            Vector3 wanted = (lookTarget - transform.position).normalized;
            Vector3 dir = Vector3.SmoothDamp(current, wanted, ref _lookVel, lookDamping);
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
