using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Movement/pose sink the NPC behaviour logic drives. The Unity implementation lives in
    /// NpcAgent (transform + optional animator); tests inject a fake to assert the behaviour
    /// headlessly. Keeps NpcLogic free of engine types.
    /// </summary>
    public interface INpcWorld
    {
        Point3 NpcPosition { get; }
        void NpcMoveTowards(Point3 target, float speed, float dt);
        void NpcFaceTowards(Point3 target, float turnSpeed, float dt);
    }

    /// <summary>The NPC's current visible behaviour state (GAME_DESIGN §9.2 behaviour profiles).</summary>
    public enum NpcMoodState
    {
        Idle,        // nothing to do - stands still
        RoutineWalk, // walking the routine loop
        Dwell,       // resting at a routine stop (dwell timer)
        Approach,    // walking toward the player (Friendly / Curious)
        Avoid,       // stepping back from the player (Wary)
        ReactFace,   // standing, turned to face the player
        Talk         // conversation active: frozen, facing the player
    }

    /// <summary>Resolved behaviour numbers (base behaviour + active fate-state overrides).</summary>
    public struct NpcProfile
    {
        public bool facesPlayer;
        public float reactRadius;
        public float approach;      // >0: walk toward player when idle & in react radius
        public float avoid;         // >0: back off if the player is closer than this
        public float talkDistance;  // stop approaching at this distance
        public float moveSpeed;
        public float turnSpeed;
    }

    /// <summary>
    /// Pure-C# NPC behaviour FSM (idle / walking / talking / routine / reacting to the player).
    /// One tick = one decision; movement is applied through INpcWorld so the whole thing is
    /// headless-testable and cheap (a couple of distance comparisons per NPC per frame -
    /// mobile-friendly; no allocations, no raycasts, no pathfinding).
    ///
    /// Personality presets (NpcPersonality) map to the numbers in NpcProfile:
    ///   Friendly  -> approach > 0, avoid = 0   (Mara)
    ///   Wary      -> approach = 0, avoid > 0   (Sera, before the tide path)
    ///   Curious   -> approach > 0, avoid = 0, taller talkDistance
    ///   Reserved  -> approach = 0, avoid = 0 (only faces the player)
    /// A fate-state (NpcBrain) can flip those numbers live (Sera: avoid -> approach).
    /// </summary>
    public class NpcLogic
    {
        public const float ArrivalEpsilon = 0.2f;
        private const float AvoidPadding = 0.35f;

        private readonly System.Collections.Generic.List<NpcStopData> _routine;
        private int _stopIndex;
        private float _dwellTimer;
        private Point3 _target;
        private int _arrivals;

        public NpcMoodState State { get; private set; }
        public bool Moving { get { return State == NpcMoodState.RoutineWalk || State == NpcMoodState.Approach || State == NpcMoodState.Avoid; } }
        public Point3 Target { get { return _target; } }
        public float PlayerDistance { get; private set; }
        /// <summary>Routine waypoint completions (tests + debug: cycle counter).</summary>
        public int Arrivals { get { return _arrivals; } }

        public NpcLogic(System.Collections.Generic.List<NpcStopData> routine)
        {
            _routine = routine ?? new System.Collections.Generic.List<NpcStopData>();
            _stopIndex = 0;
        }

        /// <summary>Re-routes after a fate-state change (cancels any in-flight walking target).</summary>
        public void Reset()
        {
            State = NpcMoodState.Idle;
            _dwellTimer = 0f;
        }

        /// <summary>
        /// One behaviour step. talking=true freezes everything (dialogue lock - GAME_DESIGN §4.5);
        /// playerActive=false means the player is out of the world (no reaction, back to routine).
        /// </summary>
        public void Tick(INpcWorld world, float dt, Point3 playerPos, bool playerActive, NpcProfile profile, bool talking)
        {
            if (world == null) return;

            Point3 self = world.NpcPosition;
            PlayerDistance = Point3.Distance(self, playerPos);

            if (talking)
            {
                State = NpcMoodState.Talk;
                if (profile.facesPlayer) world.NpcFaceTowards(playerPos, profile.turnSpeed, dt);
                return;
            }

            bool playerNear = playerActive && PlayerDistance < profile.reactRadius;

            // ---- social reaction (the player is close enough to matter) ----
            if (playerNear)
            {
                if (profile.avoid > 0f && PlayerDistance < profile.avoid)
                {
                    // Wary: step back, keeping the comfort distance
                    State = NpcMoodState.Avoid;
                    Point3 dir = (self - playerPos).normalized;
                    Point3 retreat = self + dir * (profile.avoid - PlayerDistance + AvoidPadding);
                    Move(world, dt, retreat, profile.moveSpeed, playerPos, profile, faceWhileMoving: true);
                    return;
                }
                if (profile.approach > 0f && PlayerDistance > profile.talkDistance)
                {
                    // Friendly/Curious: walk up, stop at talking distance
                    State = NpcMoodState.Approach;
                    Move(world, dt, playerPos, profile.moveSpeed, playerPos, profile, faceWhileMoving: true);
                    return;
                }
                if (profile.facesPlayer)
                {
                    State = NpcMoodState.ReactFace;
                    world.NpcFaceTowards(playerPos, profile.turnSpeed, dt);
                    return;
                }
                State = NpcMoodState.Idle;
                return;
            }

            // ---- routine (player away; daily loop) ----
            if (_routine != null && _routine.Count > 0)
            {
                TickRoutine(world, dt, profile);
                return;
            }

            State = NpcMoodState.Idle;
        }

        private void TickRoutine(INpcWorld world, float dt, NpcProfile profile)
        {
            NpcStopData stop = _routine[_stopIndex];
            Point3 stopPos = stop.position;

            switch (State)
            {
                case NpcMoodState.RoutineWalk:
                    if (Point3.Distance(world.NpcPosition, stopPos) <= ArrivalEpsilon)
                    {
                        _arrivals++;
                        _dwellTimer = stop.dwellSeconds > 0f ? stop.dwellSeconds : 0f;
                        State = _dwellTimer > 0f ? NpcMoodState.Dwell : NextStop();
                    }
                    else
                    {
                        Move(world, dt, stopPos, profile.moveSpeed, stopPos, profile, faceWhileMoving: true);
                    }
                    break;

                case NpcMoodState.Dwell:
                    _dwellTimer -= dt;
                    if (_dwellTimer <= 0f) State = NextStop();
                    break;

                default:
                    State = NpcMoodState.RoutineWalk;
                    break;
            }
        }

        private NpcMoodState NextStop()
        {
            _stopIndex = (_stopIndex + 1) % _routine.Count;
            return NpcMoodState.RoutineWalk;
        }

        private void Move(INpcWorld world, float dt, Point3 target, float speed, Point3 faceTarget,
                          NpcProfile profile, bool faceWhileMoving)
        {
            _target = target;
            if (Point3.Distance(world.NpcPosition, target) > ArrivalEpsilon)
            {
                world.NpcMoveTowards(target, speed > 0f ? speed : 1f, dt);
                if (faceWhileMoving) world.NpcFaceTowards(faceTarget, profile.turnSpeed, dt);
            }
        }
    }
}
