using System;

namespace WontFix
{
    // Pure math, no UnityEngine dependency, so it can be unit-tested without
    // spinning up a scene. Numbers follow Cookie Clicker's known-good pacing:
    // ~1.15x cost growth per purchase.
    public static class Economy
    {
        public const double CostGrowth = 1.15;

        static readonly string[] Suffixes =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"
        };

        public static double CostOf(double baseCost, int owned)
        {
            return baseCost * Math.Pow(CostGrowth, owned);
        }

        public static double Accrue(double perSecond, double seconds)
        {
            return perSecond * seconds;
        }

        public static string Format(double value)
        {
            if (value < 1000)
                return value.ToString("0.0");

            var tier = 0;
            while (value >= 1000 && tier < Suffixes.Length - 1)
            {
                value /= 1000;
                tier++;
            }

            return value.ToString("0.00") + " " + Suffixes[tier];
        }
    }
}
