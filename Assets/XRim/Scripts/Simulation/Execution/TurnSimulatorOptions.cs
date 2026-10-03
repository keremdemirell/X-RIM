using XRim.Simulation.Drivers;

namespace XRim.Simulation.Execution
{
    /// <summary>Optional strategies for a <see cref="TurnSimulator"/>. Every default is the current game.</summary>
    public sealed class TurnSimulatorOptions
    {
        /// <summary>How a weapon or club lies along its path. Default: aim from the shoulder (designer, 2026-09-29).</summary>
        public IWeaponAimModel Aim { get; set; } = new AimFromShoulderModel();

        /// <summary>How the shield lies along its path. Default: held like a shield, face outward (designer, 2026-10-03, A3).</summary>
        public IWeaponAimModel ShieldAim { get; set; } = new ShieldFaceAimModel();

        /// <summary>Plays each planned body move. Default: the game's moves (GDD §5), each played from its tuning data.</summary>
        public IBodyMoveDriverFactory BodyMoves { get; set; } = new StanceBodyMoveDriverFactory();

        /// <summary>Decides what contacts mean. Default: the hit, clash and block rules (Sessions 06 and 07).</summary>
        public ITurnContactHandler Contacts { get; set; } = new HitContactHandler();
    }
}
