using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Planning
{
    /// <summary>
    /// Builders for planning tests. Planning lasts 12 s with a 1.5 s lock-out, so the lock-out starts at 10.5 s.
    /// The shoulder is at (0, 100) and the weapon tip rests at (100, 100): strokes along y = 100 run straight
    /// out from the shoulder.
    /// </summary>
    internal static class PlanningTestData
    {
        public const double DeadlineSeconds = 12.0;
        public const double LockoutStartSeconds = 10.5;

        public static readonly Vec2 Tip = new Vec2(100f, 100f);
        public static readonly WeaponId[] Loadout = { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield };

        public static PlanningSession CreateSession(RulesSettings settings = null, PlanningConstraints constraints = null,
            RulePolicies policies = null)
        {
            settings = settings ?? GddStartingValues.CreateRulesSettings();
            constraints = constraints ?? PlanningConstraints.CreateDefault(settings.Match);
            return new PlanningSession(Side.Left, WeaponIds.Rapier, Loadout, new FixedWeaponTipSource(Tip), constraints,
                DeadlineSeconds, settings, policies ?? new RulePolicies());
        }

        /// <summary>A straight stroke along y = 100 from x = 150 to <paramref name="toX"/>.</summary>
        public static WeaponPath Thrust(float toX) => new WeaponPath(new[] { new Vec2(150f, 100f), new Vec2(toX, 100f) });

        /// <summary>Out to x = 600, back to 150, out again: well over any rapier budget.</summary>
        public static WeaponPath ThereAndBackAgain() => new WeaponPath(new[]
        {
            new Vec2(150f, 100f), new Vec2(600f, 100f), new Vec2(150f, 100f), new Vec2(600f, 100f),
        });

        public static Vec2 End(WeaponPath path) => path.Points[path.Points.Count - 1];
    }
}
