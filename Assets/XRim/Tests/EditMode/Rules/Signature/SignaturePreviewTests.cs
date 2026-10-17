using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Signature
{
    /// <summary>
    /// A tapped signature move draws itself on the screen (GDD §8): the preview is exactly the path the rules will execute, and a move
    /// that ignores ink leaves the ink meter full.
    /// </summary>
    public sealed class SignaturePreviewTests
    {
        private RulesSettings _settings;
        private PathPreviewBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _builder = new PathPreviewBuilder(new RulePolicies());
        }

        [Test]
        public void ThePreview_IsThePathTheSessionWillExecute()
        {
            PlanningSession session = PlanningTestData.CreateSession(_settings, signatureCharge: SignatureTestData.Charged);
            session.Apply(new UseSignatureCommand(SignatureMoveIds.WhiplashCalibrationSpin), 1.0);
            SignatureMoveStats move = _settings.FindSignatureMove(SignatureMoveIds.WhiplashCalibrationSpin);

            PathPreview preview = _builder.BuildSignature(move, PlanningTestData.Tip, _settings.FindWeapon(WeaponIds.Rapier), _settings.Paths);

            WeaponPath executed = session.CurrentPlan.Path;
            Assert.That(preview.Path.Points.Count, Is.EqualTo(executed.Points.Count));
            for (int i = 0; i < executed.Points.Count; i++)
            {
                Assert.That(Vec2.Distance(preview.Path.Points[i], executed.Points[i]), Is.LessThan(1e-3f), $"point {i}");
            }
        }

        [Test]
        public void AMoveThatIgnoresInk_LeavesTheInkMeterFull()
        {
            SignatureMoveStats move = _settings.FindSignatureMove(SignatureMoveIds.WhiplashCalibrationSpin);

            PathPreview preview = _builder.BuildSignature(move, PlanningTestData.Tip, _settings.FindWeapon(WeaponIds.Rapier), _settings.Paths);

            Assert.That(preview.InkRemainingFraction, Is.EqualTo(1f));
            Assert.That(preview.WasCutByInk, Is.False);
            Assert.That(preview.Path.LengthUnits, Is.GreaterThan(_settings.FindWeapon(WeaponIds.Rapier).InkLengthUnits), "longer than the ink, still whole");
        }

        [Test]
        public void ThePreview_ShowsThePatternAsItsStroke()
        {
            SignatureMoveStats move = _settings.FindSignatureMove(SignatureMoveIds.FiveFingerExplodingTouch);

            PathPreview preview = _builder.BuildSignature(move, PlanningTestData.Tip, _settings.FindWeapon(WeaponIds.Mace), _settings.Paths);

            Assert.That(preview.Stroke.Points, Is.EqualTo(move.PresetPath));
            Assert.That(preview.WasClampedByReach, Is.True, "the mace's shorter reach clamps a pattern built for the rapier (D4)");
        }
    }
}
