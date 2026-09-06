using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crossroads.Prototype
{
    /// <summary>Spawns the Ari prefab at the hall spawn point when FirstLocation
    /// is played in the Editor, tags it Player and wires prototype interaction.
    /// If the prefab is missing, points at the one-click build menu.</summary>
    public class FirstLocationBootstrap : MonoBehaviour
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Player/Ari.prefab";
        [SerializeField] private Vector3 spawnPoint = new Vector3(0f, 0f, -16f);

        private void Awake()
        {
            // Polish pass: the generated scene now carries Ari as a PrefabInstance of Ari.fbx
            // (device builds included). Only fall back to the editor prefab when she is absent.
            if (GameObject.FindGameObjectWithTag("Player") != null) return;
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[CROSSROADS] Ari prefab missing - run menu: CROSSROADS > Prototype > Build Ari Prefab & Test Scene, then Play again.");
                return;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "Ari";
            inst.tag = "Player";
            inst.transform.SetPositionAndRotation(spawnPoint, Quaternion.identity);
            if (inst.GetComponent<Crossroads.Gameplay.PlayerInteraction>() == null)
                inst.AddComponent<Crossroads.Gameplay.PlayerInteraction>();
            Debug.Log("[CROSSROADS] Ari spawned in FirstLocation at " + spawnPoint);
#else
            Debug.LogWarning("[CROSSROADS] FirstLocation bootstrap is editor-only.");
#endif
        }
    }
}
