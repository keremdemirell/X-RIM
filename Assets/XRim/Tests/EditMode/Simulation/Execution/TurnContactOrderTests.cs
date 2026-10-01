using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>ARCHITECTURE §6: contacts are handled in time order, then by a stable id that does not depend on the engine.</summary>
    public sealed class TurnContactOrderTests
    {
        private static readonly BodyTag LeftTorso = new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.Torso);
        private static readonly BodyTag LeftHead = new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.Head);
        private static readonly BodyTag LeftWeapon = new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso);
        private static readonly BodyTag RightTorso = new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Torso);
        private static readonly BodyTag Edge = new BodyTag(null, BodyRole.ArenaEdge, BodyPart.Torso);

        private static TurnContact Contact(long microseconds, BodyTag a, BodyTag b, int reportedStep = 1) =>
            new TurnContact(new ContactFacts(a, b, Vec2.Zero, Vec2.UnitX, Vec2.Zero), reportedStep, new SimTime(microseconds), null, 0f, 0f);

        [Test]
        public void EarlierTime_ComesFirstWhateverTheBodies()
        {
            Assert.That(TurnContactOrder.Compare(Contact(10, Edge, RightTorso), Contact(20, LeftHead, RightTorso)), Is.LessThan(0));
        }

        [Test]
        public void SameTime_OrdersByOwnerThenRoleThenPart()
        {
            Assert.That(TurnContactOrder.Compare(LeftTorso, RightTorso), Is.LessThan(0), "left before right");
            Assert.That(TurnContactOrder.Compare(RightTorso, Edge), Is.LessThan(0), "fighters before the arena");
            Assert.That(TurnContactOrder.Compare(LeftTorso, LeftWeapon), Is.LessThan(0), "body parts before held items");
            Assert.That(TurnContactOrder.Compare(LeftHead, LeftTorso), Is.LessThan(0), "head before torso");
        }

        [Test]
        public void SameFirstBody_OrdersByTheSecond()
        {
            Assert.That(TurnContactOrder.Compare(Contact(10, LeftWeapon, LeftHead), Contact(10, LeftWeapon, RightTorso)), Is.LessThan(0));
        }

        [Test]
        public void Sort_IsStableForEqualContacts()
        {
            var contacts = new List<TurnContact>
            {
                Contact(30, LeftTorso, RightTorso, 1),
                Contact(10, LeftWeapon, RightTorso, 2),
                Contact(30, LeftTorso, RightTorso, 3),
                Contact(10, LeftHead, RightTorso, 4),
            };

            TurnContactOrder.Sort(contacts);

            Assert.That(contacts.ConvertAll(c => c.ReportedStep), Is.EqualTo(new[] { 4, 2, 1, 3 }));
        }
    }
}
