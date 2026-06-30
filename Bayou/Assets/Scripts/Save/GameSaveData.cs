using System;

namespace Bayou.Save
{
    [Serializable]
    public sealed class SavedItemEntry
    {
        public string itemId;
        public string instanceId;
        public string compartmentId;
        public int gridX;
        public int gridY;
        public int rotation;
        public int stackCount = 1;
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public int version = 1;
        public string sceneName;
        public string lastBonfireId;
        public float playerX;
        public float playerY;
        public float playerZ;
        public float playerRotY;
        public int walletBalance;
        public SavedItemEntry[] inventoryItems = Array.Empty<SavedItemEntry>();
    }
}
