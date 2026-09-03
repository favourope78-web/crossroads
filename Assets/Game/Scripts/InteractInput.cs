using System.Collections.Generic;
using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>Prototype proximity interaction: nearest Interactable + E key / tap.
    /// IMGUI prompt keeps the prototype UI-free (no Canvas yet).</summary>
    [RequireComponent(typeof(CharacterController))]
    public class InteractInput : MonoBehaviour
    {
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        private Interactable[] _all;
        private Interactable _current;

        private void Start() { _all = FindObjectsByType<Interactable>(FindObjectsSortMode.None); }

        private void Update()
        {
            _current = null;
            float best = float.MaxValue;
            for (int i = 0; i < _all.Length; i++)
            {
                float d = Vector3.Distance(transform.position, _all[i].transform.position);
                if (d <= _all[i].Radius && d < best) { best = d; _current = _all[i]; }
            }
            bool pressed = Input.GetKeyDown(interactKey) || Input.GetMouseButtonDown(0);
            if (_current != null && pressed) _current.OnInteract(gameObject);
        }

        private void OnGUI()
        {
            if (_current != null)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.72f, 180f, 30f),
                          $"[E] {_current.Label}: {_current.name}");
            }
        }
    }
}
