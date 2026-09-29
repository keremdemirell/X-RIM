using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode
{
    /// <summary>Shared builders for test inputs.</summary>
    internal static class TestData
    {
        public const uint Seed = 12345u;

        public static MatchSetup CreateMatchSetup() => new MatchSetup(
            new PerSide<FighterSetup>(
                new FighterSetup(Handedness.Right, new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }),
                new FighterSetup(Handedness.Left, new[] { WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield })),
            Seed);

        public static BoardSnapshot CreateInitialBoard(RulesSettings settings) =>
            new BoardSnapshot(MatchState.CreateInitial(CreateMatchSetup(), settings), new PoseSnapshot());
    }
}
