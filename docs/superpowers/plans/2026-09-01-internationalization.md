# Internationalization (English / Portuguese-BR) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every player-visible string in Won't Fix (button labels, header, all 12 generator names/flavors) renders in English or Portuguese-BR, switchable live via an in-game toggle, with the choice persisted and auto-detected from the OS on first run.

**Architecture:** A new `Localization` static class holds two hand-written `Dictionary<string, string>` tables (English, Portuguese-BR) keyed by plain string constants, mirroring the existing "content lives in code" pattern from `GameData.cs`. `GameData.Generator` drops its raw `name`/`flavor` strings in favor of a stable `id`, which `Localization` keys off of (`gen.<id>.name`, `gen.<id>.flavor`). `GameUI.Refresh()` — already the single place that redraws dynamic state — becomes the single place that redraws *all* text, static and dynamic alike, so a language switch is just "call Refresh() again."

**Tech Stack:** Unity 6000.5.9f1, C#, `UnityEngine.UI` (legacy `Text`), NUnit via `com.unity.test-framework`.

## Global Constraints

- No Unity Editor GUI authoring anywhere in this plan — every change is a text file edit. Verification is either the in-Editor Test Runner (Window > General > Test Runner > EditMode > Run All) or the headless batchmode command in `CLAUDE.md`, never Inspector clicking.
- Active Input Handling is Input System package only (`activeInputHandler: 1`) — not touched by this plan, but nothing here should reintroduce `StandaloneInputModule` or `UnityEngine.Input`.
- No TextMeshPro — all new text uses `UnityEngine.UI.Text`, same as the existing UI.
- Two assemblies: gameplay code lives in `WontFix` (`Assets/Scripts/WontFix.asmdef`), tests live in `WontFix.Tests` (`Assets/Tests/Editor/WontFix.Tests.asmdef`, references `WontFix`). Custom asmdefs compile *before* the implicit `Assembly-CSharp`, so nothing under `Assets/Tests` can ever reference code outside an asmdef — this is why gameplay code has its own asmdef at all. New files in this plan go in `Assets/Scripts/` (gameplay) or `Assets/Tests/Editor/` (tests), never elsewhere.
- Language preference persists under its own `PlayerPrefs` key, `wontfix.language` — independent of `Game`'s save blob (`wontfix.save`). Never merge the two.
- Numbers stay locale-invariant: `Economy.Format` output (e.g. `"1.23 K"`) is identical in both languages. No locale-aware number formatting in this plan.
- Local git identity for every commit in this repo: `F. Manzoni <felipemanzoni3@gmail.com>`. Never touch global git config. Never add a co-author line or any reference to Claude/Anthropic/SiDi in a commit message.

---

### Task 1: Localization core infrastructure

**Files:**
- Create: `Assets/Scripts/Localization.cs`
- Create: `Assets/Tests/Editor/LocalizationTests.cs`

**Interfaces:**
- Consumes: nothing (new, standalone).
- Produces (used by Task 2 and Task 3):
  - `enum Language { English, PortugueseBR }`
  - `static Language Localization.Current { get; }`
  - `static void Localization.SetLanguage(Language language)`
  - `static string Localization.Get(string key)` — returns `key` itself if no translation exists for the current language
  - `static event Action Localization.Changed`
  - `static void Localization.ResetForTests()` — test-only, clears cached init state
  - Keys defined in this task: `"ui.run_test"`, `"ui.bugs_found"` (format string, one `{0}`), `"ui.bugs_per_second"` (format string, one `{0}`)

This task does not wire `Localization` into `Game` or `GameUI` — the game still shows its original hardcoded English text after this task. That wiring is Task 2 (generator rows) and Task 3 (header/button/toggle).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/LocalizationTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace WontFix.Tests
{
    public class LocalizationTests
    {
        const string LanguageKey = "wontfix.language";

        static readonly string[] UiKeys =
        {
            "ui.run_test",
            "ui.bugs_found",
            "ui.bugs_per_second",
        };

        int? originalLanguage;

        [SetUp]
        public void SaveOriginalLanguage()
        {
            originalLanguage = PlayerPrefs.HasKey(LanguageKey) ? PlayerPrefs.GetInt(LanguageKey) : (int?)null;
        }

        [TearDown]
        public void RestoreOriginalLanguage()
        {
            if (originalLanguage.HasValue)
                PlayerPrefs.SetInt(LanguageKey, originalLanguage.Value);
            else
                PlayerPrefs.DeleteKey(LanguageKey);
            PlayerPrefs.Save();
            Localization.ResetForTests();
        }

        [Test]
        public void Get_UiKeys_NonEmptyInBothLanguages()
        {
            AssertTranslated(UiKeys);
        }

        static void AssertTranslated(string[] keys)
        {
            foreach (var key in keys)
            {
                Localization.SetLanguage(Language.English);
                Assert.IsFalse(string.IsNullOrEmpty(Localization.Get(key)), $"Missing English text for '{key}'");
                Assert.AreNotEqual(key, Localization.Get(key), $"English text for '{key}' fell back to the raw key");

                Localization.SetLanguage(Language.PortugueseBR);
                Assert.IsFalse(string.IsNullOrEmpty(Localization.Get(key)), $"Missing Portuguese text for '{key}'");
                Assert.AreNotEqual(key, Localization.Get(key), $"Portuguese text for '{key}' fell back to the raw key");
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Editor open (normal case): switch focus to Unity, wait for the compile spinner to finish, open **Window > General > Test Runner > EditMode**, click **Run All**.
Editor closed: run

```bash
~/Unity/Hub/Editor/6000.5.9f1/Editor/Unity -batchmode -nographics -logFile - \
  -projectPath /home/fmanzoni-lx/Documents/Pessoal/wont-fix \
  -runTests -testPlatform EditMode -testResults /tmp/wont-fix-results.xml
```

Expected: **compile error** — `Localization` and `Language` don't exist yet, so `WontFix.Tests` fails to build and the Test Runner reports the whole assembly as failing. That compile failure is the expected "red" state for this step.

- [ ] **Step 3: Write minimal implementation**

Create `Assets/Scripts/Localization.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WontFix
{
    public enum Language
    {
        English,
        PortugueseBR,
    }

    // Hand-rolled, mirroring GameData's "content lives in code" pattern --
    // Unity's Localization package assumes Editor-window authoring, which
    // nobody can do headlessly here. Self-initializing (not tied to any
    // MonoBehaviour's Awake) because Unity doesn't guarantee Game.Awake()
    // runs before GameUI.Awake() just because GameUI is a required
    // component -- Get()/Current resolve themselves the first time
    // anything touches them, so there's no ordering to get wrong.
    public static class Localization
    {
        const string LanguageKey = "wontfix.language";

        public static event Action Changed;

        static Language current;
        static bool initialized;

        public static Language Current
        {
            get
            {
                EnsureInitialized();
                return current;
            }
        }

        public static void SetLanguage(Language language)
        {
            EnsureInitialized();
            if (current == language) return;

            current = language;
            PlayerPrefs.SetInt(LanguageKey, (int)current);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string Get(string key)
        {
            EnsureInitialized();
            var table = current == Language.English ? En : PtBr;
            return table.TryGetValue(key, out var value) ? value : key;
        }

        // Test-only: clears cached state so the next Get()/Current call
        // re-reads PlayerPrefs from scratch. Without this, running EditMode
        // tests and then pressing Play in the same Editor session would
        // leak whatever language the tests last set into the live game,
        // since `initialized` would already be true.
        public static void ResetForTests()
        {
            initialized = false;
        }

        static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            if (PlayerPrefs.HasKey(LanguageKey))
            {
                current = (Language)PlayerPrefs.GetInt(LanguageKey);
                return;
            }

            current = Application.systemLanguage == SystemLanguage.Portuguese
                ? Language.PortugueseBR
                : Language.English;
            PlayerPrefs.SetInt(LanguageKey, (int)current);
            PlayerPrefs.Save();
        }

        static readonly Dictionary<string, string> En = new()
        {
            ["ui.run_test"] = "RUN TEST",
            ["ui.bugs_found"] = "{0} bugs found",
            ["ui.bugs_per_second"] = "{0} bugs/sec",
        };

        static readonly Dictionary<string, string> PtBr = new()
        {
            ["ui.run_test"] = "EXECUTAR TESTE",
            ["ui.bugs_found"] = "{0} bugs encontrados",
            ["ui.bugs_per_second"] = "{0} bugs/seg",
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Same as Step 2 (Test Runner or batchmode command).
Expected: **PASS** — `LocalizationTests.Get_UiKeys_NonEmptyInBothLanguages` green, `EconomyTests` still green too (unaffected by this task).

- [ ] **Step 5: Commit**

```bash
cd /home/fmanzoni-lx/Documents/Pessoal/wont-fix
git add Assets/Scripts/Localization.cs Assets/Tests/Editor/LocalizationTests.cs
git commit -m "$(cat <<'EOF'
Add Localization infrastructure (EN / PT-BR)

Static class mirrors GameData's content-in-code pattern. Not wired
into Game or GameUI yet -- the game still shows its original
hardcoded English text until the next two tasks retrofit it.
EOF
)"
```

---

### Task 2: GameData generator retrofit + localized shop rows

**Files:**
- Modify: `Assets/Scripts/GameData.cs`
- Modify: `Assets/Scripts/GameUI.cs:207-220` (the generator-row loop inside `Refresh()`)
- Modify: `Assets/Tests/Editor/LocalizationTests.cs` (add generator-key coverage)

**Interfaces:**
- Consumes: `Localization.Get(string key)`, `Localization.SetLanguage`, `Language` enum from Task 1.
- Produces (used by Task 3 — though Task 3 doesn't touch this loop again):
  - `GameData.Generator.id` (string field, replaces `name`/`flavor`)
  - The 12 generator ids, in order: `junior_tester`, `test_case_spreadsheet`, `selenium_script`, `ci_pipeline`, `load_test_rig`, `fuzzer`, `static_analyzer`, `monkey_test_farm`, `llm_test_agent`, `chaos_engineering_cluster`, `formal_verification_lab`, `production_users`
  - Localization key pattern `gen.<id>.name` / `gen.<id>.flavor`, populated for all 12 ids in both languages

This task changes `GameData.Generator`'s public shape, and `GameUI.cs` is its only consumer (line 213 in the current file) — both files must change together or the project won't compile, so this task lands as one commit rather than being split further.

- [ ] **Step 1: Write the failing test**

Add this test to `Assets/Tests/Editor/LocalizationTests.cs`, inside the `LocalizationTests` class (after `Get_UiKeys_NonEmptyInBothLanguages`):

```csharp
        [Test]
        public void Get_GeneratorKeys_NonEmptyInBothLanguages()
        {
            var keys = new System.Collections.Generic.List<string>();
            foreach (var gen in GameData.Generators)
            {
                keys.Add($"gen.{gen.id}.name");
                keys.Add($"gen.{gen.id}.flavor");
            }

            AssertTranslated(keys.ToArray());
        }
```

- [ ] **Step 2: Run test to verify it fails**

Same run instructions as Task 1, Step 2.
Expected: **compile error** — `Generator` has no `id` field yet (`GameData.cs` still declares `name`/`flavor`).

- [ ] **Step 3: Write minimal implementation**

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
```

Add the generator translation keys to both dictionaries in `Assets/Scripts/Localization.cs`. In `En`, after the three `ui.*` entries:

```csharp
            ["gen.junior_tester.name"] = "Junior Tester",
            ["gen.junior_tester.flavor"] = "Clicks every button. Occasionally reads the spec.",
            ["gen.test_case_spreadsheet.name"] = "Test Case Spreadsheet",
            ["gen.test_case_spreadsheet.flavor"] = "400 rows. Last updated 2019.",
            ["gen.selenium_script.name"] = "Selenium Script",
            ["gen.selenium_script.flavor"] = "Flaky, but it runs. Mostly.",
            ["gen.ci_pipeline.name"] = "CI Pipeline",
            ["gen.ci_pipeline.flavor"] = "Runs on every commit. Red on every commit.",
            ["gen.load_test_rig.name"] = "Load Test Rig",
            ["gen.load_test_rig.flavor"] = "Finds bugs by asking \"what if 10,000 users?\"",
            ["gen.fuzzer.name"] = "Fuzzer",
            ["gen.fuzzer.flavor"] = "Throws garbage at it until something screams.",
            ["gen.static_analyzer.name"] = "Static Analyzer",
            ["gen.static_analyzer.flavor"] = "4,000 warnings. Three of them matter.",
            ["gen.monkey_test_farm.name"] = "Monkey Test Farm",
            ["gen.monkey_test_farm.flavor"] = "Literal monkeys. Literal tablets. Great coverage.",
            ["gen.llm_test_agent.name"] = "LLM Test Agent",
            ["gen.llm_test_agent.flavor"] = "Writes brilliant tests for features you don't have.",
            ["gen.chaos_engineering_cluster.name"] = "Chaos Engineering Cluster",
            ["gen.chaos_engineering_cluster.flavor"] = "Breaks production on purpose, for science.",
            ["gen.formal_verification_lab.name"] = "Formal Verification Lab",
            ["gen.formal_verification_lab.flavor"] = "Proves mathematically that the bug exists.",
            ["gen.production_users.name"] = "Production Users",
            ["gen.production_users.flavor"] = "The finest test environment ever devised.",
```

In `PtBr`, after the three `ui.*` entries:

```csharp
            ["gen.junior_tester.name"] = "Testador Júnior",
            ["gen.junior_tester.flavor"] = "Clica em todos os botões. Às vezes lê a especificação.",
            ["gen.test_case_spreadsheet.name"] = "Planilha de Casos de Teste",
            ["gen.test_case_spreadsheet.flavor"] = "400 linhas. Última atualização em 2019.",
            ["gen.selenium_script.name"] = "Script Selenium",
            ["gen.selenium_script.flavor"] = "Instável, mas funciona. Quase sempre.",
            ["gen.ci_pipeline.name"] = "Pipeline de CI",
            ["gen.ci_pipeline.flavor"] = "Roda a cada commit. Fica vermelho a cada commit.",
            ["gen.load_test_rig.name"] = "Bancada de Teste de Carga",
            ["gen.load_test_rig.flavor"] = "Encontra bugs perguntando \"e se fossem 10.000 usuários?\"",
            ["gen.fuzzer.name"] = "Fuzzer",
            ["gen.fuzzer.flavor"] = "Joga lixo até algo gritar.",
            ["gen.static_analyzer.name"] = "Analisador Estático",
            ["gen.static_analyzer.flavor"] = "4.000 avisos. Três deles importam.",
            ["gen.monkey_test_farm.name"] = "Fazenda de Teste de Macaco",
            ["gen.monkey_test_farm.flavor"] = "Macacos de verdade. Tablets de verdade. Ótima cobertura.",
            ["gen.llm_test_agent.name"] = "Agente de Teste com LLM",
            ["gen.llm_test_agent.flavor"] = "Escreve testes brilhantes para funcionalidades que você não tem.",
            ["gen.chaos_engineering_cluster.name"] = "Cluster de Engenharia do Caos",
            ["gen.chaos_engineering_cluster.flavor"] = "Quebra a produção de propósito, pela ciência.",
            ["gen.formal_verification_lab.name"] = "Laboratório de Verificação Formal",
            ["gen.formal_verification_lab.flavor"] = "Prova matematicamente que o bug existe.",
            ["gen.production_users.name"] = "Usuários em Produção",
            ["gen.production_users.flavor"] = "O melhor ambiente de teste já criado.",
```

In `Assets/Scripts/GameUI.cs`, replace the generator-row loop inside `Refresh()` (currently lines 207-220):

```csharp
            for (var i = 0; i < GameData.Generators.Length; i++)
            {
                var gen = GameData.Generators[i];
                var owned = game.Owned[i];
                var canBuy = game.CanBuy(i);

                rowInfoTexts[i].text = $"{gen.name}  (x{owned})\n{gen.flavor}";
                rowCostTexts[i].text = $"{Economy.Format(game.CostOf(i))}";
                rowButtons[i].interactable = canBuy;
                rowButtons[i].GetComponent<Image>().color =
                    canBuy ? new Color(0.20f, 0.45f, 0.30f) : new Color(0.25f, 0.25f, 0.28f);
                rowButtons[i].transform.parent.GetComponent<Image>().color =
                    canBuy ? RowAffordable : RowLocked;
            }
```

with:

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
```

- [ ] **Step 4: Run test to verify it passes**

Same run instructions as Task 1, Step 2.
Expected: **PASS** — both `LocalizationTests` methods green, `EconomyTests` still green.

- [ ] **Step 5: Manual verification (Play Mode)**

`GameUI`/`Game` aren't unit-tested in this codebase (only pure-logic `Economy.cs` is — see `CLAUDE.md`), so this step is the equivalent manual check. Switch to the Unity Editor, press **Play**. Confirm the 12 shop rows still show the exact same English names/flavors as before this task (they should be pixel-identical to pre-task behavior — only the code path changed, not the displayed English text). Stop Play.

- [ ] **Step 6: Commit**

```bash
cd /home/fmanzoni-lx/Documents/Pessoal/wont-fix
git add Assets/Scripts/GameData.cs Assets/Scripts/Localization.cs Assets/Scripts/GameUI.cs Assets/Tests/Editor/LocalizationTests.cs
git commit -m "$(cat <<'EOF'
Localize generator shop rows

GameData.Generator now carries a stable id instead of raw display
strings; GameUI's row loop resolves name/flavor through
Localization.Get. Decouples game balance from translated content.
EOF
)"
```

---

### Task 3: Header, RUN TEST button, and live language toggle

**Files:**
- Modify: `Assets/Scripts/GameUI.cs` (fields, `OnEnable`/`OnDisable`, `BuildHeader`, `BuildRunButton`, top of `Refresh`)

**Interfaces:**
- Consumes: `Localization.Current`, `Localization.Get`, `Localization.SetLanguage`, `Localization.Changed`, `Language` enum from Task 1. `GameData.Generators[i].id` pattern from Task 2 is unaffected by this task (the row loop already reflects Task 2's edit and isn't touched again here).
- Produces: nothing further consumed by another task — this completes the i18n milestone.

This is a UI-only change with no new automated test, matching how `GameUI`/`Game` are already covered by manual Play verification rather than NUnit tests in this codebase.

- [ ] **Step 1: Add the two new stored text fields**

In `Assets/Scripts/GameUI.cs`, replace:

```csharp
        Game game;
        Text bugsText;
        Text rateText;
```

with:

```csharp
        Game game;
        Text bugsText;
        Text rateText;
        Text runTestLabel;
        Text languageToggleText;
```

- [ ] **Step 2: Subscribe to language changes**

Replace:

```csharp
        void OnEnable()
        {
            if (game != null) game.Changed += Refresh;
        }

        void OnDisable()
        {
            if (game != null) game.Changed -= Refresh;
        }
```

with:

```csharp
        void OnEnable()
        {
            if (game != null) game.Changed += Refresh;
            Localization.Changed += Refresh;
        }

        void OnDisable()
        {
            if (game != null) game.Changed -= Refresh;
            Localization.Changed -= Refresh;
        }
```

- [ ] **Step 3: Rebuild the header with a language toggle**

Replace the entire `BuildHeader` method:

```csharp
        void BuildHeader(Transform parent)
        {
            var header = CreatePanel(parent, "Header", Panel).gameObject;
            AddFixedHeight(header, 100);

            var layout = header.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            bugsText = CreateText(header.transform, "0.0 bugs found", 32, TerminalGreen, FontStyle.Bold, TextAnchor.MiddleLeft);
            rateText = CreateText(header.transform, "0.0 bugs/sec", 18, DimText, FontStyle.Normal, TextAnchor.MiddleLeft);
        }
```

with:

```csharp
        void BuildHeader(Transform parent)
        {
            var header = CreatePanel(parent, "Header", Panel).gameObject;
            AddFixedHeight(header, 120);

            var layout = header.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            bugsText = CreateText(header.transform, "", 32, TerminalGreen, FontStyle.Bold, TextAnchor.MiddleLeft);

            var subHeader = new GameObject("SubHeader", typeof(RectTransform));
            subHeader.transform.SetParent(header.transform, false);
            AddFixedHeight(subHeader, 30);

            var subLayout = subHeader.AddComponent<HorizontalLayoutGroup>();
            subLayout.spacing = 12;
            subLayout.childControlWidth = true;
            subLayout.childControlHeight = true;
            subLayout.childForceExpandHeight = true;

            rateText = CreateText(subHeader.transform, "", 18, DimText, FontStyle.Normal, TextAnchor.MiddleLeft);
            AddFlexibleWidth(rateText.gameObject);

            var toggleGO = CreatePanel(subHeader.transform, "LanguageToggle", Panel).gameObject;
            AddFixedWidth(toggleGO, 90);
            var toggleButton = toggleGO.AddComponent<Button>();
            toggleButton.targetGraphic = toggleGO.GetComponent<Image>();
            languageToggleText = CreateText(toggleGO.transform, "", 16, TerminalGreen, FontStyle.Bold, TextAnchor.MiddleCenter);
            toggleButton.onClick.AddListener(() => Localization.SetLanguage(
                Localization.Current == Language.English ? Language.PortugueseBR : Language.English));
        }
```

- [ ] **Step 4: Store the RUN TEST label reference**

Replace:

```csharp
            CreateText(buttonGO.transform, "RUN TEST", 26, White, FontStyle.Bold, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => game.Click());
```

with:

```csharp
            runTestLabel = CreateText(buttonGO.transform, "", 26, White, FontStyle.Bold, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => game.Click());
```

- [ ] **Step 5: Make Refresh() redraw the static labels too**

Replace the first two lines inside `Refresh()`:

```csharp
        void Refresh()
        {
            bugsText.text = $"{Economy.Format(game.Bugs)} bugs found";
            rateText.text = $"{Economy.Format(game.BugsPerSecond)} bugs/sec";
```

with:

```csharp
        void Refresh()
        {
            runTestLabel.text = Localization.Get("ui.run_test");
            languageToggleText.text = Localization.Current == Language.English ? "PT-BR" : "EN";

            bugsText.text = string.Format(Localization.Get("ui.bugs_found"), Economy.Format(game.Bugs));
            rateText.text = string.Format(Localization.Get("ui.bugs_per_second"), Economy.Format(game.BugsPerSecond));
```

- [ ] **Step 6: Verify it compiles and passes existing tests**

Same run instructions as Task 1, Step 2.
Expected: **PASS** — all `EconomyTests` and `LocalizationTests` green (this task adds no new automated test, so the count doesn't change; it's a regression check that the UI edits didn't break compilation).

- [ ] **Step 7: Manual verification (Play Mode)**

Switch to the Unity Editor, press **Play**. Confirm:
- The header shows the bug count and rate exactly as before.
- The `RUN TEST` button still says "RUN TEST" (English is still the effective language unless your OS locale is Portuguese or you'd previously toggled it).
- A small button reading `PT-BR` appears next to the rate text.
- Click it: the header, the `RUN TEST` button (now "EXECUTAR TESTE"), and all 12 shop rows switch to Portuguese instantly, and the toggle now reads `EN`.
- Click it again: everything switches back to English.
- Stop Play, press Play again: the language you left it on is still selected (persisted via `PlayerPrefs`).

- [ ] **Step 8: Commit**

```bash
cd /home/fmanzoni-lx/Documents/Pessoal/wont-fix
git add Assets/Scripts/GameUI.cs
git commit -m "$(cat <<'EOF'
Wire header, RUN TEST button, and language toggle to Localization

Refresh() now redraws every piece of text, static and dynamic, so a
language switch is just calling it again. Adds a toggle button in
the header showing the other language's code.
EOF
)"
```
