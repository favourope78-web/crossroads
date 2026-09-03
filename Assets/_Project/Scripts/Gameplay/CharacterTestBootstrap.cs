using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Test-scene helper: instantiates the Ari prefab at the origin when the
    /// CharacterTest scene is played in the Editor. If the prefab does not
    /// exist yet, points the user at the one-click setup menu.
    /// </summary>
    public class CharacterTestBootstrap : MonoBehaviour
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Player/Ari.prefab";

        private void Awake()
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[CROSSROADS] Ari prefab not found. Run menu: CROSSROADS > Prototype > Build Ari Prefab & Test Scene.");
                return;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "Ari";
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Debug.Log("[CROSSROADS] Ari prototype spawned for test play.");
#else
            Debug.LogWarning("[CROSSROADS] CharacterTestBootstrap is editor-only; use the built spawn flow in play builds.");
#endif
        }
    }
}
