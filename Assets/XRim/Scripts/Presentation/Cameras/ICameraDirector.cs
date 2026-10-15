using XRim.Core;
using XRim.Core.Gdd;
using XRim.Presentation.Playback;
using XRim.Rules.Arena;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Cameras
{
    /// <summary>
    /// Frames the duel and reacts to it: shake on impacts, a punch-in on big moments (GDD §2 camera behaviour is TBD; the fixed side
    /// view stays the default, Session 13). It never moves while a player plans, so touches always map to the arena the player sees.
    /// </summary>
    [GddTbd("§2", "Camera behaviour", Proposal = "Fixed side view")]
    public interface ICameraDirector : IMatchEventReactor
    {
        /// <summary>Flips the arena view so the local player's own dummy appears on their side of the screen (GDD §4).</summary>
        void SetArenaFlipped(bool flipped);

        /// <summary>The arena's edges this match (a framing never shows far past them).</summary>
        void SetArena(ArenaEdges edges);

        /// <summary>Frames a frozen board at once (match start, planning): nothing moves while a player plans.</summary>
        void ShowBoard(PoseSnapshot pose);

        /// <summary>A turn starts playing: it is framed from its first pose, and the camera settles on its last pose's framing once playback reaches the end.</summary>
        void BeginTurn(TurnResult turn);

        /// <summary>True while the camera still moves (settling on the next framing, shaking, punching in): playback holds the turn until it stops.</summary>
        bool IsSettling { get; }

        /// <summary>
        /// The instant replay closes in on its moment (Session 13): the view eases down to this share of its size around the point (canonical
        /// arena units) over this many real seconds, and stays there until the next board or turn.
        /// </summary>
        void FocusOn(Vec2 pointUnits, float viewFraction, float seconds);
    }
}
