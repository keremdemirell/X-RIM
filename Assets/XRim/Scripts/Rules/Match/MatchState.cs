using XRim.Core;
using XRim.Rules.Arena;
using XRim.Rules.Settings;

namespace XRim.Rules.Match
{
    /// <summary>
    /// Everything the rules track between turns. Together with the simulation's PoseSnapshot it forms the
    /// frozen board that players plan from (GDD §3 stance persistence).
    /// </summary>
    public sealed class MatchState
    {
        /// <summary>0-based index of the current turn.</summary>
        public int TurnIndex { get; set; }

        public bool IsSuddenDeath { get; set; }
        public PerSide<FighterState> Fighters { get; }
        public PerSide<ElectricWallState> Walls { get; }

        public MatchState(PerSide<FighterState> fighters, PerSide<ElectricWallState> walls)
        {
            Fighters = Guard.NotNull(fighters, nameof(fighters));
            Walls = Guard.NotNull(walls, nameof(walls));
        }

        public static MatchState CreateInitial(MatchSetup setup, RulesSettings settings)
        {
            var fighters = PerSide<FighterState>.Create(side =>
            {
                FighterSetup fighter = setup.Fighters[side];
                WeaponId firstWeapon = fighter.Loadout.Count > 0 ? fighter.Loadout[0] : default;
                return new FighterState(fighter.Handedness, settings.Damage.MaxHp, firstWeapon);
            });
            var walls = PerSide<ElectricWallState>.Create(_ => new ElectricWallState());
            return new MatchState(fighters, walls);
        }

        public MatchState Clone() =>
            new MatchState(
                PerSide<FighterState>.Create(side => Fighters[side].Clone()),
                PerSide<ElectricWallState>.Create(side => Walls[side].Clone()))
            {
                TurnIndex = TurnIndex,
                IsSuddenDeath = IsSuddenDeath,
            };
    }
}
