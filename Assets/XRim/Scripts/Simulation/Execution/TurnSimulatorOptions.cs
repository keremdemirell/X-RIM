using XRim.Simulation.Drivers;

namespace XRim.Simulation.Execution
{
    /// <summary>Optional strategies for a <see cref="TurnSimulator"/>. Every default is the current game.</summary>
    public sealed class TurnSimulatorOptions
    {
        /// <summary>How the weapon lies along its path. Default: aim from the shoulder (designer, 2026-09-29).</summary>
        public IWeaponAimModel Aim { get; set; } = new AimFromShoulderModel();

        /// <summary>Plays each planned body move. Default: the game's moves (GDD §5), each played from its tuning data.</summary>
        public IBodyMoveDriverFactory BodyMoves { get; set; } = new StanceBodyMoveDriverFactory();

        /// <summary>Decides what contacts mean. Default: record them raw (Sessions 06 and 07 add the rules).</summary>
        public ITurnContactHandler Contacts { get; set; } = new RecordContactsHandler();
    }
}
