using System;
using System.Collections.Generic;

namespace Crossroads.Core
{
    /// <summary>Minimal service lifecycle contract (GAME_DESIGN §13.4).</summary>
    public interface IGameService
    {
        void Init();
        void Shutdown();
    }

    /// <summary>
    /// Lightweight service locator (design: no DI framework, headless-testable).
    /// Crossroads.Narrative.GameServices is the strongly-typed app facade on top of this.
    /// </summary>
    public static class AppServices
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) return;
            Services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            object o;
            return Services.TryGetValue(typeof(T), out o) ? o as T : null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            object o;
            if (Services.TryGetValue(typeof(T), out o) && o is T)
            {
                service = (T)o;
                return true;
            }
            service = null;
            return false;
        }

        public static bool Has<T>() where T : class { return Services.ContainsKey(typeof(T)); }

        public static void Clear() { Services.Clear(); }
    }
}
