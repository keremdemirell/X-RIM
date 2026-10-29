using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XRim.Networking.Protocol;
using XRim.Rules.Events;
using XRim.Rules.Planning;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Scenarios;

namespace XRim.Tests.EditMode.Networking.Protocol
{
    /// <summary>
    /// The protocol keeps up with the game (Session 19): every event and planning command the game defines can be sent, every message kind
    /// travels one way only, and the data shapes the codec writes field by field have not gained a field it would silently drop. A failure
    /// here names what to add to the codec (and to raise <see cref="ProtocolCodec.CurrentVersion"/>).
    /// </summary>
    public sealed class ProtocolCoverageTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void EveryEventTheGameDefines_CanBeSent()
        {
            IEnumerable<Type> events = ConcreteSubclasses(typeof(MatchEvent), typeof(MatchEvent).Assembly, typeof(ContactEvent).Assembly);

            foreach (Type type in events)
            {
                Assert.That(ProtocolCodec.SupportedEventTypes, Does.Contain(type), $"{type.Name} has no protocol form: add it to EventCodec.");
            }
        }

        [Test]
        public void EveryPlanningCommand_CanBeSent()
        {
            foreach (Type type in ConcreteSubclasses(typeof(PlanningCommand), typeof(PlanningCommand).Assembly))
            {
                Assert.That(ProtocolCodec.SupportedCommandTypes, Does.Contain(type), $"{type.Name} has no protocol form: add it to CommandCodec.");
            }
        }

        [Test]
        public void EveryKind_TravelsOneWay_AndClientsSendOnlyTheirInputs()
        {
            var sentByClient = Enum.GetValues(typeof(MessageKind)).Cast<MessageKind>().Where(MessageKinds.IsSentByClient).ToList();

            Assert.That(Enum.GetValues(typeof(MessageKind)).Cast<MessageKind>().All(MessageKinds.IsKnown), Is.True);
            Assert.That(sentByClient, Is.EquivalentTo(new[]
            {
                MessageKind.ClientHello, MessageKind.LoadoutPick, MessageKind.PlanningCommand, MessageKind.PlaybackFinished, MessageKind.Ping,
            }));
            Assert.That(MessageKinds.IsSentByClient(MessageKind.TurnResolved), Is.False, "a client can never send a turn's result");
        }

        [TestCase(typeof(MatchStateData), "TurnIndex IsSuddenDeath SuddenDeathRepeats Left Right LeftWall RightWall")]
        [TestCase(typeof(FighterStateData), "Handedness Hp HpBeforeSuddenDeath CurrentWeapon HpDamageTaken LifestealHitCount LifestealDamageDealt " +
                                            "HitLandedThisTurn SignatureCharge SignatureUses ConsecutiveIdleTurns LimbDamage Severed Statuses " +
                                            "HasHeldLimb HeldLimbOwner HeldLimbPart LostWeapons")]
        [TestCase(typeof(StatusEffectData), "Kind Effect RemainingTurns Fraction")]
        [TestCase(typeof(ElectricWallStateData), "Phase IsUp PositionUnits ConsecutiveRetreats")]
        [TestCase(typeof(PoseSnapshot), "Left Right SeveredLimbs LeftWall RightWall")]
        [TestCase(typeof(FighterPose), "Parts LowerSegments HasLowerSegments HeldItem HasHeldItem DroppedItem HasDroppedItem IsTorsoSplit TorsoTop SeveredParts")]
        [TestCase(typeof(SeveredLimbPose), "Owner Part Pose Lower HasLower")]
        [TestCase(typeof(ElectricWallPose), "IsUp Phase XUnits")]
        [TestCase(typeof(BodyPose), "PositionUnits RotationDegrees")]
        public void TheDataShapesTheCodecWrites_HaveNoNewField(Type type, string fields)
        {
            IEnumerable<string> actual = type.GetFields(Instance).Select(field => field.Name);

            Assert.That(actual, Is.EquivalentTo(fields.Split(' ')),
                $"{type.Name} changed: add its new field to the protocol (BoardCodec / TimelineCodec) and raise ProtocolCodec.CurrentVersion.");
        }

        [Test]
        public void ThePlanningLimits_HaveNoNewState()
        {
            IEnumerable<string> fields = typeof(PlanningConstraints).GetFields(Instance).Where(field => !field.Name.Contains("k__BackingField"))
                .Select(field => field.Name);
            IEnumerable<string> settable = typeof(PlanningConstraints).GetProperties().Where(property => property.CanWrite).Select(property => property.Name);

            Assert.That(fields, Is.EquivalentTo(new[] { "_forbiddenBodyMoves", "_forbiddenWeapons", "_extraWeapons", "_statuses", "_allBodyMovesForbidden" }),
                "PlanningConstraints changed: send its new state in BoardCodec.WriteConstraints and raise ProtocolCodec.CurrentVersion.");
            Assert.That(settable, Is.EquivalentTo(new[]
            {
                "PlanningDurationSeconds", "InkLengthMultiplier", "CanSwitchWeapon", "CanUseSignature", "CanUseLoadoutWeapons",
            }), "PlanningConstraints changed: send its new setting in BoardCodec.WriteConstraints and raise ProtocolCodec.CurrentVersion.");
        }

        private static IEnumerable<Type> ConcreteSubclasses(Type baseType, params Assembly[] assemblies) =>
            assemblies.Distinct().SelectMany(assembly => assembly.GetTypes()).Where(type => !type.IsAbstract && baseType.IsAssignableFrom(type));
    }
}
