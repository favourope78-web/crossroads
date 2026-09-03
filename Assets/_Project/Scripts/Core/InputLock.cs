namespace Crossroads.Core
{
    /// <summary>
    /// Global gameplay-input gate. Locked while dialogue/decisions are on screen
    /// (GAME_DESIGN §4.5: choice UI pauses the gameplay camera/world input, not the world).
    /// Pure C# - PlayerPrototypeController and PlayerInteraction both poll it.
    /// </summary>
    public static class InputLock
    {
        public static bool Active { get; private set; }
        public static string Reason { get; private set; }

        public static void Set(bool locked, string reason)
        {
            if (!locked) reason = ""; // unlocking always clears the reason
            if (Active == locked && Reason == reason) return;
            Active = locked;
            Reason = reason;
            EventBus.Publish(new InputLockEvent { locked = locked, reason = reason });
        }

        public static void Clear()
        {
            Active = false;
            Reason = "";
        }
    }
}
