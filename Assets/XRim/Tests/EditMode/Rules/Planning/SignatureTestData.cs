using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Rules.Signature;

namespace XRim.Tests.EditMode.Rules.Planning
{
    /// <summary>
    /// A small signature catalogue for the planning tests (not the placeholder moves of the game; those are in the starting values).
    /// The weapon tip rests at (100, 100) and the shoulder at (0, 100), so a preset along y = 100 runs straight out from the shoulder.
    /// </summary>
    internal static class SignatureTestData
    {
        public static readonly SignatureMoveId Jab = new SignatureMoveId("test_jab");
        public static readonly SignatureMoveId Sweep = new SignatureMoveId("test_sweep");
        public static readonly SignatureMoveId PaidSweep = new SignatureMoveId("test_paid_sweep");
        public static readonly SignatureMoveId MaceOnly = new SignatureMoveId("test_mace_only");
        public static readonly SignatureMoveId LungeJab = new SignatureMoveId("test_lunge_jab");

        /// <summary>200 units straight out; any weapon; ignores ink.</summary>
        public static SignatureMoveStats CreateJab() => Move(Jab, Line(150f, 350f));

        /// <summary>1800 units out and back: three times a rapier's whole ink. Ignores ink, so it all plays.</summary>
        public static SignatureMoveStats CreateSweep() => Move(Sweep, OutAndBack());

        /// <summary>The same pattern, but it pays the weapon's ink like a drawn path.</summary>
        public static SignatureMoveStats CreatePaidSweep()
        {
            SignatureMoveStats move = Move(PaidSweep, OutAndBack());
            move.ConsumesInk = true;
            return move;
        }

        public static SignatureMoveStats CreateMaceOnly()
        {
            SignatureMoveStats move = Move(MaceOnly, Line(150f, 300f));
            move.AllowedWeaponIds.Add(WeaponIds.Mace.Value);
            return move;
        }

        /// <summary>Defines its own body move: a lunge.</summary>
        public static SignatureMoveStats CreateLungeJab()
        {
            SignatureMoveStats move = Move(LungeJab, Line(150f, 350f));
            move.OverridesBodyMove = true;
            move.BodyMoveOverride = BodyMove.Lunge;
            return move;
        }

        /// <summary>The starting values with all five test moves in the catalogue.</summary>
        public static RulesSettings CreateSettings()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.SignatureMoves.Clear();
            settings.SignatureMoves.AddRange(new[]
            {
                CreateJab(), CreateSweep(), CreatePaidSweep(), CreateMaceOnly(), CreateLungeJab(),
            });
            return settings;
        }

        /// <summary>A charge that allows a move now.</summary>
        public static SignatureChargeState Charged => new SignatureChargeState(true, 1f);

        private static SignatureMoveStats Move(SignatureMoveId id, List<Vec2> path) => new SignatureMoveStats
        {
            Id = id.Value, DisplayName = id.Value, PresetPath = path,
        };

        private static List<Vec2> Line(float fromX, float toX) => new List<Vec2> { new Vec2(fromX, 100f), new Vec2(toX, 100f) };

        private static List<Vec2> OutAndBack() => new List<Vec2>
        {
            new Vec2(150f, 100f), new Vec2(600f, 100f), new Vec2(150f, 100f), new Vec2(600f, 100f), new Vec2(150f, 100f),
        };
    }
}
