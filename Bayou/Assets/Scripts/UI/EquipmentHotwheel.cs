#if !ENABLE_INPUT_SYSTEM
#error EquipmentHotwheel requires the New Input System.
#endif

using System.Collections.Generic;
using Bayou.Fishing;
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Save;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// Circular equipment wheel. Hold Tab, aim a slice, release to equip.
    /// Art comes from <see cref="EquipmentHotwheelSkin"/> (teammate disc / prefab).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentHotwheel : MonoBehaviour
    {
        public const string DefaultSkinResource = "Bayou/UI/HotwheelSkin_Default";
        public const string OverrideSkinResource = "Bayou/UI/HotwheelSkin";

        [SerializeField] private EquipmentHotwheelSkin skin;
        [SerializeField] private bool buildUiIfMissing = true;

        private string[] _slotItemIds = System.Array.Empty<string>();
        private SlotView[] _slots = System.Array.Empty<SlotView>();

        private Canvas _canvas;
        private RectTransform _wheelRoot;
        private RectTransform _assignRoot;
        private Image _dim;
        private Image _disc;
        private Image _hub;
        private RectTransform _selector;
        private TextMeshProUGUI _hint;
        private TextMeshProUGUI _centerLabel;
        private bool _open;
        private int _hover = -1;
        private string _pendingAssignId;
        private readonly List<ItemDefinition> _owned = new();

        public static EquipmentHotwheel Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;
        public static bool SuppressLegacyToolKeys => Instance != null;

        public EquipmentHotwheelSkin Skin => skin;

        private sealed class SlotView
        {
            public RectTransform Root;
            public Image Plate;
            public Image Icon;
            public TextMeshProUGUI Index;
            public TextMeshProUGUI Name;
        }

        public static void EnsureInScene()
        {
            if (Object.FindFirstObjectByType<EquipmentHotwheel>(FindObjectsInactive.Include) != null)
                return;
            var go = new GameObject("EquipmentHotwheel");
            go.AddComponent<EquipmentHotwheel>();
        }

        /// <summary>Swap teammate art at runtime or from a bootstrap.</summary>
        public void ApplySkin(EquipmentHotwheelSkin next)
        {
            if (next == null) return;
            skin = next;
            if (_wheelRoot != null)
                Destroy(_wheelRoot.gameObject);
            if (_dim != null)
                Destroy(_dim.gameObject);
            if (_assignRoot != null)
                Destroy(_assignRoot.gameObject);
            if (_canvas != null)
                Destroy(_canvas.gameObject);
            BuildUi();
            SetWheelVisible(_open);
            RefreshSlotVisuals();
        }

        private void Awake()
        {
            Instance = this;
            ResolveSkin();
            EnsureSlotBuffers();
            if (buildUiIfMissing)
                BuildUi();
            SetWheelVisible(false);
            RefreshAssignStrip();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (ShouldHide())
            {
                if (_open) Close(selectHover: false);
                RefreshAssignStrip();
                return;
            }

            HandleSlotHotkeys();
            HandleWheelHold();
            RefreshAssignStrip();

            if (!_open) return;

            _hover = ResolveHoverSlot();
            RefreshSlotVisuals();
            UpdateCenterLabel();
            UpdateSelector();

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && _hover >= 0)
                CycleAssign(_hover);
        }

        /// <summary>Playtest / bootstrap: show the wheel without holding Tab.</summary>
        public void OpenWheel() => Open();

        /// <summary>Playtest / bootstrap: hide the wheel without equipping.</summary>
        public void CloseWheel() => Close(selectHover: false);

        /// <summary>Playtest / bootstrap: write item ids into slices (null / empty = unequip).</summary>
        public void SetSlotItemIds(params string[] itemIds)
        {
            EnsureSlotBuffers();
            for (var i = 0; i < _slotItemIds.Length; i++)
                _slotItemIds[i] = itemIds != null && i < itemIds.Length ? itemIds[i] : null;
            RefreshSlotVisuals();
            RefreshAssignStrip();
        }

        public int SlotCount => _slotItemIds.Length;

        public bool TrySelectSlot(int index)
        {
            if (index < 0 || index >= _slotItemIds.Length) return false;
            var id = _slotItemIds[index];
            var equipment = ResolveEquipment();
            if (equipment == null) return false;

            if (string.IsNullOrWhiteSpace(id))
            {
                equipment.ApplyItem(BayouHeldItem.None);
                return true;
            }

            if (!equipment.TryEquipItemId(id))
            {
                Debug.Log($"[Hotwheel] Can't equip {id} — you don't have it.");
                return false;
            }

            return true;
        }

        public void AssignSlot(int index, ItemDefinition item)
        {
            if (index < 0 || index >= _slotItemIds.Length) return;
            _slotItemIds[index] = item != null ? item.Id : null;
            RefreshSlotVisuals();
            RefreshAssignStrip();
        }

        private void ResolveSkin()
        {
            if (skin != null) return;
            skin = Resources.Load<EquipmentHotwheelSkin>(OverrideSkinResource);
            if (skin == null)
                skin = Resources.Load<EquipmentHotwheelSkin>(DefaultSkinResource);
            if (skin == null)
                skin = ScriptableObject.CreateInstance<EquipmentHotwheelSkin>();
        }

        private void EnsureSlotBuffers()
        {
            var count = skin != null ? skin.ResolvedSlotCount : 4;
            if (_slotItemIds.Length != count)
            {
                var next = new string[count];
                for (var i = 0; i < Mathf.Min(count, _slotItemIds.Length); i++)
                    next[i] = _slotItemIds[i];
                _slotItemIds = next;
            }

            if (_slots.Length != count)
                _slots = new SlotView[count];
        }

        private void HandleSlotHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null || _open) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) TrySelectSlot(0);
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) TrySelectSlot(1);
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) TrySelectSlot(2);
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) TrySelectSlot(3);
        }

        private void HandleWheelHold()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame)
                Open();
            if (_open && kb.tabKey.wasReleasedThisFrame)
                Close(selectHover: true);
        }

        private void Open()
        {
            _open = true;
            _hover = -1;
            _pendingAssignId = null;
            SetWheelVisible(true);
            RefreshSlotVisuals();
            UpdateCenterLabel();
            UpdateSelector();
        }

        private void Close(bool selectHover)
        {
            if (selectHover && _hover >= 0)
                TrySelectSlot(_hover);
            _open = false;
            _hover = -1;
            SetWheelVisible(false);
        }

        private int ResolveHoverSlot()
        {
            var mouse = Mouse.current;
            if (mouse == null || skin == null) return _hover;

            var pos = mouse.position.ReadValue();
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var radius = Mathf.Min(Screen.height, Screen.width) * 0.28f;
            return skin.SlotFromAim(pos - center, radius);
        }

        private void CycleAssign(int index)
        {
            CollectOwnedEquipment();
            if (_owned.Count == 0)
            {
                AssignSlot(index, null);
                return;
            }

            var current = _slotItemIds[index];
            var next = 0;
            for (var i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null && _owned[i].MatchesId(current))
                {
                    next = i + 1;
                    break;
                }
            }

            AssignSlot(index, next >= _owned.Count ? null : _owned[next]);
        }

        private void CollectOwnedEquipment()
        {
            _owned.Clear();
            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (inv?.Bag == null) return;

            foreach (var inst in inv.Bag.AllItems)
            {
                var def = inst?.definition;
                if (def == null || !def.IsUniqueEquipment) continue;
                if (!BayouFishingEquipment.TryResolveHeldItem(def.Id, out _)) continue;
                var already = false;
                for (var i = 0; i < _owned.Count; i++)
                {
                    if (_owned[i].MatchesId(def.Id))
                    {
                        already = true;
                        break;
                    }
                }

                if (!already)
                    _owned.Add(def);
            }
        }

        private void RefreshSlotVisuals()
        {
            var equipment = ResolveEquipment();
            var held = equipment != null ? equipment.CurrentItem : BayouHeldItem.None;

            for (var i = 0; i < _slots.Length; i++)
            {
                var view = _slots[i];
                if (view == null) continue;

                var def = i < _slotItemIds.Length ? ResolveItem(_slotItemIds[i]) : null;
                var selected = def != null &&
                               BayouFishingEquipment.TryResolveHeldItem(def.Id, out var mapped) &&
                               mapped == held;
                var hovered = _open && _hover == i;

                if (view.Plate != null)
                {
                    view.Plate.color = hovered
                        ? skin.slotHover
                        : selected
                            ? skin.slotSelected
                            : skin.slotIdle;
                    view.Plate.enabled = view.Plate.sprite != null;
                }

                if (view.Icon != null)
                {
                    view.Icon.sprite = def != null ? def.icon : null;
                    view.Icon.enabled = def != null && def.icon != null;
                    view.Icon.preserveAspect = true;
                    view.Icon.color = Color.white;
                }

                if (view.Name != null)
                {
                    view.Name.color = skin.labelColor;
                    view.Name.text = def != null
                        ? (string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName)
                        : "Empty";
                }

                if (view.Index != null)
                {
                    view.Index.color = skin.labelColor;
                    view.Index.text = (i + 1).ToString();
                }
            }
        }

        private void UpdateCenterLabel()
        {
            if (_centerLabel == null) return;
            _centerLabel.color = skin.labelColor;
            if (_hover >= 0 && _hover < _slotItemIds.Length)
            {
                var def = ResolveItem(_slotItemIds[_hover]);
                _centerLabel.text = def != null
                    ? (string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName)
                    : "Unequip";
                return;
            }

            _centerLabel.text = "Equipment";
        }

        private void UpdateSelector()
        {
            if (_selector == null || skin == null) return;
            var show = _open && _hover >= 0;
            if (_selector.gameObject.activeSelf != show)
                _selector.gameObject.SetActive(show);
            if (!show) return;
            _selector.localRotation = Quaternion.Euler(0f, 0f, skin.SlotCenterAngle(_hover) - 90f);
        }

        private void RefreshAssignStrip()
        {
            if (_assignRoot == null) return;
            var bag = InventoryDisplayUI.Active;
            var show = bag != null && bag.IsOpen && !_open;
            if (_assignRoot.gameObject.activeSelf != show)
                _assignRoot.gameObject.SetActive(show);
            if (!show) return;

            CollectOwnedEquipment();
            RebuildAssignButtons();
        }

        private void RebuildAssignButtons()
        {
            for (var i = _assignRoot.childCount - 1; i >= 0; i--)
            {
                var child = _assignRoot.GetChild(i);
                if (child.name.StartsWith("Chip_", System.StringComparison.Ordinal) ||
                    child.name.StartsWith("AssignSlot_", System.StringComparison.Ordinal))
                    Destroy(child.gameObject);
            }

            var x = 8f;
            for (var i = 0; i < _slotItemIds.Length; i++)
            {
                var slotIndex = i;
                var def = ResolveItem(_slotItemIds[i]);
                var label = def != null
                    ? $"{i + 1} {(string.IsNullOrWhiteSpace(def.displayName) ? def.name : def.displayName)}"
                    : $"{i + 1} Empty";
                var btn = CreateChip(_assignRoot, $"AssignSlot_{i}", label, new Vector2(x, 8f), new Vector2(118f, 36f));
                btn.onClick.AddListener(() =>
                {
                    if (string.IsNullOrWhiteSpace(_pendingAssignId))
                    {
                        CycleAssign(slotIndex);
                        return;
                    }

                    AssignSlot(slotIndex, ResolveItem(_pendingAssignId));
                    _pendingAssignId = null;
                    RebuildAssignButtons();
                });
                x += 124f;
            }

            x += 16f;
            for (var i = 0; i < _owned.Count; i++)
            {
                var item = _owned[i];
                if (item == null) continue;
                var id = item.Id;
                var name = string.IsNullOrWhiteSpace(item.displayName) ? item.name : item.displayName;
                var pending = string.Equals(_pendingAssignId, id, System.StringComparison.OrdinalIgnoreCase);
                var btn = CreateChip(_assignRoot, $"Chip_{id}", pending ? $"→ {name}" : $"+ {name}",
                    new Vector2(x, 8f), new Vector2(130f, 36f));
                btn.onClick.AddListener(() =>
                {
                    _pendingAssignId = id;
                    RebuildAssignButtons();
                });
                x += 136f;
            }
        }

        private void SetWheelVisible(bool visible)
        {
            if (_wheelRoot != null)
                _wheelRoot.gameObject.SetActive(visible);
            if (_dim != null)
                _dim.enabled = visible;
        }

        private static bool ShouldHide()
        {
            if (AudioSettings.IsOpen) return true;
            if (ShopUIController.ActiveShop != null && ShopUIController.ActiveShop.IsOpen) return true;
            if (BonfireUIController.Active != null && BonfireUIController.Active.IsOpen) return true;
            var dialogue = DialogueManager.GetInstance();
            return dialogue != null && dialogue.dialogueIsPlaying;
        }

        private static BayouFishingEquipment ResolveEquipment()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var onPlayer = player.GetComponent<BayouFishingEquipment>();
                if (onPlayer != null) return onPlayer;
            }

            return Object.FindFirstObjectByType<BayouFishingEquipment>();
        }

        private static ItemDefinition ResolveItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return null;
            var catalog = GameSaveSystem.Instance != null ? GameSaveSystem.Instance.ItemCatalog : null;
            catalog ??= Resources.Load<ItemCatalog>("Bayou/ItemCatalog");
            var fromCat = catalog != null ? catalog.Resolve(itemId) : null;
            if (fromCat != null) return fromCat;

            foreach (var def in Resources.LoadAll<ItemDefinition>("Bayou/Items"))
            {
                if (def != null && def.MatchesId(itemId))
                    return def;
            }

            return null;
        }

        private void BuildUi()
        {
            ResolveSkin();
            EnsureSlotBuffers();

            var canvasGo = new GameObject("EquipmentHotwheelCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 18;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var dimGo = CreateRect("Dim", canvasGo.transform);
            Stretch(dimGo);
            _dim = dimGo.gameObject.AddComponent<Image>();
            _dim.color = skin.dimColor;
            _dim.raycastTarget = false;

            _wheelRoot = CreateRect("Wheel", canvasGo.transform);
            _wheelRoot.anchorMin = _wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _wheelRoot.sizeDelta = skin.wheelSize;

            if (skin.wheelPrefab != null)
                BindTeammatePrefab();
            else
                BuildGeneratedCircle();

            BuildSlotIcons();

            _centerLabel = CreateTmp("Center", _wheelRoot, "Equipment", 22f, FontStyles.Bold);
            _centerLabel.rectTransform.sizeDelta = new Vector2(200f, 64f);
            _centerLabel.color = skin.labelColor;

            _hint = CreateTmp("Hint", _wheelRoot, "Hold Tab · aim a slice · release\nRMB assign  ·  1–4 quick-select", 15f,
                FontStyles.Normal);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, -skin.wheelSize.y * 0.42f);
            _hint.rectTransform.sizeDelta = new Vector2(400f, 48f);
            _hint.color = skin.labelColor;

            BuildAssignStrip(canvasGo.transform);
            RefreshSlotVisuals();
        }

        private void BindTeammatePrefab()
        {
            var art = Instantiate(skin.wheelPrefab, _wheelRoot, false);
            art.name = "TeammateWheel";
            var artRt = art.transform as RectTransform;
            if (artRt != null)
            {
                artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 0.5f);
                artRt.pivot = new Vector2(0.5f, 0.5f);
                artRt.anchoredPosition = Vector2.zero;
                artRt.sizeDelta = skin.wheelSize;
            }

            _disc = FindNamed<Image>(art.transform, "Disc");
            _hub = FindNamed<Image>(art.transform, "Hub");
            var selectorImg = FindNamed<Image>(art.transform, "Selector");
            _selector = selectorImg != null ? selectorImg.rectTransform : null;

            if (_disc != null)
            {
                _disc.preserveAspect = true;
                if (skin.wheelDisc != null)
                    _disc.sprite = skin.wheelDisc;
            }

            if (_hub != null && skin.hub != null)
                _hub.sprite = skin.hub;

            if (selectorImg != null)
            {
                if (skin.selector != null)
                    selectorImg.sprite = skin.selector;
                selectorImg.preserveAspect = true;
                selectorImg.color = skin.selectorColor;
            }
        }

        private void BuildGeneratedCircle()
        {
            var discRt = CreateRect("Disc", _wheelRoot);
            discRt.sizeDelta = skin.wheelSize;
            _disc = discRt.gameObject.AddComponent<Image>();
            _disc.sprite = skin.wheelDisc != null ? skin.wheelDisc : CircleSprite(256);
            _disc.preserveAspect = true;
            _disc.color = skin.wheelDisc != null ? skin.discColor : new Color(0.14f, 0.12f, 0.09f, 0.82f);
            _disc.raycastTarget = false;

            var hubRt = CreateRect("Hub", _wheelRoot);
            hubRt.sizeDelta = skin.wheelSize * 0.28f;
            _hub = hubRt.gameObject.AddComponent<Image>();
            _hub.sprite = skin.hub != null ? skin.hub : CircleSprite(96);
            _hub.preserveAspect = true;
            _hub.color = skin.hub != null ? skin.hubColor : new Color(0.1f, 0.09f, 0.07f, 0.95f);
            _hub.raycastTarget = false;

            var selRt = CreateRect("Selector", _wheelRoot);
            selRt.sizeDelta = new Vector2(36f, skin.iconOrbit + 24f);
            selRt.pivot = new Vector2(0.5f, 0f);
            _selector = selRt;
            var selImg = selRt.gameObject.AddComponent<Image>();
            selImg.sprite = skin.selector != null ? skin.selector : PointerSprite();
            selImg.preserveAspect = true;
            selImg.color = skin.selectorColor;
            selImg.raycastTarget = false;
            selRt.gameObject.SetActive(false);
        }

        private void BuildSlotIcons()
        {
            var count = skin.ResolvedSlotCount;
            var art = _wheelRoot.Find("TeammateWheel");

            for (var i = 0; i < count; i++)
            {
                var host = FindSlotHost(art, i);
                if (host == null)
                {
                    host = CreateRect($"Slot_{i + 1}", _wheelRoot);
                    host.sizeDelta = skin.slotPlateSize;
                    host.anchoredPosition = skin.SlotDirection(i) * skin.iconOrbit;
                }

                var view = new SlotView { Root = host };

                var plate = host.GetComponent<Image>();
                if (plate == null)
                    plate = host.gameObject.AddComponent<Image>();
                plate.sprite = skin.slotPlate != null ? skin.slotPlate : CircleSprite(96);
                plate.preserveAspect = true;
                plate.raycastTarget = false;
                view.Plate = plate;

                var iconRt = host.Find("Icon") as RectTransform;
                if (iconRt == null)
                    iconRt = CreateRect("Icon", host);
                iconRt.sizeDelta = skin.iconSize;
                var icon = iconRt.GetComponent<Image>() ?? iconRt.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                view.Icon = icon;

                view.Index = host.Find("Index")?.GetComponent<TextMeshProUGUI>();
                if (view.Index == null)
                {
                    view.Index = CreateTmp("Index", host, (i + 1).ToString(), 16f, FontStyles.Bold);
                    view.Index.rectTransform.anchoredPosition = new Vector2(0f, skin.iconSize.y * 0.42f);
                    view.Index.rectTransform.sizeDelta = new Vector2(40f, 22f);
                }

                view.Name = host.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (view.Name == null)
                {
                    view.Name = CreateTmp("Name", host, "Empty", 13f, FontStyles.Normal);
                    view.Name.rectTransform.anchoredPosition = new Vector2(0f, -skin.iconSize.y * 0.55f);
                    view.Name.rectTransform.sizeDelta = new Vector2(130f, 22f);
                }

                _slots[i] = view;
            }
        }

        private static RectTransform FindSlotHost(Transform artRoot, int index)
        {
            if (artRoot == null) return null;
            var n = index + 1;
            var names = new[]
            {
                $"Slot_{n}",
                $"Slot{n}",
                $"Slots/Slot_{n}",
                $"Slots/Slot{n}"
            };

            for (var i = 0; i < names.Length; i++)
            {
                var found = artRoot.Find(names[i]);
                if (found is RectTransform rt)
                    return rt;
            }

            return FindNamed<RectTransform>(artRoot, $"Slot_{n}");
        }

        private void BuildAssignStrip(Transform canvas)
        {
            _assignRoot = CreateRect("AssignStrip", canvas);
            _assignRoot.anchorMin = new Vector2(0.5f, 0f);
            _assignRoot.anchorMax = new Vector2(0.5f, 0f);
            _assignRoot.pivot = new Vector2(0.5f, 0f);
            _assignRoot.anchoredPosition = new Vector2(0f, 18f);
            _assignRoot.sizeDelta = new Vector2(920f, 52f);
            var stripBg = _assignRoot.gameObject.AddComponent<Image>();
            stripBg.color = new Color(0.12f, 0.1f, 0.08f, 0.82f);
            stripBg.raycastTarget = true;

            var stripHint = CreateTmp("AssignHint", _assignRoot, "Hotwheel  ·  click gear, then a slot", 13f,
                FontStyles.Italic);
            stripHint.rectTransform.anchorMin = new Vector2(0f, 1f);
            stripHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            stripHint.rectTransform.pivot = new Vector2(0.5f, 0f);
            stripHint.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            stripHint.rectTransform.sizeDelta = new Vector2(0f, 18f);
        }

        private static T FindNamed<T>(Transform root, string name) where T : Component
        {
            if (root == null) return null;
            if (root.name == name)
            {
                var self = root.GetComponent<T>();
                if (self != null) return self;
            }

            var child = root.Find(name);
            if (child != null)
            {
                var c = child.GetComponent<T>();
                if (c != null) return c;
            }

            var all = root.GetComponentsInChildren<T>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }

            return null;
        }

        private static Button CreateChip(RectTransform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.28f, 0.24f, 0.16f, 0.95f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var tmp = CreateTmp("Label", rt, label, 13f, FontStyles.Normal);
            tmp.rectTransform.anchorMin = Vector2.zero;
            tmp.rectTransform.anchorMax = Vector2.one;
            tmp.rectTransform.offsetMin = Vector2.zero;
            tmp.rectTransform.offsetMax = Vector2.zero;
            return btn;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, float size, FontStyles style)
        {
            var rt = CreateRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.95f, 0.9f, 0.78f, 1f);
            tmp.raycastTarget = false;
            rt.sizeDelta = new Vector2(200f, 28f);
            return tmp;
        }

        private static Sprite CircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var c = (size - 1) * 0.5f;
            var r = c - 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite PointerSprite()
        {
            const int w = 32;
            const int h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color(1f, 1f, 1f, 0f);
            for (var y = 0; y < h; y++)
            {
                var t = y / (float)(h - 1);
                var half = Mathf.Lerp(2f, w * 0.45f, t);
                for (var x = 0; x < w; x++)
                {
                    var dx = Mathf.Abs(x - (w - 1) * 0.5f);
                    tex.SetPixel(x, y, dx <= half ? Color.white : clear);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0f), 100f);
        }
    }
}
