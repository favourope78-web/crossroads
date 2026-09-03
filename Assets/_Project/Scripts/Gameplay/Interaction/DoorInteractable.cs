using System.Collections;
using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>Sliding door for the hall prototype (pivot-free, mobile-cheap).
    /// Extensible: GatedDoor subclasses it to gate area access behind state.</summary>
    public class DoorInteractable : Interactable
    {
        [SerializeField] protected float openOffset = 3.4f;
        [SerializeField] protected float slideTime = 0.9f;

        protected Vector3 _start;
        protected bool _open;
        protected bool _busy;

        private void Awake() { _start = transform.localPosition; }

        /// <summary>True when the door is (or is sliding to) open.</summary>
        public bool IsOpen { get { return _open; } }

        /// <summary>Animates (or instantly sets) the door to the given state.</summary>
        public void SetOpen(bool open, bool instant = false)
        {
            if (_open == open) return;
            _open = open;
            StartCoroutine(Slide(open ? 1f : 0f, instant));
            Debug.Log("[CROSSROADS] Door " + (open ? "opened" : "closed") + ": " + name);
        }

        public override void OnInteract(GameObject player)
        {
            if (_busy) return;
            SetOpen(!_open, false);
        }

        private IEnumerator Slide(float t, bool instant)
        {
            _busy = true;
            Vector3 fromPos = transform.localPosition;
            Vector3 toPos = _start + transform.right * (openOffset * t) * Mathf.Sign(transform.localScale.x);
            if (instant)
            {
                transform.localPosition = toPos;
                _busy = false;
                yield break;
            }
            float elapsed = 0f;
            while (elapsed < slideTime)
            {
                elapsed += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(fromPos, toPos, Mathf.SmoothStep(0f, 1f, elapsed / slideTime));
                yield return null;
            }
            transform.localPosition = toPos;
            _busy = false;
        }
    }
}
