using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Arena
{
    /// <summary>
    /// GDD §13 (Decided): using a shield never triggers the electric wall. Only the player's backward swipe counts, so the
    /// item held, being pushed back by a block or standing behind a shield can never count as retreating.
    /// </summary>
    public sealed class ShieldAndElectricWallTests
    {
        [Test]
        public void OnlyABackwardSwipe_CountsAsRetreating_WhateverTheItemHeld()
        {
            foreach (WeaponStats weapon in GddStartingValues.CreateWeapons())
            {
                foreach (BodyMove move in (BodyMove[])Enum.GetValues(typeof(BodyMove)))
                {
                    var plan = new TurnPlan(weapon.WeaponId, move, WeaponPath.Empty, default, true);

                    Assert.That(plan.BodyMove.IsBackward(), Is.EqualTo(move == BodyMove.StepBack), $"{weapon.Id} with {move}");
                }
            }
        }

        [Test]
        public void TheWall_IsToldOnlyThePlannedBodyMove_NeverTheItemHeld()
        {
            MethodInfo trigger = typeof(ElectricWallRules).GetMethod(nameof(ElectricWallRules.OnPlansLocked));

            Type[] inputs = trigger.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

            Assert.That(inputs, Does.Contain(typeof(BodyMove)));
            Assert.That(inputs, Has.None.EqualTo(typeof(WeaponId)).And.None.EqualTo(typeof(WeaponStats)).And.None.EqualTo(typeof(TurnPlan)),
                "the shield cannot reach the wall's trigger");
        }
    }
}
