#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Bayou.UI.Editor
{
    public static class HotwheelSkinMenu
    {
        private const string DefaultPath = "Assets/Resources/Bayou/UI/HotwheelSkin_Default.asset";

        [MenuItem("Bayou/UI/Create Hotwheel Skin", false, 40)]
        public static void CreateSkin()
        {
            var skin = ScriptableObject.CreateInstance<EquipmentHotwheelSkin>();
            skin.slotCount = 4;
            skin.firstSlotAngle = 90f;
            skin.clockwise = true;

            var path = EditorUtility.SaveFilePanelInProject(
                "Hotwheel Skin",
                "HotwheelSkin_",
                "asset",
                "Save the skin your teammate can fill with circular wheel art.",
                "Assets/Resources/Bayou/UI");
            if (string.IsNullOrEmpty(path))
            {
                Object.DestroyImmediate(skin);
                return;
            }

            AssetDatabase.CreateAsset(skin, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = skin;
            EditorGUIUtility.PingObject(skin);
            Debug.Log(
                "[Bayou] Hotwheel skin created.\n" +
                "  Assign Wheel Disc (circular PNG), optional Selector / Hub / Slot Plate,\n" +
                "  or a prefab with children: Disc, Selector, Hub, Slot_1..Slot_4.\n" +
                "  Drag item icons on each Item Definition (rod, net, lantern) — those show on the wheel.\n" +
                "  To override the default, save as Resources/Bayou/UI/HotwheelSkin.asset");
        }

        [MenuItem("Bayou/UI/Select Default Hotwheel Skin")]
        public static void SelectDefault()
        {
            var skin = AssetDatabase.LoadAssetAtPath<EquipmentHotwheelSkin>(DefaultPath);
            if (skin == null)
            {
                Debug.LogWarning($"[Bayou] Missing {DefaultPath}");
                return;
            }

            Selection.activeObject = skin;
            EditorGUIUtility.PingObject(skin);
        }
    }
}
#endif
