using UnityEngine;

namespace Bayou.UI
{
    /// <summary>
    /// Art + layout for the circular equipment wheel.
    /// Drop a teammate's circular PNG/prefab here — gameplay code stays the same.
    ///
    /// Prefab child names (optional, all centered):
    ///   Disc, Selector, Hub, Slots/Slot_1 .. Slot_4
    /// Icons are parented into Slot_N at runtime from each item's Icon field.
    /// </summary>
    [CreateAssetMenu(menuName = "Bayou/UI/Hotwheel Skin", fileName = "HotwheelSkin_")]
    public sealed class EquipmentHotwheelSkin : ScriptableObject
    {
        [Header("Teammate art")]
        [Tooltip("Full circular wheel plate (PNG with transparent corners).")]
        public Sprite wheelDisc;
        [Tooltip("Rotates to the hovered slice — pointer, notch, or wedge.")]
        public Sprite selector;
        [Tooltip("Center cap over the disc.")]
        public Sprite hub;
        [Tooltip("Plate behind each slotted item icon. Leave empty to hide.")]
        public Sprite slotPlate;
        [Tooltip("Optional prefab with Disc / Selector / Hub / Slot_1..Slot_N. Overrides the sprites above when set.")]
        public GameObject wheelPrefab;

        [Header("Circle")]
        [Range(2, 8)] public int slotCount = 4;
        [Tooltip("Angle of slot 1 in degrees. 90 = top, 0 = right.")]
        public float firstSlotAngle = 90f;
        public bool clockwise = true;
        public Vector2 wheelSize = new(560f, 560f);
        [Tooltip("Distance from center to item icons, in wheel-local pixels.")]
        public float iconOrbit = 168f;
        public Vector2 iconSize = new(72f, 72f);
        public Vector2 slotPlateSize = new(96f, 96f);
        [Range(0.05f, 0.6f)]
        [Tooltip("Ignore aim inside this fraction of the wheel radius (hub).")]
        public float innerDeadzone = 0.22f;

        [Header("Tint")]
        public Color discColor = Color.white;
        public Color hubColor = Color.white;
        public Color selectorColor = new(1f, 0.86f, 0.45f, 1f);
        public Color slotIdle = new(1f, 1f, 1f, 0.55f);
        public Color slotHover = new(1f, 0.88f, 0.4f, 1f);
        public Color slotSelected = new(0.65f, 0.9f, 0.5f, 1f);
        public Color dimColor = new(0.02f, 0.02f, 0.02f, 0.4f);
        public Color labelColor = new(0.95f, 0.9f, 0.78f, 1f);

        public int ResolvedSlotCount => Mathf.Clamp(slotCount, 2, 8);

        public float SlotStepDegrees => 360f / ResolvedSlotCount;

        public float SlotCenterAngle(int index)
        {
            var step = SlotStepDegrees;
            var signed = clockwise ? -index * step : index * step;
            return firstSlotAngle + signed;
        }

        public Vector2 SlotDirection(int index)
        {
            var rad = SlotCenterAngle(index) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        public int SlotFromAim(Vector2 screenDelta, float wheelRadiusPixels)
        {
            var dead = Mathf.Max(8f, wheelRadiusPixels * innerDeadzone);
            if (screenDelta.sqrMagnitude < dead * dead)
                return -1;

            var aim = Mathf.Atan2(screenDelta.y, screenDelta.x) * Mathf.Rad2Deg;
            var best = 0;
            var bestDelta = 999f;
            var count = ResolvedSlotCount;
            for (var i = 0; i < count; i++)
            {
                var d = Mathf.Abs(Mathf.DeltaAngle(aim, SlotCenterAngle(i)));
                if (d < bestDelta)
                {
                    bestDelta = d;
                    best = i;
                }
            }

            return bestDelta <= SlotStepDegrees * 0.5f + 0.01f ? best : -1;
        }
    }
}
