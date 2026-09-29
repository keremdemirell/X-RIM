using XRim.Rules.Paths;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Everything one player set for a turn (GDD §3, Decided): a weapon, an optional body move, and a
    /// drawn path or a signature move. Whatever is set when the timer ends still executes.
    /// </summary>
    public sealed class TurnPlan
    {
        public WeaponId Weapon { get; }
        public BodyMove BodyMove { get; }

        /// <summary>Empty when nothing was drawn.</summary>
        public WeaponPath Path { get; }

        /// <summary>Set when a signature move replaces the drawn path (GDD §8).</summary>
        public SignatureMoveId Signature { get; }

        public bool PressedReady { get; }

        public TurnPlan(WeaponId weapon, BodyMove bodyMove, WeaponPath path, SignatureMoveId signature, bool pressedReady)
        {
            Weapon = weapon;
            BodyMove = bodyMove;
            Path = path ?? WeaponPath.Empty;
            Signature = signature;
            PressedReady = pressedReady;
        }

        /// <summary>A turn with nothing set: the dummy holds its pose (GDD §3).</summary>
        public static TurnPlan Empty(WeaponId weapon) =>
            new TurnPlan(weapon, BodyMove.None, WeaponPath.Empty, default, false);
    }
}
