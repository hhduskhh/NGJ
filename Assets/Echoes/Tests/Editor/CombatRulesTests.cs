using NUnit.Framework;
using UnityEngine;

namespace Echoes.Tests
{
    public sealed class CombatRulesTests
    {
        [TestCase(false, true, 1, false)]
        [TestCase(true, false, 1, false)]
        [TestCase(true, true, 6, false)]
        [TestCase(true, true, 5, true)]
        public void PressureOnlyCountsNearbyLivingAwakeEnemies(bool alive, bool awake, float distance, bool expected)
        {
            Assert.AreEqual(expected, CombatRules.CountsForPressure(alive, awake,
                new Vector2(distance, 0), Vector2.zero, 5));
        }
        [Test]
        public void PressureBonusHasUpperBound()
        {
            Assert.AreEqual(1.64f, CombatRules.DamageMultiplier(100, 8, .08f), .0001f);
            Assert.AreEqual(1, CombatRules.DamageMultiplier(0, 8, .08f));
        }
        [Test]
        public void LevelCostGrowsPredictably()
        { Assert.AreEqual(4, CombatRules.ExperienceRequired(1, 4, 3)); Assert.AreEqual(10, CombatRules.ExperienceRequired(3, 4, 3)); }
        [Test]
        public void ThreeChoicesNeverRepeat()
        {
            var rng = new System.Random(713);
            for (int i = 0; i < 50; i++)
            { var choices = UpgradeDefinition.Roll(rng); Assert.AreEqual(3, choices.Count); CollectionAssert.AllItemsAreUnique(choices); }
        }
    }
}
