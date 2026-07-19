namespace Bayou.Fishing
{
    /// <summary>
    /// Global "player is mid throw-net minigame" flag so the character motor can freeze movement
    /// while Move input is reused for wiggle / reel.
    /// </summary>
    public static class FishingActivity
    {
        public static bool IsBusy { get; private set; }

        public static void SetBusy(bool busy) => IsBusy = busy;
    }
}
