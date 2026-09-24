#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EcosDeLaCripta.EditorTools
{
    public static class KenneyAnimCheck
    {
        public static void Check()
        {
            CheckFile("Assets/Art/Kenney/MiniDungeon/Models/character-human.fbx");
            CheckFile("Assets/Art/Kenney/MiniDungeon/Models/character-orc.fbx");
        }

        private static void CheckFile(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Debug.Log($"[KenneyAnimCheck] {path}: {assets.Length} sub-assets");
            foreach (var a in assets)
            {
                if (a == null) continue;
                Debug.Log($"  - {a.GetType().Name}: {a.name}");
            }
        }
    }
}
#endif
