using Crossroads.Gameplay.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Right-side look/control surface: any drag in the zone orbits the camera (yaw/pitch
    /// via InputBus look deltas, scaled by the player's sensitivity). Fully transparent,
    /// sits UNDER the action buttons in the hierarchy so buttons keep their touches.
    /// Multi-touch safe: each pointer drags independently; only the newest look finger wins.
    /// </summary>
    public class TouchLookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private bool _active;
        private Vector2 _last;

        public static TouchLookPad Build(RectTransform parent, bool leftSide)
        {
            var go = new GameObject("TouchLookPad");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            // covers the non-joystick half of the screen, full height, under everything else
            rect.anchorMin = new Vector2(leftSide ? 0f : 0.42f, 0f);
            rect.anchorMax = new Vector2(leftSide ? 0.58f : 1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // invisible, but raycastable
            img.raycastTarget = true;
            return go.AddComponent<TouchLookPad>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _active = true;
            _last = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_active) return;
            Vector2 delta = eventData.position - _last;
            _last = eventData.position;
            InputBus.AddLookDelta(delta.x, delta.y);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_active) return;
            _active = false;
        }
    }
}
