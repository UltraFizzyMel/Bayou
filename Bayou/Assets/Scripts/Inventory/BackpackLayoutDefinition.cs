using UnityEngine;

namespace Bayou.Inventory
{
    public enum InventoryUILayoutMode
    {
        [Tooltip("Single puzzle grid (RE attaché case).")]
        AttachéCase = 0,

        [Tooltip("Multiple pockets over backpack art (top / middle / bottom). Shelved until upgrades return.")]
        MultiCompartmentBackpack = 1
    }

    [CreateAssetMenu(menuName = "Bayou/Inventory/Backpack Layout", fileName = "BackpackLayout_")]
    public sealed class BackpackLayoutDefinition : ScriptableObject
    {
        public InventoryUILayoutMode layoutMode = InventoryUILayoutMode.AttachéCase;

        [Tooltip("Optional panel background (backpack illustration). Leave empty for a plain dark case panel.")]
        public Sprite backgroundSprite;

        public Vector2 panelSize = new(640f, 480f);

        [Tooltip("One or more grids on the panel. Basic setup uses a single 7×6 case compartment.")]
        public InventoryCompartmentConfig[] compartments =
        {
            new InventoryCompartmentConfig
            {
                id = "case",
                displayName = "Case",
                gridWidth = 7,
                gridHeight = 6,
                anchor = InventoryCompartmentAnchor.BottomRight,
                anchoredPosition = new Vector2(-24f, 24f),
                slotAreaSize = new Vector2(252f, 212f),
                fillPanel = true,
                unlockedAtStart = true
            }
        };

        public static BackpackLayoutDefinition CreateDefaultRuntime()
        {
            var layout = CreateInstance<BackpackLayoutDefinition>();
            layout.name = "BackpackLayout_RuntimeDefault";
            ApplyAttachéCasePreset(layout);
            return layout;
        }

        /// <summary>RE attaché case: one 7×6 grid, bottom-right on a wide dark panel.</summary>
        public static void ApplyAttachéCasePreset(BackpackLayoutDefinition layout)
        {
            if (layout == null) return;

            layout.layoutMode = InventoryUILayoutMode.AttachéCase;
            layout.backgroundSprite = null;
            layout.panelSize = new Vector2(640f, 480f);
            layout.compartments = new[]
            {
                new InventoryCompartmentConfig
                {
                    id = "case",
                    displayName = "Case",
                    gridWidth = 7,
                    gridHeight = 6,
                    anchor = InventoryCompartmentAnchor.BottomRight,
                    anchoredPosition = new Vector2(-24f, 24f),
                    slotAreaSize = new Vector2(252f, 212f),
                    fillPanel = true,
                    unlockedAtStart = true
                }
            };
        }

        /// <summary>Three-pocket backpack art layout (upgrade-gated pockets when upgrades are enabled).</summary>
        public static void ApplyMultiCompartmentBackpackPreset(BackpackLayoutDefinition layout)
        {
            if (layout == null) return;

            layout.layoutMode = InventoryUILayoutMode.MultiCompartmentBackpack;
            layout.panelSize = new Vector2(280f, 520f);
            layout.compartments = new[]
            {
                new InventoryCompartmentConfig
                {
                    id = "top",
                    displayName = "Top",
                    gridWidth = 5,
                    gridHeight = 5,
                    anchoredPosition = new Vector2(40f, -52f),
                    slotAreaSize = new Vector2(200f, 200f),
                    fillPanel = false,
                    unlockedAtStart = false,
                    requiredUpgradeId = "upgrade_top"
                },
                new InventoryCompartmentConfig
                {
                    id = "middle",
                    displayName = "Middle",
                    gridWidth = 4,
                    gridHeight = 4,
                    anchoredPosition = new Vector2(48f, -268f),
                    slotAreaSize = new Vector2(184f, 160f),
                    fillPanel = false,
                    unlockedAtStart = false,
                    requiredUpgradeId = "upgrade_middle"
                },
                new InventoryCompartmentConfig
                {
                    id = "bottom",
                    displayName = "Bottom",
                    gridWidth = 6,
                    gridHeight = 2,
                    anchoredPosition = new Vector2(32f, -448f),
                    slotAreaSize = new Vector2(216f, 56f),
                    fillPanel = false,
                    unlockedAtStart = true
                }
            };
        }
    }
}
