using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bayou.Inventory.Shop
{
    public enum ShopBagRole
    {
        Player,
        Merchant
    }

    [Serializable]
    public sealed class ItemPlacementSnapshot
    {
        public string instanceId;
        public ShopBagRole owner;
        public string compartmentId;
        public int gridX;
        public int gridY;
        public int rotation;
    }

    /// <summary>
    /// Tracks RE-style pending deal: items moved between player and merchant bags update net price until Close Deal.
    /// </summary>
    public sealed class ShopTransaction
    {
        private readonly Dictionary<string, ShopBagRole> _originalOwners = new();
        private readonly List<ItemPlacementSnapshot> _originalPlacements = new();

        public int TotalBuyCost { get; private set; }
        public int TotalSellCredit { get; private set; }
        public int NetCost => TotalBuyCost - TotalSellCredit;

        public event Action DealChanged;

        public void BeginSession(
            InventoryBagModel playerBag,
            InventoryBagModel merchantBag)
        {
            _originalOwners.Clear();
            _originalPlacements.Clear();
            SnapshotBag(playerBag, ShopBagRole.Player);
            SnapshotBag(merchantBag, ShopBagRole.Merchant);
            Recalculate(playerBag, merchantBag);
        }

        public void Recalculate(InventoryBagModel playerBag, InventoryBagModel merchantBag)
        {
            TotalBuyCost = 0;
            TotalSellCredit = 0;

            foreach (var item in playerBag.AllItems)
            {
                if (item?.definition == null) continue;
                if (GetOriginalOwner(item.instanceId) == ShopBagRole.Merchant)
                    TotalBuyCost += Mathf.Max(0, item.definition.buyPrice);
            }

            foreach (var item in merchantBag.AllItems)
            {
                if (item?.definition == null) continue;
                if (GetOriginalOwner(item.instanceId) == ShopBagRole.Player)
                    TotalSellCredit += Mathf.Max(0, item.definition.sellPrice);
            }

            DealChanged?.Invoke();
        }

        public bool CanCloseDeal(PlayerWallet wallet)
        {
            if (wallet == null) return NetCost <= 0;
            return wallet.CanAfford(NetCost);
        }

        public bool TryCloseDeal(PlayerWallet wallet)
        {
            if (!CanCloseDeal(wallet)) return false;
            if (NetCost > 0)
                wallet.TrySpend(NetCost);
            else if (NetCost < 0)
                wallet.Add(-NetCost);
            return true;
        }

        public void RevertToSnapshot(
            InventoryBagModel playerBag,
            InventoryBagModel merchantBag,
            InventoryBagModel scratchBag)
        {
            ClearBag(playerBag);
            ClearBag(merchantBag);

            foreach (var snap in _originalPlacements)
            {
                var item = FindItem(scratchBag, snap.instanceId);
                if (item == null) continue;

                scratchBag.Remove(item);
                var target = snap.owner == ShopBagRole.Player ? playerBag : merchantBag;
                item.rotation = snap.rotation;
                if (!target.TryPlace(item, snap.compartmentId, snap.gridX, snap.gridY, snap.rotation))
                    target.TryAddItem(item.definition, snap.rotation, out _);
            }
        }

        public void CaptureAllItems(InventoryBagModel playerBag, InventoryBagModel merchantBag, InventoryBagModel scratchBag)
        {
            ClearBag(scratchBag);
            MoveAll(playerBag, scratchBag);
            MoveAll(merchantBag, scratchBag);
        }

        private void SnapshotBag(InventoryBagModel bag, ShopBagRole role)
        {
            foreach (var item in bag.AllItems)
            {
                if (item?.definition == null) continue;
                _originalOwners[item.instanceId] = role;
                _originalPlacements.Add(new ItemPlacementSnapshot
                {
                    instanceId = item.instanceId,
                    owner = role,
                    compartmentId = item.compartmentId,
                    gridX = item.gridX,
                    gridY = item.gridY,
                    rotation = item.rotation
                });
            }
        }

        private ShopBagRole GetOriginalOwner(string instanceId) =>
            _originalOwners.TryGetValue(instanceId, out var role) ? role : ShopBagRole.Merchant;

        private static InventoryItemInstance FindItem(InventoryBagModel bag, string instanceId)
        {
            foreach (var item in bag.AllItems)
            {
                if (item != null && item.instanceId == instanceId)
                    return item;
            }
            return null;
        }

        private static void ClearBag(InventoryBagModel bag)
        {
            var copy = new List<InventoryItemInstance>(bag.AllItems);
            foreach (var item in copy)
                bag.Remove(item);
        }

        private static void MoveAll(InventoryBagModel from, InventoryBagModel to)
        {
            var copy = new List<InventoryItemInstance>(from.AllItems);
            foreach (var item in copy)
            {
                from.DetachFromGrid(item);
                from.Remove(item);
                to.HoldItem(item);
            }
        }
    }
}
