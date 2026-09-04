using System.Collections.Generic;
namespace Crossroads.Gameplay.Input
{
    /// <summary>
    /// "Is the player in a fight right now?" - one predicate, reused by the control rig
    /// (combat buttons appear only when appropriate), tests, and future audio states.
    /// A fight means: at least one REGISTERED enemy that is alive, activated and on screen
    /// (dormant story-gated wardens don't count; wreckage doesn't count).
    /// </summary>
    public static class CombatPresence
    {
        public static bool HasLiveEnemy(IReadOnlyList<EnemyAgent> agents)
        {
            if (agents == null) return false;
            for (int i = 0; i < agents.Count; i++)
            {
                EnemyAgent a = agents[i];
                if (a == null || a.IsDefeated) continue;
                if (!a.isActiveAndEnabled) continue;
                return true;
            }
            return false;
        }
    }
}
