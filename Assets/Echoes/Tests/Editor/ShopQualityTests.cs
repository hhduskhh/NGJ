using NUnit.Framework;
using UnityEngine;

namespace Echoes.Tests
{
    public sealed class ShopQualityTests
    {
        [TestCase(1, 65, 25, 8, 2)]
        [TestCase(5, 65, 25, 8, 2)]
        [TestCase(6, 60, 27, 10, 3)]
        [TestCase(10, 60, 27, 10, 3)]
        [TestCase(11, 55, 29, 12, 4)]
        [TestCase(66, 0, 51, 34, 15)]
        [TestCase(1000, 0, 51, 34, 15)]
        public void LevelThresholdsAndSaturation(int level, float white, float blue, float purple, float red)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var weights = ShopQualityRules.Weights(config, level);
                CollectionAssert.AreEqual(new[] { white, blue, purple, red }, weights);
                CollectionAssert.AreEqual(new[] {65f, 25, 8, 2}, config.shopTierWeights, "Rolling cannot mutate shared configuration");
                Assert.AreEqual(100, weights[0] + weights[1] + weights[2] + weights[3]);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test] public void SelectionUsesExactQualityIntervalsAndEligibleWeights()
        {
            var weights = new[] {65f, 25, 8, 2};
            // A midpoint in each hundredth of [0,1) must reproduce the percentages exactly.
            var counts = new int[4];
            for (int i = 0; i < 100; i++) counts[ShopQualityRules.Select(weights, (i + .5) / 100)]++;
            CollectionAssert.AreEqual(new[] {65, 25, 8, 2}, counts);
            var eligible = new[] {false, true, false, true};
            Assert.AreEqual(1, ShopQualityRules.Select(weights, .92, eligible));
            Assert.AreEqual(3, ShopQualityRules.Select(weights, .93, eligible));
            Assert.AreEqual(-1, ShopQualityRules.Select(weights, .5, new bool[4]));
        }
    }
}
