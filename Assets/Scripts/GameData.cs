using System;

namespace WontFix
{
    [Serializable]
    public struct SaveData
    {
        public double bugs;
        public double lifetimeBugs;
        public int[] owned;
        public long lastSeenUnixSeconds;
    }

    [Serializable]
    public struct Generator
    {
        public string name;
        public string flavor;
        public double baseCost;
        public double bugsPerSecond;

        public Generator(string name, string flavor, double baseCost, double bugsPerSecond)
        {
            this.name = name;
            this.flavor = flavor;
            this.baseCost = baseCost;
            this.bugsPerSecond = bugsPerSecond;
        }
    }

    // Content lives here, in code, not in a ScriptableObject asset -- a
    // ScriptableObject would mean hand-filling 12 rows in the Inspector,
    // and Claude can't touch the Inspector.
    public static class GameData
    {
        public static readonly Generator[] Generators =
        {
            new Generator("Junior Tester", "Clicks every button. Occasionally reads the spec.", 15, 0.1),
            new Generator("Test Case Spreadsheet", "400 rows. Last updated 2019.", 100, 1),
            new Generator("Selenium Script", "Flaky, but it runs. Mostly.", 1100, 8),
            new Generator("CI Pipeline", "Runs on every commit. Red on every commit.", 12000, 47),
            new Generator("Load Test Rig", "Finds bugs by asking \"what if 10,000 users?\"", 130000, 260),
            new Generator("Fuzzer", "Throws garbage at it until something screams.", 1400000, 1400),
            new Generator("Static Analyzer", "4,000 warnings. Three of them matter.", 20000000, 7800),
            new Generator("Monkey Test Farm", "Literal monkeys. Literal tablets. Great coverage.", 330000000, 44000),
            new Generator("LLM Test Agent", "Writes brilliant tests for features you don't have.", 5100000000, 260000),
            new Generator("Chaos Engineering Cluster", "Breaks production on purpose, for science.", 75000000000, 1600000),
            new Generator("Formal Verification Lab", "Proves mathematically that the bug exists.", 1000000000000, 10000000),
            new Generator("Production Users", "The finest test environment ever devised.", 14000000000000, 65000000),
        };
    }
}
