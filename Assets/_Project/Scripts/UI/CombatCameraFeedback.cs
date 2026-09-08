using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;

namespace Crossroads.UI
{
    /// <summary>
    /// Combat feel (VISUAL_TARGET §6): feeds the third-person rig a small decaying
    /// impulse when the player takes a meaningful hit or lands a heavy one - a nudge,
    /// never a full screen-shake (mobile comfort + readability of telegraphs).
    /// Presentation-only; combat logic is untouched.
    /// </summary>
    public class CombatCameraFeedback : MonoBehaviour
    {
        private Crossroads.Prototype.ThirdPersonCameraController _rig;

        private void OnEnable()
        {
            EventBus.Subscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Subscribe<CombatantDefeatedEvent>(OnDefeated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Unsubscribe<CombatantDefeatedEvent>(OnDefeated);
        }

        private void OnDamaged(CombatantDamagedEvent e)
        {
            if (e.amount <= 0f) return;
            float strength = e.isPlayer ? 0.16f : (e.amount >= 20f ? 0.09f : 0.04f);
            Rig().AddImpulse(strength);
        }

        private void OnDefeated(CombatantDefeatedEvent e)
        {
            Rig().AddImpulse(e.isPlayer ? 0.34f : 0.2f);
        }

        private Crossroads.Prototype.ThirdPersonCameraController Rig()
        {
            if (_rig == null)
            {
                var cam = Camera.main;
                if (cam != null) _rig = cam.GetComponent<Crossroads.Prototype.ThirdPersonCameraController>();
            }
            return _rig;
        }
    }
}
