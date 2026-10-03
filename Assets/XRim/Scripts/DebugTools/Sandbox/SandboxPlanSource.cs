using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;

namespace XRim.DebugTools.Sandbox
{
    /// <summary>
    /// One side of the debug sandbox: the mouse plans for it through the <see cref="SandboxController"/>, and every choice
    /// goes to the authority as the same <see cref="PlanningCommand"/> real touch input will send (Session 08), so the rules
    /// (ink, reach, switching erases the path, Ready) apply exactly as in a match.
    /// </summary>
    internal sealed class SandboxPlanSource : IPlanSource
    {
        private IPlanningCommandSink _sink;

        public Side Side { get; }

        /// <summary>The planning phase in progress; null between phases.</summary>
        public PlanningWindow Window { get; private set; }

        public bool IsPlanning => Window != null;

        /// <summary>The weapon this side will execute with (the board's weapon until switched).</summary>
        public WeaponId Weapon { get; private set; }

        /// <summary>The raw stroke last accepted, in the torso frame; empty when none.</summary>
        public WeaponPath Stroke { get; private set; } = WeaponPath.Empty;

        /// <summary>The body move set for this turn (GDD §5); none until one is picked.</summary>
        public BodyMove BodyMove { get; private set; }

        /// <summary>The last command the rules refused and why; empty when the last one was accepted.</summary>
        public string LastRejection { get; private set; } = string.Empty;

        public SandboxPlanSource(Side side)
        {
            Side = side;
        }

        public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink)
        {
            Window = Guard.NotNull(window, nameof(window));
            _sink = Guard.NotNull(sink, nameof(sink));
            Weapon = window.Board.State.Fighters[Side].CurrentWeapon;
            Stroke = WeaponPath.Empty;
            BodyMove = BodyMove.None;
            LastRejection = string.Empty;
        }

        public void OnPlanningEnded()
        {
            Window = null;
            _sink = null;
        }

        public void SelectWeapon(WeaponId weapon)
        {
            if (Send(new SelectWeaponCommand(weapon), nameof(SelectWeapon)))
            {
                Weapon = weapon;
                Stroke = WeaponPath.Empty; // GDD §6 (Decided): switching erases the drawn path
            }
        }

        public void SetPath(WeaponPath stroke)
        {
            if (Send(new SetPathCommand(stroke), nameof(SetPath))) Stroke = stroke;
        }

        public void ClearPath()
        {
            if (Send(new ClearPathCommand(), nameof(ClearPath))) Stroke = WeaponPath.Empty;
        }

        /// <summary>The stance swipe a finger will make in the body zone (Session 08); <see cref="BodyMove.None"/> clears it.</summary>
        public void SetBodyMove(BodyMove move)
        {
            if (Send(new SetBodyMoveCommand(move), nameof(SetBodyMove))) BodyMove = move;
        }

        public void Ready() => Send(new SetReadyCommand(true), nameof(Ready));

        private bool Send(PlanningCommand command, string what)
        {
            if (_sink == null) return false;
            CommandResult result = _sink.Send(command);
            LastRejection = result.Accepted ? string.Empty : $"{what} refused: {result.Rejection}";
            return result.Accepted;
        }
    }
}
