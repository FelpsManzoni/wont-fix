# Unlockable Generators & Upgrades Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retrofit the 12 existing generators to be hidden until unlocked (own ≥1 of
the previous generator reveals the next), and add 4 upgrades on the same unlock
rule — one-off boosts to the click value or a specific generator's output.

**Architecture:** Unlock/multiplier math moves into a new pure file, `Progression.cs`,
mirroring `Economy.cs`'s separation of testable logic from the `Game`/`GameUI`
MonoBehaviours. `GameData.cs` gains the `Upgrade` content array alongside the
existing `Generator` array. `GameUI.cs`'s row list becomes conditionally visible
(`SetActive` toggled every `Refresh()`) rather than static.

**Tech Stack:** Unity 6000.5.9f1, C#, `UnityEngine.UI` (legacy `Text`), NUnit via
`com.unity.test-framework`.

**Spec:** `docs/superpowers/specs/2026-09-02-unlockable-generators-and-upgrades-design.md`

## Global Constraints

- No Unity Editor GUI authoring anywhere in this plan — every change is a text file
  edit. Verification is either the in-Editor Test Runner (Window > General > Test
  Runner > EditMode > Run All) or the headless batchmode command in `CLAUDE.md`.
- Two assemblies: gameplay in `WontFix` (`Assets/Scripts/WontFix.asmdef`), tests in
  `WontFix.Tests` (`Assets/Tests/Editor/WontFix.Tests.asmdef`, references `WontFix`).
  New files in this plan go in `Assets/Scripts/` or `Assets/Tests/Editor/`.
- Generator unlocking needs zero new save state — `generator[i]` is visible when
  `i == 0 || Owned[i-1] >= 1`, derived from data that only ever increases. Upgrade
  purchases are new state: `SaveData.upgradesPurchased` (`bool[]`), same
  save/load treatment as `SaveData.owned`.
- `Upgrade.generatorId` does double duty: it gates the upgrade's own unlock (own ≥1
  of that generator) AND, for `UpgradeEffect.Generator` upgrades, names the generator
  it boosts. This is deliberate, not an accident — do not split it into two fields.
- Commits are made by the human partner via GitKraken, not by the implementer
  running `git commit`. Every task's final step stages the files and reports the
  commit message — it does not run `git commit`.
- Local git identity for this repo: `Felipe Sonntag Manzoni <felipemanzoni3@gmail.com>`.
  Never touch global git config. Never add a co-author line or any reference to
  Claude/Anthropic/SiDi in a commit message.

---

### Task 1: Upgrade content in GameData.cs

**Files:**
- Modify: `Assets/Scripts/GameData.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces (used by Task 2 onward):
  - `enum UpgradeEffect { Click, Generator }`
  - `struct Upgrade { string id; string generatorId; double cost; UpgradeEffect effect; double multiplier; }`
    with constructor `Upgrade(string id, string generatorId, double cost, UpgradeEffect effect, double multiplier)`
  - `static readonly Upgrade[] GameData.Upgrades` — 4 entries, exact ids/values below
  - `static int GameData.IndexOfGenerator(string id)` — linear search, returns -1 if not found
  - `SaveData.upgradesPurchased` (`bool[]`)

No automated test for this task — it's pure content, matching the precedent set by
`GameData.Generators` (which also has no dedicated test file; correctness is
exercised indirectly by `Progression`'s and `Localization`'s tests in later tasks,
and by manual Play Mode).

- [ ] **Step 1: Add the Upgrade type and content**

Replace the full contents of `Assets/Scripts/GameData.cs`:

```csharp
using System;

namespace WontFix
{
    [Serializable]
    public struct SaveData
    {
        public double bugs;
        public double lifetimeBugs;
        public int[] owned;
        public bool[] upgradesPurchased;
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

    public enum UpgradeEffect
    {
        Click,
        Generator,
    }

    [Serializable]
    public struct Upgrade
    {
        public string id;
        public string generatorId;
        public double cost;
        public UpgradeEffect effect;
        public double multiplier;

        public Upgrade(string id, string generatorId, double cost, UpgradeEffect effect, double multiplier)
        {
            this.id = id;
            this.generatorId = generatorId;
            this.cost = cost;
            this.effect = effect;
            this.multiplier = multiplier;
        }
    }

    // Content lives here, in code, not in a ScriptableObject asset -- a
    // ScriptableObject would mean hand-filling rows in the Inspector, and
    // nobody can touch the Inspector here. Display text (name/flavor) lives
    // in Localization.cs, keyed by id, so balance changes here never touch
    // translated content and vice versa.
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

        // generatorId gates this upgrade's own unlock (own >=1 of it) AND,
        // for Generator-effect upgrades, is the generator it boosts.
        public static readonly Upgrade[] Upgrades =
        {
            new Upgrade("reproducible_steps", "junior_tester", 150, UpgradeEffect.Click, 2),
            new Upgrade("second_monitor", "junior_tester", 150, UpgradeEffect.Generator, 2),
            new Upgrade("explicit_waits", "selenium_script", 11000, UpgradeEffect.Generator, 2),
            new Upgrade("flaky_test_quarantine", "ci_pipeline", 120000, UpgradeEffect.Generator, 2),
        };

        public static int IndexOfGenerator(string id)
        {
            for (var i = 0; i < Generators.Length; i++)
                if (Generators[i].id == id)
                    return i;
            return -1;
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Editor open: switch focus to Unity, wait for the compile spinner, check the Console
for errors (there should be none — nothing yet consumes `Upgrades`/`IndexOfGenerator`).
Editor closed:

```bash
~/Unity/Hub/Editor/6000.5.9f1/Editor/Unity -batchmode -nographics -logFile - \
  -projectPath /home/fmanzoni-lx/Documents/Pessoal/wont-fix \
  -runTests -testPlatform EditMode -testResults /tmp/wont-fix-task1-results.xml
```

Expected: all existing tests (`EconomyTests`, `LocalizationTests`) still pass — this
task doesn't touch anything they exercise.

- [ ] **Step 3: Stage and report the commit message**

```bash
cd /home/fmanzoni-lx/Documents/Pessoal/wont-fix
git add Assets/Scripts/GameData.cs
```

Commit message (for the human partner to use via GitKraken):

```
Add upgrade content to GameData

Upgrade struct + UpgradeEffect enum + the 4 upgrades' costs/targets,
alongside the existing Generator content. SaveData gains
upgradesPurchased for the next task. Nothing consumes this yet.
```

---

### Task 2: Progression.cs — unlock and multiplier logic

**Files:**
- Create: `Assets/Scripts/Progression.cs`
- Create: `Assets/Tests/Editor/ProgressionTests.cs`

**Interfaces:**
- Consumes: `GameData.Generators`, `GameData.Upgrades`, `GameData.IndexOfGenerator`,
  `Upgrade`, `UpgradeEffect` (Task 1).
- Produces (used by Task 4):
  - `static bool Progression.IsGeneratorUnlocked(int index, int[] owned)`
  - `static bool Progression.IsUpgradeUnlocked(Upgrade upgrade, int[] owned)`
  - `static double Progression.ClickMultiplier(bool[] purchased)`
  - `static double Progression.GeneratorMultiplier(string generatorId, bool[] purchased)`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/ProgressionTests.cs`:

```csharp
using NUnit.Framework;

namespace WontFix.Tests
{
    public class ProgressionTests
    {
        [Test]
        public void IsGeneratorUnlocked_FirstGenerator_AlwaysUnlocked()
        {
            var owned = new int[GameData.Generators.Length];
            Assert.IsTrue(Progression.IsGeneratorUnlocked(0, owned));
        }

        [Test]
        public void IsGeneratorUnlocked_LockedUntilPreviousOwned()
        {
            var owned = new int[GameData.Generators.Length];
            Assert.IsFalse(Progression.IsGeneratorUnlocked(1, owned));

            owned[0] = 1;
            Assert.IsTrue(Progression.IsGeneratorUnlocked(1, owned));
        }

        [Test]
        public void IsUpgradeUnlocked_RequiresOwningGatingGenerator()
        {
            var owned = new int[GameData.Generators.Length];
            var upgrade = GameData.Upgrades[0]; // reproducible_steps, gates on junior_tester

            Assert.IsFalse(Progression.IsUpgradeUnlocked(upgrade, owned));

            owned[GameData.IndexOfGenerator("junior_tester")] = 1;
            Assert.IsTrue(Progression.IsUpgradeUnlocked(upgrade, owned));
        }

        [Test]
        public void ClickMultiplier_NoPurchases_IsOne()
        {
            var purchased = new bool[GameData.Upgrades.Length];
            Assert.AreEqual(1.0, Progression.ClickMultiplier(purchased), 0.0001);
        }

        [Test]
        public void ClickMultiplier_ReproducibleStepsPurchased_DoublesClick()
        {
            var purchased = new bool[GameData.Upgrades.Length];
            purchased[0] = true; // reproducible_steps, Click effect, x2

            Assert.AreEqual(2.0, Progression.ClickMultiplier(purchased), 0.0001);
        }

        [Test]
        public void GeneratorMultiplier_NoPurchases_IsOne()
        {
            var purchased = new bool[GameData.Upgrades.Length];
            Assert.AreEqual(1.0, Progression.GeneratorMultiplier("junior_tester", purchased), 0.0001);
        }

        [Test]
        public void GeneratorMultiplier_MatchingPurchase_Doubles()
        {
            var purchased = new bool[GameData.Upgrades.Length];
            purchased[1] = true; // second_monitor, Generator effect, targets junior_tester

            Assert.AreEqual(2.0, Progression.GeneratorMultiplier("junior_tester", purchased), 0.0001);
        }

        [Test]
        public void GeneratorMultiplier_PurchaseForDifferentGenerator_DoesNotApply()
        {
            var purchased = new bool[GameData.Upgrades.Length];
            purchased[1] = true; // second_monitor targets junior_tester, not selenium_script

            Assert.AreEqual(1.0, Progression.GeneratorMultiplier("selenium_script", purchased), 0.0001);
        }

        [Test]
        public void GeneratorMultiplier_ClickEffectPurchase_DoesNotLeakIn()
        {
            var purchased = new bool[GameData.Upgrades.Length];
            purchased[0] = true; // reproducible_steps is Click-effect, not Generator-effect

            Assert.AreEqual(1.0, Progression.GeneratorMultiplier("junior_tester", purchased), 0.0001);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Editor open: **Window > General > Test Runner > EditMode > Run All**.
Editor closed:

```bash
~/Unity/Hub/Editor/6000.5.9f1/Editor/Unity -batchmode -nographics -logFile - \
  -projectPath /home/fmanzoni-lx/Documents/Pessoal/wont-fix \
  -runTests -testPlatform EditMode -testResults /tmp/wont-fix-task2-results.xml
```

Expected: **compile error** — `Progression` doesn't exist yet, so `WontFix.Tests`
fails to build.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/Progression.cs`:

```csharp
namespace WontFix
{
    // Pure math/logic, no UnityEngine dependency, so it's testable without a
    // scene -- same reasoning as Economy.cs. Game.cs calls these instead of
    // computing unlock/multiplier logic inline, so branching that's easy to
    // get subtly wrong (right upgrade, right generator, right effect) is
    // covered by tests instead of only by manual Play Mode checks.
    public static class Progression
    {
        public static bool IsGeneratorUnlocked(int index, int[] owned) =>
            index == 0 || owned[index - 1] >= 1;

        public static bool IsUpgradeUnlocked(Upgrade upgrade, int[] owned) =>
            owned[GameData.IndexOfGenerator(upgrade.generatorId)] >= 1;

        public static double ClickMultiplier(bool[] purchased) =>
            Product(purchased, UpgradeEffect.Click, null);

        public static double GeneratorMultiplier(string generatorId, bool[] purchased) =>
            Product(purchased, UpgradeEffect.Generator, generatorId);

        static double Product(bool[] purchased, UpgradeEffect effect, string generatorId)
        {
            double result = 1;
            for (var i = 0; i < GameData.Upgrades.Length; i++)
            {
                var u = GameData.Upgrades[i];
                if (purchased[i] && u.effect == effect && (generatorId == null || u.generatorId == generatorId))
                    result *= u.multiplier;
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Same as Step 2. Expected: **PASS** — all 8 `ProgressionTests` methods green,
`EconomyTests`/`LocalizationTests` still green.

- [ ] **Step 5: Stage and report the commit message**

```bash
cd /home/fmanzoni-lx/Documents/Pessoal/wont-fix
git add Assets/Scripts/Progression.cs Assets/Tests/Editor/ProgressionTests.cs
```

Commit message:

```
Add Progression: unlock and multiplier logic

Pure static functions (no UnityEngine dependency), same pattern as
Economy.cs. Covers the generator-unlock chain and the click/generator
multiplier math with tests -- not wired into Game.cs yet.
```

---

### Task 3: Upgrade translations in Localization.cs

**Files:**
- Modify: `Assets/Scripts/Localization.cs`
- Modify: `Assets/Tests/Editor/LocalizationTests.cs`

**Interfaces:**
- Consumes: `GameData.Upgrades` (Task 1).
- Produces (used by Task 5): localization keys `upg.<id>.name` / `upg.<id>.flavor`
  for all 4 upgrade ids, plus `ui.owned`, resolvable via the existing
  `Localization.Get(string key)`.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/Editor/LocalizationTests.cs`, replace the `UiKeys` array:

```csharp
        static readonly string[] UiKeys =
        {
            "ui.run_test",
            "ui.bugs_found",
            "ui.bugs_per_second",
        };
```

with:

```csharp
        static readonly string[] UiKeys =
        {
            "ui.run_test",
            "ui.bugs_found",
            "ui.bugs_per_second",
            "ui.owned",
        };
```

Then add this test method inside the `LocalizationTests` class, alongside
`Get_GeneratorKeys_NonEmptyInBothLanguages`:

```csharp
        [Test]
        public void Get_UpgradeKeys_NonEmptyInBothLanguages()
        {
            var keys = new System.Collections.Generic.List<string>();
            foreach (var upgrade in GameData.Upgrades)
            {
                keys.Add($"upg.{upgrade.id}.name");
                keys.Add($"upg.{upgrade.id}.flavor");
            }

            AssertTranslated(keys.ToArray());
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Same run instructions as Task 2, Step 2. Expected: **FAIL** —
`Get_UiKeys_NonEmptyInBothLanguages` fails on `"ui.owned"` (missing from both
dictionaries), `Get_UpgradeKeys_NonEmptyInBothLanguages` fails on every `upg.*` key
(none exist yet).

- [ ] **Step 3: Write minimal implementation**

In `Assets/Scripts/Localization.cs`, insert these entries just before the closing
`};` of the `En` dictionary (after the last `gen.production_users.flavor` line):

```csharp
            ["ui.owned"] = "OWNED",
            ["upg.reproducible_steps.name"] = "Reproducible Steps",
            ["upg.reproducible_steps.flavor"] = "Doubles bugs found per click.",
            ["upg.second_monitor.name"] = "Second Monitor",
            ["upg.second_monitor.flavor"] = "Doubles Junior Tester output.",
            ["upg.explicit_waits.name"] = "Explicit Waits",
            ["upg.explicit_waits.flavor"] = "Doubles Selenium Script output.",
            ["upg.flaky_test_quarantine.name"] = "Flaky Test Quarantine",
            ["upg.flaky_test_quarantine.flavor"] = "Doubles CI Pipeline output.",
```

Insert these entries just before the closing `};` of the `PtBr` dictionary (after
the last `gen.production_users.flavor` line):

```csharp
            ["ui.owned"] = "ADQUIRIDO",
            ["upg.reproducible_steps.name"] = "Passos Reproduzíveis",
            ["upg.reproducible_steps.flavor"] = "Dobra os bugs encontrados por clique.",
            ["upg.second_monitor.name"] = "Segundo Monitor",
            ["upg.second_monitor.flavor"] = "Dobra a produção do Testador Júnior.",
            ["upg.explicit_waits.name"] = "Esperas Explícitas",
            ["upg.explicit_waits.flavor"] = "Dobra a produção do Script Selenium.",
            ["upg.flaky_test_quarantine.name"] = "Quarentena de Testes Instáveis",
            ["upg.flaky_test_quarantine.flavor"] = "Dobra a produção do Pipeline de CI.",
```

- [ ] **Step 4: Run tests to verify they pass**

Same as Step 2. Expected: **PASS** — all `LocalizationTests` methods green
(including the two changed/added ones), `EconomyTests`/`ProgressionTests` unaffected.

- [ ] **Step 5: Stage and report the commit message**

```bash
cd /home/fmanzoni-lx/Documents/Pessoal/wont-fix
git add Assets/Scripts/Localization.cs Assets/Tests/Editor/LocalizationTests.cs
```

Commit message:

```
Add upgrade translations

upg.<id>.name/flavor for all 4 upgrades in EN/PT-BR, plus ui.owned
for the purchased-state label. Extends the existing completeness
tests the same way the generator keys already are covered.
```

---

### Task 4: Game.cs — wire unlocking and upgrades into gameplay

**Files:**
- Modify: `Assets/Scripts/Game.cs`

**Interfaces:**
- Consumes: `Progression.IsGeneratorUnlocked`, `Progression.IsUpgradeUnlocked`,
  `Progression.ClickMultiplier`, `Progression.GeneratorMultiplier` (Task 2);
  `GameData.Upgrades`, `SaveData.upgradesPurchased` (Task 1).
- Produces (used by Task 5):
  - `public bool[] Game.UpgradesPurchased { get; }`
  - `public bool Game.IsGeneratorUnlocked(int index)`
  - `public bool Game.IsUpgradeUnlocked(int index)`
  - `public bool Game.CanBuyUpgrade(int index)`
  - `public void Game.BuyUpgrade(int index)`

No new automated test — `Game` is a `MonoBehaviour`, verified manually via Play
Mode in this codebase (same convention as milestone 1 and the i18n retrofit). The
math it now calls (`Progression`) is already covered by Task 2's tests; this task
is orchestration only.

- [ ] **Step 1: Write the implementation**

Replace the full contents of `Assets/Scripts/Game.cs`:

```csharp
using System;
using UnityEngine;

namespace WontFix
{
    // The whole game lives on one component: accumulate bugs, buy generators,
    // save/load. RequireComponent pulls GameUI along automatically, so
    // adding "Game" in the Inspector is the only wiring step this game needs.
    [RequireComponent(typeof(GameUI))]
    public class Game : MonoBehaviour
    {
        const string SaveKey = "wontfix.save";

        public double Bugs { get; private set; }
        public double LifetimeBugs { get; private set; }
        public double BugsPerSecond { get; private set; }
        public int[] Owned { get; private set; }
        public bool[] UpgradesPurchased { get; private set; }

        public event Action Changed;

        void Awake()
        {
            Owned = new int[GameData.Generators.Length];
            UpgradesPurchased = new bool[GameData.Upgrades.Length];
            Load();
        }

        void Update()
        {
            if (BugsPerSecond <= 0) return;
            Add(BugsPerSecond * Time.deltaTime);
        }

        public void Click() => Add(Progression.ClickMultiplier(UpgradesPurchased));

        public bool IsGeneratorUnlocked(int index) => Progression.IsGeneratorUnlocked(index, Owned);

        public double CostOf(int index) =>
            Economy.CostOf(GameData.Generators[index].baseCost, Owned[index]);

        public bool CanBuy(int index) => Bugs >= CostOf(index);

        public void Buy(int index)
        {
            var cost = CostOf(index);
            if (Bugs < cost) return;

            Bugs -= cost;
            Owned[index]++;
            RecalculateRate();
            Save();
            Changed?.Invoke();
        }

        public bool IsUpgradeUnlocked(int index) =>
            Progression.IsUpgradeUnlocked(GameData.Upgrades[index], Owned);

        public bool CanBuyUpgrade(int index) =>
            !UpgradesPurchased[index] && Bugs >= GameData.Upgrades[index].cost;

        public void BuyUpgrade(int index)
        {
            if (UpgradesPurchased[index]) return;

            var cost = GameData.Upgrades[index].cost;
            if (Bugs < cost) return;

            Bugs -= cost;
            UpgradesPurchased[index] = true;
            RecalculateRate();
            Save();
            Changed?.Invoke();
        }

        void Add(double amount)
        {
            Bugs += amount;
            LifetimeBugs += amount;
            Changed?.Invoke();
        }

        void RecalculateRate()
        {
            double rate = 0;
            for (var i = 0; i < GameData.Generators.Length; i++)
            {
                var gen = GameData.Generators[i];
                rate += gen.bugsPerSecond * Owned[i] * Progression.GeneratorMultiplier(gen.id, UpgradesPurchased);
            }
            BugsPerSecond = rate;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        void OnApplicationQuit() => Save();

        void Save()
        {
            var data = new SaveData
            {
                bugs = Bugs,
                lifetimeBugs = LifetimeBugs,
                owned = Owned,
                upgradesPurchased = UpgradesPurchased,
                lastSeenUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                RecalculateRate();
                return;
            }

            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            Bugs = data.bugs;
            LifetimeBugs = data.lifetimeBugs;
            if (data.owned != null && data.owned.Length == Owned.Length)
                Owned = data.owned;
            if (data.upgradesPurchased != null && data.upgradesPurchased.Length == UpgradesPurchased.Length)
                UpgradesPurchased = data.upgradesPurchased;

            RecalculateRate();

            if (data.lastSeenUnixSeconds > 0)
            {
                var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - data.lastSeenUnixSeconds;
                if (elapsed > 0)
                    Add(Economy.Accrue(BugsPerSecond, elapsed));
            }
        }
    }
}
```

- [ ] **Step 2: Verify it compiles and existing tests still pass**

Same run instructions as Task 2, Step 2.
Expected: **PASS** — all existing tests green (`EconomyTests`, `LocalizationTests`,
`ProgressionTests`); this task adds no new automated test, so the count doesn't
change.

- [ ] **Step 3: Manual verification (Play Mode)**

`GameUI` hasn't changed yet, so it still only calls the methods that existed
before this task (`Owned`, `CanBuy`, `CostOf`, `Buy`, `Click`, `Bugs`,
`BugsPerSecond`) — none of which changed signature. Switch to the Unity Editor,
press **Play**. Confirm the game behaves exactly as before this task: all 12
generators visible, clicking and buying work identically, no upgrades visible
anywhere yet (expected — `GameUI` doesn't build upgrade rows until Task 5). Stop
Play.

- [ ] **Step 4: Stage and report the commit message**

```bash
cd /home/fmanzoni-lx/Documents/Pessoal/wont-fix
git add Assets/Scripts/Game.cs
```

Commit message:

```
Wire Progression and upgrades into Game

Click() and RecalculateRate() route through Progression's multiplier
functions; adds BuyUpgrade/CanBuyUpgrade/IsUpgradeUnlocked/
IsGeneratorUnlocked and persists upgradesPurchased. GameUI doesn't
consume any of this yet, so behavior is unchanged until the next task.
```

---

### Task 5: GameUI.cs — conditional rows and upgrade UI

**Files:**
- Modify: `Assets/Scripts/GameUI.cs`

**Interfaces:**
- Consumes: `Game.IsGeneratorUnlocked`, `Game.IsUpgradeUnlocked`,
  `Game.CanBuyUpgrade`, `Game.BuyUpgrade`, `Game.UpgradesPurchased` (Task 4);
  `GameData.Upgrades` (Task 1); `upg.<id>.name`/`upg.<id>.flavor`/`ui.owned`
  localization keys (Task 3).
- Produces: nothing further — this completes the milestone.

UI-only change, no new automated test, matching this codebase's established
convention (`GameUI`/`Game` verified manually via Play Mode).

- [ ] **Step 1: Add row-tracking fields**

In `Assets/Scripts/GameUI.cs`, replace:

```csharp
        readonly List<Text> rowInfoTexts = new();
        readonly List<Text> rowCostTexts = new();
        readonly List<Button> rowButtons = new();
```

with:

```csharp
        readonly List<GameObject> rowObjects = new();
        readonly List<Text> rowInfoTexts = new();
        readonly List<Text> rowCostTexts = new();
        readonly List<Button> rowButtons = new();

        readonly List<GameObject> upgradeRowObjects = new();
        readonly List<Text> upgradeInfoTexts = new();
        readonly List<Text> upgradeCostTexts = new();
        readonly List<Button> upgradeButtons = new();
```

- [ ] **Step 2: Interleave upgrade rows into BuildShop**

Replace the last two lines of `BuildShop` (the generator-building loop):

```csharp
            for (var i = 0; i < GameData.Generators.Length; i++)
                BuildRow(contentGO.transform, i);
        }
```

with:

```csharp
            for (var i = 0; i < GameData.Generators.Length; i++)
            {
                BuildRow(contentGO.transform, i);

                for (var u = 0; u < GameData.Upgrades.Length; u++)
                    if (GameData.Upgrades[u].generatorId == GameData.Generators[i].id)
                        BuildUpgradeRow(contentGO.transform, u);
            }
        }
```

- [ ] **Step 3: Track the row GameObject and start rows hidden**

Replace `BuildRow` in full:

```csharp
        void BuildRow(Transform parent, int index)
        {
            var rowGO = CreatePanel(parent, $"Row{index}", RowLocked).gameObject;
            AddFixedHeight(rowGO, 64);

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            var info = CreateText(rowGO.transform, "", 16, White, FontStyle.Normal, TextAnchor.MiddleLeft);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            AddFlexibleWidth(info.gameObject);

            var buyGO = CreatePanel(rowGO.transform, "Buy", new Color(0.20f, 0.45f, 0.30f)).gameObject;
            AddFixedWidth(buyGO, 160);
            var button = buyGO.AddComponent<Button>();
            button.targetGraphic = buyGO.GetComponent<Image>();
            var cost = CreateText(buyGO.transform, "", 16, White, FontStyle.Bold, TextAnchor.MiddleCenter);

            var capturedIndex = index;
            button.onClick.AddListener(() => game.Buy(capturedIndex));

            rowGO.SetActive(false);

            rowObjects.Add(rowGO);
            rowInfoTexts.Add(info);
            rowCostTexts.Add(cost);
            rowButtons.Add(button);
        }
```

(The only changes from the existing method: `rowGO.SetActive(false)` added right
before the `.Add()` calls, and `rowObjects.Add(rowGO)` added alongside the existing
three `.Add()` calls.)

- [ ] **Step 4: Add BuildUpgradeRow**

Add this new method directly after `BuildRow`:

```csharp
        void BuildUpgradeRow(Transform parent, int index)
        {
            var rowGO = CreatePanel(parent, $"Upgrade{index}", RowLocked).gameObject;
            AddFixedHeight(rowGO, 64);

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;

            var info = CreateText(rowGO.transform, "", 16, White, FontStyle.Normal, TextAnchor.MiddleLeft);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            AddFlexibleWidth(info.gameObject);

            var buyGO = CreatePanel(rowGO.transform, "Buy", new Color(0.20f, 0.45f, 0.30f)).gameObject;
            AddFixedWidth(buyGO, 160);
            var button = buyGO.AddComponent<Button>();
            button.targetGraphic = buyGO.GetComponent<Image>();
            var cost = CreateText(buyGO.transform, "", 16, White, FontStyle.Bold, TextAnchor.MiddleCenter);

            var capturedIndex = index;
            button.onClick.AddListener(() => game.BuyUpgrade(capturedIndex));

            rowGO.SetActive(false);

            upgradeRowObjects.Add(rowGO);
            upgradeInfoTexts.Add(info);
            upgradeCostTexts.Add(cost);
            upgradeButtons.Add(button);
        }
```

- [ ] **Step 5: Extend Refresh() with unlock toggling and the upgrade loop**

Replace the generator loop inside `Refresh()`:

```csharp
            for (var i = 0; i < GameData.Generators.Length; i++)
            {
                var gen = GameData.Generators[i];
                var owned = game.Owned[i];
                var canBuy = game.CanBuy(i);
                var name = Localization.Get($"gen.{gen.id}.name");
                var flavor = Localization.Get($"gen.{gen.id}.flavor");

                rowInfoTexts[i].text = $"{name}  (x{owned})\n{flavor}";
                rowCostTexts[i].text = Economy.Format(game.CostOf(i));
                rowButtons[i].interactable = canBuy;
                rowButtons[i].GetComponent<Image>().color =
                    canBuy ? new Color(0.20f, 0.45f, 0.30f) : new Color(0.25f, 0.25f, 0.28f);
                rowButtons[i].transform.parent.GetComponent<Image>().color =
                    canBuy ? RowAffordable : RowLocked;
            }
        }
```

with:

```csharp
            for (var i = 0; i < GameData.Generators.Length; i++)
            {
                var unlocked = game.IsGeneratorUnlocked(i);
                rowObjects[i].SetActive(unlocked);
                if (!unlocked) continue;

                var gen = GameData.Generators[i];
                var owned = game.Owned[i];
                var canBuy = game.CanBuy(i);
                var name = Localization.Get($"gen.{gen.id}.name");
                var flavor = Localization.Get($"gen.{gen.id}.flavor");

                rowInfoTexts[i].text = $"{name}  (x{owned})\n{flavor}";
                rowCostTexts[i].text = Economy.Format(game.CostOf(i));
                rowButtons[i].interactable = canBuy;
                rowButtons[i].GetComponent<Image>().color =
                    canBuy ? new Color(0.20f, 0.45f, 0.30f) : new Color(0.25f, 0.25f, 0.28f);
                rowObjects[i].GetComponent<Image>().color =
                    canBuy ? RowAffordable : RowLocked;
            }

            for (var i = 0; i < GameData.Upgrades.Length; i++)
            {
                var unlocked = game.IsUpgradeUnlocked(i);
                upgradeRowObjects[i].SetActive(unlocked);
                if (!unlocked) continue;

                var upgrade = GameData.Upgrades[i];
                var purchased = game.UpgradesPurchased[i];
                var canBuy = game.CanBuyUpgrade(i);
                var name = Localization.Get($"upg.{upgrade.id}.name");
                var flavor = Localization.Get($"upg.{upgrade.id}.flavor");

                upgradeInfoTexts[i].text = $"{name}\n{flavor}";
                upgradeCostTexts[i].text = purchased ? Localization.Get("ui.owned") : Economy.Format(upgrade.cost);
                upgradeButtons[i].interactable = canBuy;
                upgradeButtons[i].GetComponent<Image>().color =
                    canBuy ? new Color(0.20f, 0.45f, 0.30f) : new Color(0.25f, 0.25f, 0.28f);
                upgradeRowObjects[i].GetComponent<Image>().color =
                    canBuy ? RowAffordable : RowLocked;
            }
        }
```

Note this also replaces `rowButtons[i].transform.parent.GetComponent<Image>()` with
the more direct `rowObjects[i].GetComponent<Image>()` — the row GameObject is now
tracked explicitly (Step 3), so reaching for it through the button's parent
transform is no longer necessary.

- [ ] **Step 6: Verify it compiles and existing tests still pass**

Same run instructions as Task 2, Step 2.
Expected: **PASS** — all existing tests green; this task adds no new automated test.

- [ ] **Step 7: Manual verification (Play Mode)**

Switch to the Unity Editor, press **Play**. Confirm:
- Only **Junior Tester** is visible at the start of a fresh game (clear
  `PlayerPrefs` first if testing from an existing save, or just note existing
  progress will already have later generators/upgrades correctly shown/hidden
  based on what's already owned).
- Buying your first Junior Tester reveals **Test Case Spreadsheet**, and also
  reveals **Reproducible Steps** and **Second Monitor** directly below it (both
  gate on `junior_tester`).
- Reproducible Steps costs 150 and, once bought, its row shows **OWNED** instead
  of a price and its button is no longer clickable; clicking `RUN TEST`
  afterward finds 2 bugs instead of 1.
- Second Monitor costs 150 and, once bought, Junior Tester's bugs/sec contribution
  doubles (watch the header's bugs/sec rate change).
- Buying generators up the chain in order reveals each next one, and
  **Explicit Waits** appears once you own 1 Selenium Script, **Flaky Test
  Quarantine** once you own 1 CI Pipeline.
- Toggle the language button — upgrade rows translate to Portuguese
  ("Passos Reproduzíveis", etc.) exactly like generator rows already do.
- Stop Play, press Play again — unlocked/purchased state persists.

- [ ] **Step 8: Stage and report the commit message**

```bash
cd /home/fmanzoni-lx/Documents/Pessoal/wont-fix
git add Assets/Scripts/GameUI.cs
```

Commit message:

```
Show unlockable generators and upgrades in the shop

Rows start hidden and Refresh() toggles them via
Game.IsGeneratorUnlocked/IsUpgradeUnlocked. Upgrade rows are
interleaved right after the generator that gates them and show an
OWNED label once purchased instead of a price.
```
