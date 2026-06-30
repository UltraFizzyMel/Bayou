#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Bayou.Inventory.Editor
{
    [CustomEditor(typeof(ItemDefinition))]
    public sealed class ItemDefinitionEditor : UnityEditor.Editor
    {
        private const int CellPixels = 22;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var def = (ItemDefinition)target;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Shape painter (click cells)", EditorStyles.boldLabel);

            var shape = def.shape;
            if (shape.width < 1) shape.width = 1;
            if (shape.height < 1) shape.height = 1;
            var needed = shape.width * shape.height;
            if (shape.cells == null || shape.cells.Length != needed)
            {
                var n = new bool[needed];
                if (shape.cells != null)
                {
                    for (var i = 0; i < Mathf.Min(shape.cells.Length, n.Length); i++)
                        n[i] = shape.cells[i];
                }
                shape.cells = n;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1×1")) ApplyPreset(def, ItemShape.Rectangle(1, 1));
            if (GUILayout.Button("2×1")) ApplyPreset(def, ItemShape.Rectangle(2, 1));
            if (GUILayout.Button("2×2")) ApplyPreset(def, ItemShape.Rectangle(2, 2));
            if (GUILayout.Button("L")) ApplyPreset(def, ItemShape.LShape());
            EditorGUILayout.EndHorizontal();

            shape.width = EditorGUILayout.IntSlider("Width", shape.width, 1, 8);
            shape.height = EditorGUILayout.IntSlider("Height", shape.height, 1, 8);
            needed = shape.width * shape.height;
            if (shape.cells.Length != needed)
            {
                var n = new bool[needed];
                for (var i = 0; i < Mathf.Min(shape.cells.Length, n.Length); i++)
                    n[i] = shape.cells[i];
                shape.cells = n;
            }

            for (var y = 0; y < shape.height; y++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                for (var x = 0; x < shape.width; x++)
                {
                    var i = y * shape.width + x;
                    var occupied = shape.cells[i];
                    var c = occupied ? new Color(0.35f, 0.75f, 0.4f) : new Color(0.25f, 0.25f, 0.28f);
                    var r = GUILayout.Button(GUIContent.none, GUILayout.Width(CellPixels), GUILayout.Height(CellPixels));
                    var last = GUILayoutUtility.GetLastRect();
                    EditorGUI.DrawRect(last, c);
                    if (r)
                    {
                        shape.cells[i] = !shape.cells[i];
                        EditorUtility.SetDirty(def);
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            def.shape = shape;
            if (GUI.changed)
                EditorUtility.SetDirty(def);
        }

        private static void ApplyPreset(ItemDefinition def, ItemShape shape)
        {
            def.shape = shape;
            EditorUtility.SetDirty(def);
        }
    }
}
#endif
