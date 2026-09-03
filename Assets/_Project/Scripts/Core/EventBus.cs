using System;
using System.Collections.Generic;

namespace Crossroads.Core
{
    /// <summary>
    /// Minimal typed pub/sub (GAME_DESIGN §13.4 - "typed EventBus"; no DI framework).
    /// Pure C# so it is headless-testable. Subscribers must unsubscribe on destroy
    /// (UI boots subscribe in Awake / unsubscribe in OnDestroy).
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Table = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            Type t = typeof(T);
            Delegate existing;
            Table.TryGetValue(t, out existing);
            Table[t] = existing == null ? (Delegate)handler : Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            Type t = typeof(T);
            Delegate existing;
            if (!Table.TryGetValue(t, out existing)) return;
            Delegate combined = Delegate.Remove(existing, handler);
            if (combined == null) Table.Remove(t); else Table[t] = combined;
        }

        public static void Publish<T>(T evt)
        {
            Delegate existing;
            if (!Table.TryGetValue(typeof(T), out existing)) return;
            Action<T> action = existing as Action<T>;
            if (action != null) action(evt);
        }

        public static void Clear() { Table.Clear(); }

        public static int SubscriberCount<T>()
        {
            Delegate existing;
            return Table.TryGetValue(typeof(T), out existing) && existing != null ? existing.GetInvocationList().Length : 0;
        }
    }
}
