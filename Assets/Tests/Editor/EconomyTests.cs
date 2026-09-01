using NUnit.Framework;
using WontFix;

namespace WontFix.Tests
{
    public class EconomyTests
    {
        [Test]
        public void CostOf_FirstPurchase_EqualsBaseCost()
        {
            Assert.AreEqual(15.0, Economy.CostOf(15, 0), 0.0001);
        }

        [Test]
        public void CostOf_GrowsByFifteenPercentPerOwned()
        {
            Assert.AreEqual(17.25, Economy.CostOf(15, 1), 0.0001);
            Assert.AreEqual(19.8375, Economy.CostOf(15, 2), 0.0001);
        }

        [Test]
        public void Accrue_MultipliesRateByElapsedSeconds()
        {
            Assert.AreEqual(50.0, Economy.Accrue(10, 5), 0.0001);
        }

        [Test]
        public void Format_BelowThousand_ShowsOneDecimal()
        {
            Assert.AreEqual("15.0", Economy.Format(15));
            Assert.AreEqual("0.1", Economy.Format(0.1));
        }

        [Test]
        public void Format_UsesSuffixesAboveThousand()
        {
            Assert.AreEqual("1.23 K", Economy.Format(1234));
            Assert.AreEqual("1.10 M", Economy.Format(1_100_000));
        }
    }
}
