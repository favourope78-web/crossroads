using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Prototype-only locomotion controller for Ari (idle / walk / turning).
    /// Per FIRST_ASSET_BRIEFS scope: NO combat, powers, quests or decisions here.
    /// Replaced by PlayerMotor + PlayerCombatFSM in DEVELOPMENT_PLAN Phase 1.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class PlayerPrototypeController : MonoBehaviour
    {
        [Header("Locomotion")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float turnSmoothTime = 0.12f;
        [SerializeField] private float pivotTurnSpeed = 240f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float pivotAngleThreshold = 50f;

        private CharacterController _cc;
        private Animator _animator;
        private float _turnVel;
        private Vector3 _velocity;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int TurningHash = Animator.StringToHash("Turning");

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            // Dialogue / decision lock (GAME_DESIGN §4.5): the world keeps running, Ari doesn't move.
            if (Crossroads.Core.InputLock.Active)
            {
                _velocity.y += gravity * Time.deltaTime;
                _velocity.y = Mathf.Max(_velocity.y, -20f);
                _cc.Move(new Vector3(0f, _velocity.y * Time.deltaTime, 0f));
                _animator.SetFloat(SpeedHash, 0f, 0.15f, Time.deltaTime);
                _animator.SetBool(TurningHash, false);
                return;
            }

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            Vector3 input = new Vector3(h, 0f, v);
            Vector3 moveDir = input.sqrMagnitude > 0.001f ? input.normalized : Vector3.zero;

            // camera-relative prototype: camera looks down +Z in test scene, so use world axes
            bool moving = moveDir.sqrMagnitude > 0.001f;

            if (moving)
            {
                float targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnVel, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
                _cc.Move(moveDir * walkSpeed * Time.deltaTime);
            }

            // gravity
            _velocity.y += gravity * Time.deltaTime;
            _velocity.y = Mathf.Max(_velocity.y, -20f);
            _cc.Move(new Vector3(0f, _velocity.y * Time.deltaTime, 0f));

            // pivot turn in place (Turn state) when input opposes facing while "stationary"
            float facing = transform.eulerAngles.y;
            float desired = moving ? Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg : facing;
            float delta = Mathf.DeltaAngle(facing, desired);
            bool pivoting = false; // pivot handled by smooth turn while moving; Turn clip used for sharp reversals
            if (moving && Mathf.Abs(delta) > pivotAngleThreshold)
            {
                pivoting = true;
                transform.Rotate(0f, Mathf.Sign(delta) * pivotTurnSpeed * Time.deltaTime, 0f);
            }

            _animator.SetFloat(SpeedHash, moving ? 1f : 0f, 0.15f, Time.deltaTime);
            _animator.SetBool(TurningHash, pivoting);
        }
    }
}
