#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Bayou.Environment;
using Bayou.Inventory;
using Bayou.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bayou.Quests.Editor
{
    /// <summary>
    /// Turns an existing artist-placed crate into one of the three Lost Cargo pickups.
    /// Select the whole crate in Hierarchy, then choose the relevant area from the menu.
    /// The tool does not move, resize or change materials on the existing box.
    /// </summary>
    public static class LostCargoPickupSetupMenu
    {
        private const string RootMenu = "Bayou/Quests/Lost Cargo/";
        private const string GraveId = "Item_CalisteCargoGraveyard";
        private const string FoggyId = "Item_CalisteCargoFoggy";
        private const string ShoreId = "Item_CalisteCargoBrackish";
        private const string AssetFolder = "Assets/Resources/Bayou/Items";

        [MenuItem(RootMenu + "Assign Selected Box - Graveyard", false, 100)]
        private static void AssignGraveyard() => Assign(GraveId, "Graveyard cargo");
        [MenuItem(RootMenu + "Assign Selected Box - Foggy Marsh", false, 101)]
        private static void AssignFoggy() => Assign(FoggyId, "Foggy marsh cargo");
        [MenuItem(RootMenu + "Assign Selected Box - Brackish Shore", false, 102)]
        private static void AssignShore() => Assign(ShoreId, "Brackish shore cargo");

        [MenuItem(RootMenu + "Assign Selected Box - Graveyard", true)]
        [MenuItem(RootMenu + "Assign Selected Box - Foggy Marsh", true)]
        [MenuItem(RootMenu + "Assign Selected Box - Brackish Shore", true)]
        private static bool ValidateSelected() =>
            !EditorApplication.isPlaying && Selection.activeGameObject != null &&
            Selection.activeGameObject.scene.IsValid();

        [MenuItem(RootMenu + "Check Cargo Pickups In Open Scene", false, 120)]
        private static void CheckCargo()
        {
            var boxes = UnityEngine.Object.FindObjectsByType<QuestItemPickup>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var counts = new int[3];
            foreach (var box in boxes)
            {
                if (box == null || box.Item == null) continue;
                switch (box.Item.Id)
                {
                    case GraveId: counts[0]++; break;
                    case FoggyId: counts[1]++; break;
                    case ShoreId: counts[2]++; break;
                }
            }

            var message = $"Graveyard: {counts[0]} pickup(s)\n" +
                          $"Foggy Marsh: {counts[1]} pickup(s)\n" +
                          $"Brackish Shore: {counts[2]} pickup(s)\n\n" +
                          "Each location needs one active crate with the correct cargo definition.";
            Debug.Log("[Lost Cargo] " + message.Replace("\n", "; "));
            EditorUtility.DisplayDialog("Lost Cargo - Scene Check", message, "OK");
        }

        private static void Assign(string itemId, string label)
        {
            var box = Selection.activeGameObject;
            if (box == null || !box.scene.IsValid())
            {
                EditorUtility.DisplayDialog("Lost Cargo", "Select the cargo box GameObject in the scene Hierarchy first.", "OK");
                return;
            }

            var definition = FindOrCreateItem(itemId, label);
            if (definition == null) return;

            // Legacy example pickups use Item_Box and its ShinyWorldPickup component.
            // Remove that handler so it cannot compete for the E key.
            var shinies = box.GetComponentsInChildren<ShinyWorldPickup>(true);
            foreach (var shiny in shinies)
                if (shiny != null) Undo.DestroyObjectImmediate(shiny);

            // An extra trigger works even when the artist's mesh has a solid BoxCollider.
            // QuestItemPickup also has a short proximity fallback for unreliable triggers.
            var trigger = box.GetComponent<SphereCollider>();
            if (trigger == null || !trigger.isTrigger)
                trigger = Undo.AddComponent<SphereCollider>(box);
            Undo.RecordObject(trigger, "Configure cargo pickup range");
            trigger.isTrigger = true;
            var scale = box.transform.lossyScale;
            var horizontalScale = Mathf.Max(0.05f, Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            trigger.radius = 2f / horizontalScale;
            trigger.center = new Vector3(0f, 0.45f / Mathf.Max(0.05f, Mathf.Abs(scale.y)), 0f);
            EditorUtility.SetDirty(trigger);

            var pickup = box.GetComponent<QuestItemPickup>();
            if (pickup == null)
                pickup = Undo.AddComponent<QuestItemPickup>(box);
            Undo.RecordObject(pickup, "Configure Lost Cargo pickup");
            pickup.ConfigureAsCargoBox(definition);
            EditorUtility.SetDirty(pickup);

            // Avoid an older pickup on a child of this crate claiming E first.
            var nestedPickups = box.GetComponentsInChildren<QuestItemPickup>(true);
            foreach (var nested in nestedPickups)
                if (nested != null && nested != pickup) Undo.DestroyObjectImmediate(nested);

            var beacon = box.GetComponent<QuestMarkerTarget>();
            if (beacon == null)
                beacon = Undo.AddComponent<QuestMarkerTarget>(box);
            Undo.RecordObject(beacon, "Configure Lost Cargo quest marker");
            beacon.Bind(QuestIds.LostCargo, itemId, label);
            EditorUtility.SetDirty(beacon);

            EditorSceneManager.MarkSceneDirty(box.scene);
            Debug.Log($"[Lost Cargo] '{box.name}' now picks up {itemId} by pressing E. Save the scene.", box);
            EditorUtility.DisplayDialog("Lost Cargo", $"Configured '{box.name}' as {label}.\n\nSave the scene (Ctrl+S), enter Play mode, approach it and press E.", "OK");
        }

        private static ItemDefinition FindOrCreateItem(string id, string label)
        {
            var guids = AssetDatabase.FindAssets("t:ItemDefinition");
            ItemDefinition template = null;
            foreach (var guid in guids)
            {
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                if (string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    AddToCatalogs(def);
                    return def;
                }
                if (string.Equals(def.Id, "Item_Box", StringComparison.OrdinalIgnoreCase))
                    template = def;
            }

            EnsureAssetFolder();
            var defNew = ScriptableObject.CreateInstance<ItemDefinition>();
            defNew.itemId = id;
            defNew.displayName = label;
            defNew.description = "A lost cargo box belonging to Caliste.";
            defNew.shape = template != null ? template.shape : ItemShape.Rectangle(2, 2);
            defNew.icon = template != null ? template.icon : null;
            defNew.maxStack = 1;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{AssetFolder}/{id}.asset");
            AssetDatabase.CreateAsset(defNew, path);
            AssetDatabase.SaveAssets();
            AddToCatalogs(defNew);
            Debug.Log($"[Lost Cargo] Created missing item definition: {path}");
            return defNew;
        }

        private static void EnsureAssetFolder()
        {
            var segments = AssetFolder.Split('/');
            var path = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = path + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(path, segments[i]);
                path = next;
            }
        }

        private static void AddToCatalogs(ItemDefinition definition)
        {
            var changed = false;
            foreach (var guid in AssetDatabase.FindAssets("t:ItemCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog == null) continue;
                var current = catalog.AllDefinitions;
                var present = false;
                foreach (var item in current)
                {
                    if (item != null && item.MatchesId(definition.Id))
                    {
                        present = true;
                        break;
                    }
                }
                if (present) continue;
                Undo.RecordObject(catalog, "Add cargo item to item catalog");
                var updated = new List<ItemDefinition>(current) { definition };
                catalog.SetItems(updated.ToArray());
                EditorUtility.SetDirty(catalog);
                changed = true;
            }
            if (changed) AssetDatabase.SaveAssets();
        }
    }
}
#endif
