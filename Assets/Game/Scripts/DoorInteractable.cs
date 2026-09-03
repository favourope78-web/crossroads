using System.Collections;
using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>Sliding door for the hall prototype (pivot-free, mobile-cheap).</summary>
    public class DoorInteractable : Interactable
    {
        [SerializeField] private float openOffset = 3.4f;
        [SerializeField] private float slideTime = 0.9f;

        private Vector3 _start;
        private bool _open;
        private bool _busy;

        private void Awake() { _start = transform.localPosition; }

        public override void OnInteract(GameObject player)
        {
            if (_busy) return;
            _open = !_open;
            StartCoroutine(Slide(_open ? 1f : 0f));
            Debug.Log($"[CROSSROADS] Door {(_open ? "opened" : "closed")}: {name}");
        }

        private IEnumerator Slide(float t)
        {
            _busy = true;
            float elapsed = 0f;
            float from = _open ? 0f : 1f; // current normalized state before flip handled by t
            float startT = Mathf.InverseLerp(0f, 1f, (transform.localPosition.x - _start.x) / Mathf.Max(0.001f, openOffset) * Mathf.Sign(transform.localScale.x));
            // simpler: lerp from current local pos to target
            Vector3 fromPos = transform.localPosition;
            Vector3 toPos = _start + transform.right * (openOffset * t) * Mathf.Sign(transform.localScale.x);
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
