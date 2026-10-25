using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using XRim.Economy;

namespace XRim.Tests.EditMode.Economy
{
    /// <summary>
    /// GDD §16 (Decided): premium currency can never be converted into earned currency, nothing that affects combat can be bought with real
    /// money, and earned currency is never sold for real money (§20, Rejected). The code has no such path; these tests fail if one is added.
    /// </summary>
    public sealed class NoConversionTests
    {
        private static readonly Assembly EconomyAssembly = typeof(Wallet).Assembly;
        private const BindingFlags AllPublicMethods = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void NoPublicMethodMentionsBothCurrencies()
        {
            var offenders = new List<string>();
            foreach (Type type in EconomyAssembly.GetExportedTypes())
            {
                foreach (MethodInfo method in type.GetMethods(AllPublicMethods))
                {
                    var mentioned = new HashSet<Type>();
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        Collect(parameter.ParameterType, mentioned);
                    }

                    Collect(method.ReturnType, mentioned);
                    if (mentioned.Contains(typeof(EarnedAmount)) && mentioned.Contains(typeof(PremiumAmount))) offenders.Add(type.Name + "." + method.Name);
                }
            }

            Assert.That(offenders, Is.Empty, "A method that takes one currency and gives the other (or both together) is a conversion path (§16)");
        }

        [Test]
        public void NeitherCurrencyHasAConversionOperator()
        {
            foreach (Type currency in new[] { typeof(EarnedAmount), typeof(PremiumAmount) })
            {
                foreach (MethodInfo method in currency.GetMethods(AllPublicMethods))
                {
                    Assert.That(method.Name, Is.Not.EqualTo("op_Implicit").And.Not.EqualTo("op_Explicit"),
                        currency.Name + " must not convert to or from anything");
                }
            }
        }

        [Test]
        public void Wallet_EveryOperationTouchesOneBalance()
        {
            foreach (MethodInfo method in typeof(Wallet).GetMethods(AllPublicMethods))
            {
                if (method.IsSpecialName || method.Name == nameof(object.GetType)) continue;
                int currencyParameters = 0;
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    if (parameter.ParameterType == typeof(EarnedAmount) || parameter.ParameterType == typeof(PremiumAmount)) currencyParameters++;
                }

                Assert.That(currencyParameters, Is.EqualTo(1), "Wallet." + method.Name + " must take exactly one amount of one currency");
                Assert.That(method.ReturnType, Is.EqualTo(typeof(void)).Or.EqualTo(typeof(bool)),
                    "Wallet." + method.Name + " returns no currency, so it cannot hand out the other one");
            }
        }

        [Test]
        public void WalletBalances_NeverMoveIntoEachOther()
        {
            var wallet = new Wallet();
            wallet.Add(new PremiumAmount(500));
            wallet.TrySpend(new PremiumAmount(200));
            wallet.TrySpend(new EarnedAmount(1));

            Assert.That(wallet.Earned.Value, Is.EqualTo(0L));
            Assert.That(wallet.Premium.Value, Is.EqualTo(300L));

            wallet.Add(new EarnedAmount(40));
            wallet.TrySpend(new EarnedAmount(15));

            Assert.That(wallet.Earned.Value, Is.EqualTo(25L));
            Assert.That(wallet.Premium.Value, Is.EqualTo(300L));
        }

        [Test]
        public void GameplayUnlock_HasNoPremiumPrice()
        {
            Assert.That(typeof(GameplayUnlock).GetProperty(nameof(GameplayUnlock.Price)).PropertyType, Is.EqualTo(typeof(EarnedAmount)));
            foreach (MemberInfo member in typeof(GameplayUnlock).GetMembers(AllPublicMethods))
            {
                Assert.That(MemberTypeOf(member), Is.Not.EqualTo(typeof(PremiumAmount)), "GameplayUnlock." + member.Name);
            }

            Assert.That(typeof(UnlockableEntry).GetField("PremiumPrice"), Is.Null, "The shop's unlockable data has no premium price either");
        }

        [Test]
        public void Shop_SellsGameplayUnlocksForEarnedCurrencyOnly()
        {
            foreach (MethodInfo method in typeof(Shop).GetMethods(AllPublicMethods))
            {
                bool takesUnlock = false;
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    takesUnlock |= parameter.ParameterType == typeof(GameplayUnlock);
                }

                if (!takesUnlock) continue;
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    Assert.That(parameter.ParameterType, Is.Not.EqualTo(typeof(PremiumAmount)).And.Not.EqualTo(typeof(CurrencyKind)),
                        "Shop." + method.Name + " lets a gameplay unlock be paid in another currency");
                }
            }
        }

        [Test]
        public void NothingInEconomySellsEarnedCurrencyForRealMoney()
        {
            foreach (Type type in EconomyAssembly.GetExportedTypes())
            {
                string name = type.Name;
                Assert.That(name, Does.Not.Contain("Exchange"), name);
                Assert.That(name, Does.Not.Contain("Convert"), name);
                foreach (MethodInfo method in type.GetMethods(AllPublicMethods))
                {
                    Assert.That(method.Name, Does.Not.Contain("Exchange").And.Not.Contain("Convert").And.Not.Contain("BuyEarned"), type.Name + "." + method.Name);
                }
            }
        }

        private static void Collect(Type type, ISet<Type> into)
        {
            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) type = underlying;
            if (type.HasElementType) Collect(type.GetElementType(), into);
            into.Add(type);
            if (!type.IsGenericType) return;
            foreach (Type argument in type.GetGenericArguments())
            {
                Collect(argument, into);
            }
        }

        private static Type MemberTypeOf(MemberInfo member)
        {
            switch (member)
            {
                case PropertyInfo property: return property.PropertyType;
                case FieldInfo field: return field.FieldType;
                case MethodInfo method: return method.ReturnType;
                default: return typeof(void);
            }
        }
    }
}
