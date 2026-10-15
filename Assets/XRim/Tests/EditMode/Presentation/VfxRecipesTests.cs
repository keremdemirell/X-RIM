using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Vfx;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// What each event sprays (Session 13; GDD §2, Decided: bolts, nuts, springs, sparks and hydraulic fluid, never blood): sparks by a
    /// hit's damage, a shower on a rebound, a thud on a crush, rings on blocks, a burst and a jet from a cut joint, a bigger burst with
    /// steam from a splitting torso, arcs on a wall zap, dust under a body move.
    /// </summary>
    public sealed class VfxRecipesTests
    {
        private static readonly SimTime Time = SimTime.FromSeconds(0.3);
        private static readonly Vec2 Point = new Vec2(300f, 120f);

        private readonly List<VfxBurst> _bursts = new List<VfxBurst>();
        private readonly List<VfxArc> _arcs = new List<VfxArc>();

        private static VfxSettings Settings() => new VfxSettings
        {
            SeverDebrisPieces = 10, SeverFluidDrops = 8, SeverSprings = 3, SeverGears = 1, DeathBurstMultiplier = 2f, DeathSteamPuffs = 5,
            DebrisSpeedUnitsPerSecond = 420f, DebrisSizeUnits = 9f, FluidDropSizeUnits = 7f, HitSparksBase = 6, HitSparksPerDamage = 0.5f,
            MaxHitSparks = 24, HeavyHitMinDamage = 15f, HeavyHitDebrisPieces = 3, ClashSparks = 24, CrushDebrisPieces = 4,
            SparkSpeedUnitsPerSecond = 650f, SparkSeconds = 0.28f, SparkSizeUnits = 7f,
        };

        [SetUp]
        public void SetUp()
        {
            _bursts.Clear();
            _arcs.Clear();
        }

        [Test]
        public void AHit_ThrowsSparksByItsDamage_OnThroughTheStruckPart_WithAFlash()
        {
            Add(Hit(Side.Left, 8f));

            VfxBurst sparks = Single(VfxParticleKind.Spark);
            Assert.That(sparks.Count, Is.EqualTo(10), "6 plus half of 8");
            Assert.That(sparks.DirectionDegrees, Is.EqualTo(20f), "onward from the left attacker, a little up");
            Assert.That(sparks.StyleSide, Is.EqualTo(Side.Left), "in the attacker's hit-effect set");
            Assert.That(Single(VfxParticleKind.Flash).Count, Is.EqualTo(1));
            Assert.That(Of(VfxParticleKind.Bolt), Is.Empty, "a light hit knocks nothing loose");
        }

        [Test]
        public void AHeavyHit_AlsoKnocksBoltsAndNutsLoose_AndTheSparksAreCapped()
        {
            Add(Hit(Side.Right, 80f));

            Assert.That(Single(VfxParticleKind.Spark).Count, Is.EqualTo(24));
            Assert.That(Single(VfxParticleKind.Spark).DirectionDegrees, Is.EqualTo(160f), "onward from the right attacker");
            Assert.That(Single(VfxParticleKind.Bolt).Count + Single(VfxParticleKind.Nut).Count, Is.EqualTo(3));
        }

        [Test]
        public void Clashes_Rebound_InAShower_Crush_WithAThud_Slide_WithAFewSkiddingSparks()
        {
            Add(Clash(ClashKind.BothRebound, null));
            Assert.That(Single(VfxParticleKind.Spark).Count, Is.EqualTo(24));
            Assert.That(Single(VfxParticleKind.Spark).DirectionDegrees, Is.EqualTo(90f), "showering up");

            SetUp();
            Add(Clash(ClashKind.CrushThrough, Side.Right));
            Assert.That(Single(VfxParticleKind.Ring).Color, Is.EqualTo(VfxColorRole.Dust), "the shock ring");
            Assert.That(Single(VfxParticleKind.Bolt).Count + Single(VfxParticleKind.Nut).Count, Is.EqualTo(4));
            Assert.That(Single(VfxParticleKind.Spark).StyleSide, Is.EqualTo(Side.Right), "in the winner's set");

            SetUp();
            Add(Clash(ClashKind.BothSlidePast, null));
            Assert.That(Single(VfxParticleKind.Spark).Count, Is.EqualTo(8));
            Assert.That(Of(VfxParticleKind.Ring), Is.Empty);
        }

        [Test]
        public void AFullBlock_RingsBrightOnTheShield_APartialBlockSmallAndOrange()
        {
            Add(Block(true));
            VfxBurst full = Single(VfxParticleKind.Ring);
            Assert.That(full.Color, Is.EqualTo(VfxColorRole.FullBlock));
            Assert.That(full.OriginUnits, Is.EqualTo(Point));

            SetUp();
            Add(Block(false));
            VfxBurst partial = Single(VfxParticleKind.Ring);
            Assert.That(partial.Color, Is.EqualTo(VfxColorRole.PartialBlock));
            Assert.That(partial.SizeUnits, Is.LessThan(full.SizeUnits));
            Assert.That(Single(VfxParticleKind.Spark).Count, Is.LessThan(10));
        }

        [Test]
        public void ASever_BurstsBoltsNutsSpringsAndAGear_FromTheJoint_AndJetsFluidBackward()
        {
            Add(new LimbSeveredEvent(Time, Side.Left, BodyPart.LeftArm));

            Vec2 shoulder = new Vec2(0f, 100f);
            Assert.That(Single(VfxParticleKind.Bolt).Count + Single(VfxParticleKind.Nut).Count, Is.EqualTo(10));
            Assert.That(Single(VfxParticleKind.Spring).Count, Is.EqualTo(3));
            Assert.That(Single(VfxParticleKind.Gear).Count, Is.EqualTo(1));
            VfxBurst jet = Single(VfxParticleKind.Drop);
            Assert.That(jet.Count, Is.EqualTo(8));
            Assert.That(jet.Color, Is.EqualTo(VfxColorRole.Fluid), "hydraulic fluid, never blood");
            Assert.That(jet.DirectionDegrees, Is.GreaterThan(90f), "the left dummy's jet leaves backward (to the left) and up");
            Assert.That(jet.OriginUnits, Is.EqualTo(shoulder), "from the joint");
            Assert.That(jet.LifetimeSeconds, Is.EqualTo(0f), "the fluid stays on the floor");
        }

        [Test]
        public void ADeath_BurstsMore_WithSteam()
        {
            Add(new FighterDiedEvent(Time, Side.Right));

            Assert.That(Single(VfxParticleKind.Bolt).Count + Single(VfxParticleKind.Nut).Count, Is.EqualTo(20), "twice a sever's");
            Assert.That(Single(VfxParticleKind.Drop).Count, Is.EqualTo(16));
            Assert.That(Single(VfxParticleKind.Puff).Count, Is.EqualTo(5));
            Assert.That(Single(VfxParticleKind.Puff).Color, Is.EqualTo(VfxColorRole.Steam));
            Assert.That(Single(VfxParticleKind.Drop).OriginUnits, Is.EqualTo(new Vec2(600f, 60f)), "from the torso's middle");
        }

        [Test]
        public void AWallZap_ArcsFromTheWallIntoTheDummy()
        {
            Add(new ElectricWallEvent(Time, Side.Right, ElectricWallPhase.Active, 10f, 700f));

            Assert.That(_arcs, Has.Count.EqualTo(3));
            Assert.That(_arcs[0].FromUnits, Is.EqualTo(new Vec2(700f, 60f)), "the wall's face at the torso's height");
            Assert.That(_arcs[0].ToUnits, Is.EqualTo(new Vec2(600f, 60f)));
            Assert.That(Single(VfxParticleKind.Spark).Color, Is.EqualTo(VfxColorRole.Zap));
        }

        [Test]
        public void AWarning_CracklesUpTheWall()
        {
            Add(new ElectricWallEvent(Time, Side.Left, ElectricWallPhase.Warning, 0f, -500f));

            Assert.That(_arcs, Has.Count.EqualTo(2));
            Assert.That(_arcs.All(arc => arc.FromUnits.X == -500f && arc.ToUnits.X == -500f), Is.True);
        }

        [Test]
        public void ABodyMove_KicksUpDustUnderTheDummy()
        {
            Add(new BodyMoveStartedEvent(SimTime.Zero, Side.Right, BodyMove.Lunge));

            VfxBurst dust = Single(VfxParticleKind.Puff);
            Assert.That(dust.Color, Is.EqualTo(VfxColorRole.Dust));
            Assert.That(dust.OriginUnits, Is.EqualTo(new Vec2(600f, 0f)), "on the floor under the pelvis");
        }

        [Test]
        public void OtherEvents_SprayNothing()
        {
            Add(new StatusAppliedEvent(Time, Side.Right, StatusKind.Stunned));
            Add(new AttackInterruptedEvent(Time, Side.Right));

            Assert.That(_bursts, Is.Empty);
            Assert.That(_arcs, Is.Empty);
        }

        [Test]
        public void NothingEverSpraysBlood()
        {
            Add(Hit(Side.Left, 40f));
            Add(new LimbSeveredEvent(Time, Side.Right, BodyPart.RightLeg));
            Add(new FighterDiedEvent(Time, Side.Right));

            Assert.That(System.Enum.GetNames(typeof(VfxColorRole)), Has.None.Contains("Blood"), "there is no such colour to spray");
            Assert.That(_bursts.Where(burst => burst.Kind == VfxParticleKind.Drop).Select(burst => burst.Color), Has.All.EqualTo(VfxColorRole.Fluid),
                "every drop is hydraulic fluid");
        }

        private void Add(MatchEvent matchEvent) => VfxRecipes.Add(matchEvent, Turn(matchEvent), Settings(), _bursts, _arcs);

        private List<VfxBurst> Of(VfxParticleKind kind) => _bursts.Where(burst => burst.Kind == kind).ToList();

        private VfxBurst Single(VfxParticleKind kind) => Of(kind).Single();

        private static HitLandedEvent Hit(Side attacker, float damage) => new HitLandedEvent(Time,
            new HitFacts(attacker, BodyPart.Torso, WeaponIds.Mace, Time, false), new DamageResult(damage, damage, 0f, false, false));

        private static WeaponClashEvent Clash(ClashKind kind, Side? winner) =>
            new WeaponClashEvent(Time, new ClashResult(kind, winner, 60f, 3f, 12f), Point);

        private static ShieldBlockEvent Block(bool full) =>
            new ShieldBlockEvent(Time, Side.Right, new BlockResult(full, full ? 0f : 0.5f, false), Point, 80f, 0.1f);

        /// <summary>A turn whose pose has the left dummy at x 0 (shoulder at 100), the right at 600 (neck at 120).</summary>
        private static TurnResult Turn(MatchEvent matchEvent)
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            BoardSnapshot board = TestData.CreateInitialBoard(settings);
            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            pose.Left.Set(BodyPart.Head, new BodyPose(new Vec2(0f, 120f), 0f));
            pose.Left.Set(BodyPart.LeftArm, new BodyPose(new Vec2(0f, 100f), 0f));
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(600f, 0f), 0f));
            pose.Right.Set(BodyPart.Head, new BodyPose(new Vec2(600f, 120f), 0f));
            var recorder = new TimelineRecorder();
            recorder.RecordFrame(0, SimTime.Zero, pose);
            recorder.RecordFrame(1, SimTime.FromSeconds(1.0), pose);
            recorder.RecordEvent(matchEvent);
            return new TurnResult(0, recorder.Build(), board, new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false));
        }
    }
}
