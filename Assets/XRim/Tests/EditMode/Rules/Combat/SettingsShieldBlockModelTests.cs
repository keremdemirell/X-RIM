using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Combat
{
    /// <summary>
    /// The shield's block rule (GDD §7): every D20 option, every D21 option, and the edges of "square" and "rim". Tests on the
    /// edges use a rim of 0.25, so the face ends exactly at 0.75.
    /// </summary>
    public sealed class SettingsShieldBlockModelTests
    {
        private const float Tolerance = 1e-4f;
        private const float StraightIn = 90f;
        private const float Glancing = 10f;
        private const float MiddleOfTheFace = 0f;
        private const float OnTheRim = 0.95f;

        private readonly SettingsShieldBlockModel _model = new SettingsShieldBlockModel();
        private ShieldBlockSettings _settings;
        private WeaponStats _rapier;
        private WeaponStats _mace;
        private WeaponStats _shield;

        [SetUp]
        public void SetUp()
        {
            _settings = new ShieldBlockSettings();
            _rapier = GddStartingValues.Rapier();
            _mace = GddStartingValues.Mace();
            _shield = GddStartingValues.Shield();
        }

        private BlockResult Block(float angle, float facePosition, WeaponStats weapon = null) =>
            _model.Resolve(new BlockFacts(angle, facePosition, weapon ?? _rapier, (weapon ?? _rapier).SpeedUnitsPerSecond, _shield), _settings);

        private void AssertFull(BlockResult result)
        {
            Assert.That(result.AttackStopped, Is.True, "a full block");
            Assert.That(result.DamageMultiplier, Is.EqualTo(0f));
        }

        private void AssertPartial(BlockResult result)
        {
            Assert.That(result.AttackStopped, Is.False, "a partial block: the weapon carries on");
            Assert.That(result.DamageMultiplier, Is.EqualTo(_settings.PartialBlockDamageMultiplier).Within(Tolerance));
        }

        // --- D20 default: full square-on to the face, partial otherwise -------------------------------

        [Test]
        public void D20Default_ASquareHitOnTheFace_IsAFullBlock()
        {
            Assert.That(new RulePolicies().ShieldBlock, Is.InstanceOf<SettingsShieldBlockModel>());
            Assert.That(_settings.Rule, Is.EqualTo(ShieldBlockRule.SquareFaceFullOtherwisePartial));

            AssertFull(Block(StraightIn, MiddleOfTheFace));
        }

        [Test]
        public void D20Default_AHitOnTheRim_IsAPartialBlock()
        {
            AssertPartial(Block(StraightIn, OnTheRim));
            AssertPartial(Block(StraightIn, 1.3f));
        }

        [Test]
        public void D20Default_AGlancingHit_IsAPartialBlock_EvenOnTheFace()
        {
            AssertPartial(Block(Glancing, MiddleOfTheFace));
        }

        [Test]
        public void D20Default_ThePartialReduction_IsHalf_AndTunable()
        {
            Assert.That(_settings.PartialBlockDamageMultiplier, Is.EqualTo(0.5f));
            _settings.PartialBlockDamageMultiplier = 0.25f;

            Assert.That(Block(Glancing, MiddleOfTheFace).DamageMultiplier, Is.EqualTo(0.25f).Within(Tolerance));
        }

        [TestCase(30f, true)]
        [TestCase(29.99f, false)]
        public void Square_StartsAtExactlyTheSquareHitAngle(float angle, bool full)
        {
            Assert.That(_settings.SquareHitMinAngleDegrees, Is.EqualTo(30f));

            Assert.That(Block(angle, MiddleOfTheFace).AttackStopped, Is.EqualTo(full));
        }

        [TestCase(0.7499f, true)]
        [TestCase(0.75f, false)]
        public void TheRim_IncludesItsInnerEdge(float facePosition, bool full)
        {
            _settings.RimFraction = 0.25f;

            Assert.That(Block(StraightIn, facePosition).AttackStopped, Is.EqualTo(full));
        }

        // --- D20 alternatives ------------------------------------------------------------------

        [Test]
        public void AlwaysFull_BlocksRimAndGlancingHitsToo()
        {
            _settings.Rule = ShieldBlockRule.AlwaysFull;

            AssertFull(Block(Glancing, OnTheRim));
        }

        [Test]
        public void AlwaysPartial_OnlyReducesEvenASquareFaceHit()
        {
            _settings.Rule = ShieldBlockRule.AlwaysPartial;

            AssertPartial(Block(StraightIn, MiddleOfTheFace));
        }

        // --- D21 ---------------------------------------------------------------------------

        [Test]
        public void D21Default_AMaceIsBlockedLikeAnyWeapon_AndStaggersNobody()
        {
            Assert.That(_settings.HeavyWeapon, Is.EqualTo(HeavyWeaponBlockRule.None));

            BlockResult result = Block(StraightIn, MiddleOfTheFace, _mace);

            AssertFull(result);
            Assert.That(result.ShieldHolderStaggered, Is.False);
        }

        [Test]
        public void D21StaggersHolder_AHeldBlockAgainstAMace_StaggersTheHolder_ButNotAgainstARapier()
        {
            _settings.HeavyWeapon = HeavyWeaponBlockRule.StaggersHolder;

            Assert.That(Block(StraightIn, MiddleOfTheFace, _mace).ShieldHolderStaggered, Is.True);
            Assert.That(Block(StraightIn, MiddleOfTheFace, _rapier).ShieldHolderStaggered, Is.False);
            Assert.That(Block(StraightIn, OnTheRim, _mace).ShieldHolderStaggered, Is.False, "only when the block holds");
        }

        [Test]
        public void D21BreaksThrough_AMaceIsOnlyPartiallyBlocked_EvenSquareOn_ARapierStillFully()
        {
            _settings.HeavyWeapon = HeavyWeaponBlockRule.BreaksThrough;

            AssertPartial(Block(StraightIn, MiddleOfTheFace, _mace));
            AssertFull(Block(StraightIn, MiddleOfTheFace, _rapier));
        }

        [TestCase(10f, true)]
        [TestCase(9.99f, false)]
        public void D21_HeavyStartsAtExactlyTheHeavyWeaponMass(float mass, bool heavy)
        {
            _settings.HeavyWeapon = HeavyWeaponBlockRule.BreaksThrough;
            var weapon = new WeaponStats { Id = "test", Mass = mass, SpeedUnitsPerSecond = 300f };

            Assert.That(Block(StraightIn, MiddleOfTheFace, weapon).AttackStopped, Is.EqualTo(!heavy));
        }
    }
}
