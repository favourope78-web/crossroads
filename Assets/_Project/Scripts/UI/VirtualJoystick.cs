using Crossroads.Gameplay.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Left virtual joystick (GAME_DESIGN §8.1): a fixed touch zone + floating knob.
    /// Drag anywhere in the zone; the knob tracks the finger clamped to the rim.
    /// Writes analog movement into the InputBus (deadzone-filtered there). Zero allocation
    /// per event - only vector math. Also usable with a mouse in the editor.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform _zone;
        private RectTransform _knob;
        private bool _dragging;
        private Vector2 _center;
        private float _rimPixels = 170f;

        public static VirtualJoystick Build(RectTransform parent, bool rightSide)
        {
            var go = new GameObject("VirtualJoystick");
            var zone = go.AddComponent<RectTransform>();
            zone.SetParent(parent, false);
            float w = 460f;
            zone.anchorMin = new Vector2(rightSide ? 1f : 0f, 0f);
            zone.anchorMax = new Vector2(rightSide ? 1f : 0f, 0f);
            zone.pivot = new Vector2(rightSide ? 1f : 0f, 0f);
            zone.offsetMin = new Vector2(rightSide ? -w : 0f, 0f);
            zone.offsetMax = new Vector2(rightSide ? 0f : w, 520f);

            var zoneImage = go.AddComponent<Image>();
            zoneImage.color = new Color(0.3f, 0.85f, 0.95f, 0.05f); // barely-there pad
            zoneImage.raycastTarget = true;

            var knobGo = new GameObject("Knob");
            var knob = knobGo.AddComponent<RectTransform>();
            knob.SetParent(zone, false);
            knob.sizeDelta = new Vector2(150f, 150f);
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.42f);
            knob.anchoredPosition = Vector2.zero;
            var knobImage = knobGo.AddComponent<Image>();
            knobImage.color = new Color(0.3f, 0.85f, 0.95f, 0.35f);

            var stick = go.AddComponent<VirtualJoystick>();
            stick._zone = zone;
            stick._knob = knob;
            return stick;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragging = true;
            _center = RectTransformUtility.WorldToScreenPoint(null, _zone.position)
                      - new Vector2(0f, _zone.rect.height * 0.08f); // rest point slightly low-center
            UpdateKnob(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging) UpdateKnob(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;
            _knob.anchoredPosition = Vector2.zero;
            InputBus.SetMovement(0f, 0f);
        }

        private void UpdateKnob(Vector2 screenPos)
        {
            Vector2 local = screenPos - _center;
            _rimPixels = Mathf.Max(_zone.rect.width * 0.36f, 90f);
            Vector2 clamped = Vector2.ClampMagnitude(local, _rimPixels);
            _knob.anchoredPosition = clamped;
            // normalized analog write: rim = 1.0 (InputBus applies the deadzone)
            InputBus.SetMovement(clamped.x / _rimPixels, clamped.y / _rimPixels);
        }

        /// <summary>Test seam + settings application: rim in normalized units is fixed, visuals scale with the rig.</summary>
        public RectTransform Zone { get { return _zone; } }
    }
}
