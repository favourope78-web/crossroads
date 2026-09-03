using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Crossroads.EditorTools
{
    /// <summary>
    /// One-click prototype assembly: builds the Ari prefab from the imported
    /// FBX (Animator + controller, CharacterController, prototype locomotion,
    /// material reassignment) and drops an instance into the CharacterTest scene.
    /// Run once after first FBX import: menu CROSSROADS > Prototype > Build Ari Prefab & Test Scene.
    /// </summary>
    public static class CrossroadsPrototypeSetup
    {
        private const string FbxPath = "Assets/_Project/Art/Characters/Ari/Ari.fbx";
        private const string PrefabPath = "Assets/_Project/Prefabs/Player/Ari.prefab";
        private const string ControllerPath = "Assets/_Project/Art/Characters/Ari/Ari_Controller.controller";
        private const string ScenePath = "Assets/_Project/Scenes/Dev/CharacterTest.unity";
        private const string MatBody = "Assets/_Project/Art/Characters/Ari/M_Ari.mat";
        private const string MatHair = "Assets/_Project/Art/Characters/Ari/M_Ari_Hair.mat";

        [MenuItem("CROSSROADS/Prototype/Build Ari Prefab & Test Scene")]
        public static void Build()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var mBody = AssetDatabase.LoadAssetAtPath<Material>(MatBody);
            var mHair = AssetDatabase.LoadAssetAtPath<Material>(MatHair);
            if (fbx == null || controller == null || mBody == null || mHair == null)
            {
                Debug.LogError("[CROSSROADS] Missing Ari FBX / controller / materials. Re-import Assets/_Project/Art/Characters/Ari first.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            instance.name = "Ari";

            // Animator + controller
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // CharacterController per GAME_DESIGN §8 (deterministic mobile movement)
            var cc = instance.GetComponent<CharacterController>();
            if (cc == null) cc = instance.AddComponent<CharacterController>();
            cc.height = 1.78f;
            cc.center = new Vector3(0f, 0.89f, 0f);
            cc.radius = 0.22f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.02f;

            // Prototype locomotion only
            if (instance.GetComponent<Crossroads.Gameplay.PlayerPrototypeController>() == null)
                instance.AddComponent<Crossroads.Gameplay.PlayerPrototypeController>();

            // Material slots: 0 = body atlas, 1 = hair
            foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name : "";
                    mats[i] = n.Contains("Hair") ? mHair : mBody;
                }
                smr.sharedMaterials = mats;
            }

            System.IO.Directory.CreateDirectory("Assets/_Project/Prefabs/Player");
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool ok);
            Object.DestroyImmediate(instance);
            if (!ok) { Debug.LogError("[CROSSROADS] Prefab save failed."); return; }

            // Place into test scene
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var spawned = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            spawned.name = "Ari";
            spawned.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CROSSROADS] Ari prefab built and placed into CharacterTest scene. Press Play.");
        }
    }
}
