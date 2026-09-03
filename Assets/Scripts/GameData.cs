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
        public string id;
        public double baseCost;
        public double bugsPerSecond;

        public Generator(string id, double baseCost, double bugsPerSecond)
        {
            this.id = id;
            this.baseCost = baseCost;
            this.bugsPerSecond = bugsPerSecond;
        }
    }

    // Content lives here, in code, not in a ScriptableObject asset -- a
    // ScriptableObject would mean hand-filling 12 rows in the Inspector,
    // and nobody can touch the Inspector here. Display text (name/flavor)
    // lives in Localization.cs, keyed by id, so balance changes here never
    // touch translated content and vice versa.
    public static class GameData
    {
        public static readonly Generator[] Generators =
        {
            new Generator("junior_tester", 15, 0.1),
            new Generator("test_case_spreadsheet", 100, 1),
            new Generator("selenium_script", 1100, 8),
            new Generator("ci_pipeline", 12000, 47),
            new Generator("load_test_rig", 130000, 260),
            new Generator("fuzzer", 1400000, 1400),
            new Generator("static_analyzer", 20000000, 7800),
            new Generator("monkey_test_farm", 330000000, 44000),
            new Generator("llm_test_agent", 5100000000, 260000),
            new Generator("chaos_engineering_cluster", 75000000000, 1600000),
            new Generator("formal_verification_lab", 1000000000000, 10000000),
            new Generator("production_users", 14000000000000, 65000000),
        };
    }
}
