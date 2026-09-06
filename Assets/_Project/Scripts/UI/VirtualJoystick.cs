using Crossroads.Gameplay.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Left-thumb movement stick (mobile). Polish pass:
    ///   - FLOATING ORIGIN: the stick centres where the thumb lands inside the zone, so the
    ///     first millimetre of travel already moves Ari (no reach to a fixed knob).
    ///   - POINTER OWNERSHIP: the stick tracks the pointerId that pressed it; a second finger
    ///     on the look pad / buttons can never steal or reset the movement input.
    ///   - PRESS FEEDBACK: knob brightens while held; a faint ring marks the rim.
    ///   - RELEASE: movement clears the same frame (no drift), knob glides back.
    /// The analog value is written normalised (rim = 1); InputBus applies the deadzone.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform _zone;
        private RectTransform _knob;
        private RectTransform _ring;
        private Image _knobImage;
        private Image _ringImage;
        private bool _dragging;
        private int _pointerId = int.MinValue;
        private Vector2 _center;          // screen-space floating origin
        private Vector2 _restAnchored;    // knob rest position (anchored)
        private float _rimPixels = 170f;

        private static readonly Color KnobIdle = new Color(0.3f, 0.85f, 0.95f, 0.35f);
        private static readonly Color KnobHeld = new Color(0.3f, 0.85f, 0.95f, 0.75f);
        private static readonly Color RingIdle = new Color(0.3f, 0.85f, 0.95f, 0.0f);
        private static readonly Color RingHeld = new Color(0.3f, 0.85f, 0.95f, 0.18f);

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

            var ringGo = new GameObject("Ring");
            var ring = ringGo.AddComponent<RectTransform>();
            ring.SetParent(zone, false);
            ring.sizeDelta = new Vector2(340f, 340f);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.42f);
            ring.anchoredPosition = Vector2.zero;
            var ringImage = ringGo.AddComponent<Image>();
            ringImage.color = RingIdle;
            ringImage.raycastTarget = false;

            var knobGo = new GameObject("Knob");
            var knob = knobGo.AddComponent<RectTransform>();
            knob.SetParent(zone, false);
            knob.sizeDelta = new Vector2(150f, 150f);
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.42f);
            knob.anchoredPosition = Vector2.zero;
            var knobImage = knobGo.AddComponent<Image>();
            knobImage.color = KnobIdle;
            knobImage.raycastTarget = false;

            var stick = go.AddComponent<VirtualJoystick>();
            stick._zone = zone;
            stick._knob = knob;
            stick._ring = ring;
            stick._knobImage = knobImage;
            stick._ringImage = ringImage;
            stick._restAnchored = Vector2.zero;
            return stick;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_dragging) return;                    // one finger owns the stick
            _dragging = true;
            _pointerId = eventData.pointerId;
            // floating origin: centre on the touch itself (falls back to the zone rest point)
            _center = eventData.position;
            Vector2 rest = RectTransformUtility.WorldToScreenPoint(null, _zone.position)
                           - new Vector2(0f, _zone.rect.height * 0.08f);
            Vector2 anchoredShift = _center - rest;
            _rimPixels = Mathf.Max(_zone.rect.width * 0.36f, 90f);
            // keep the rim fully inside the zone so the visual never clips
            anchoredShift = Vector2.ClampMagnitude(anchoredShift, Mathf.Max(_zone.rect.width * 0.5f - _rimPixels * 0.6f, 0f));
            _center = rest + anchoredShift;
            _ring.anchoredPosition = anchoredShift;
            _knob.anchoredPosition = anchoredShift;
            _restAnchored = anchoredShift;
            if (_knobImage != null) _knobImage.color = KnobHeld;
            if (_ringImage != null) _ringImage.color = RingHeld;
            UpdateKnob(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging && eventData.pointerId == _pointerId) UpdateKnob(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_dragging || eventData.pointerId != _pointerId) return;
            _dragging = false;
            _pointerId = int.MinValue;
            _knob.anchoredPosition = Vector2.zero;
            _ring.anchoredPosition = Vector2.zero;
            _restAnchored = Vector2.zero;
            if (_knobImage != null) _knobImage.color = KnobIdle;
            if (_ringImage != null) _ringImage.color = RingIdle;
            InputBus.SetMovement(0f, 0f);
        }

        private void UpdateKnob(Vector2 screenPos)
        {
            Vector2 local = screenPos - _center;
            Vector2 clamped = Vector2.ClampMagnitude(local, _rimPixels);
            _knob.anchoredPosition = _restAnchored + clamped;
            // normalized analog write: rim = 1.0 (InputBus applies the deadzone)
            InputBus.SetMovement(clamped.x / _rimPixels, clamped.y / _rimPixels);
        }

        public RectTransform Zone { get { return _zone; } }
        public bool Held { get { return _dragging; } }
    }
}
