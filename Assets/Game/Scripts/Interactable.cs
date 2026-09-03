using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>Base interactable for the environment prototype (inspect / use).
    /// Full interaction system arrives with the decision system (Phase 2+).</summary>
    public class Interactable : MonoBehaviour
    {
        [SerializeField] private string promptLabel = "Inspect";
        [SerializeField] protected float interactRadius = 2.5f;

        public string Label => promptLabel;
        public float Radius => interactRadius;

        public bool InRange(Vector3 playerPos) =>
            Vector3.Distance(playerPos, transform.position) <= interactRadius;

        public virtual void OnInteract(GameObject player)
        {
            Debug.Log($"[CROSSROADS] Inspect: {name}");
        }
    }
}
