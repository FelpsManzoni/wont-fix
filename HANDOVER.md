# Won't Fix Project Handover

**Date:** 2026-09-14  
**Project:** Won't Fix — QA-themed incremental clicker in Unity  
**Status:** Mid-implementation (subagent-driven development, Task 1 of 5 staged, awaiting test confirmation)

---

## Session Context: Key Decisions & Brainstorming Notes

This section captures the brainstorming and design decisions from the chat session that led to the current spec and plan.

### Unlock Mechanics — Why "Own 1 of Previous"?

**Question asked:** What should trigger a generator revealing itself in the shop?

**Options considered:**
- Own 1 of the previous generator (Recommended) ✅ **CHOSEN**
- Lifetime bugs threshold
- Mix of both

**Decision & why:**
Simple, predictable chain (Junior Tester visible from start → buying first reveals Test Case Spreadsheet, etc.). Naturally "sticky" — owned counts never decrease, so "once revealed, stays revealed" is automatic with zero new save state. Matches genre convention (Cookie Clicker's upgrade reveal pattern). The unlock state is derived purely from existing data (`Owned[]`), which only increases, eliminating a whole class of edge cases.

**Downstream impact:** Same rule applies to upgrades (own 1 of a generator → its upgrade appears). All unlock logic is identical: `index == 0 || Owned[index-1] >= 1`.

### Architecture Decision: Progression.cs

**Question:** Where should unlock checks and multiplier math live?

**Decision:** New file `Progression.cs`, pure static class with zero `UnityEngine` dependency.

**Why:** 
- Mirrors `Economy.cs`'s pattern (testable pure logic, separate from MonoBehaviour orchestration)
- Unlock checks and multiplier math are genuine branching logic, easy to get subtly wrong
- Pure-logic functions can be tested headlessly (EditMode tests), not buried in `Game.cs` which stays manual-only
- Decouples logic from MonoBehaviour lifecycle issues

**Functions:**
- `IsGeneratorUnlocked(int index, int[] owned)`
- `IsUpgradeUnlocked(Upgrade upgrade, int[] owned)`  
- `ClickMultiplier(bool[] purchased)`
- `GeneratorMultiplier(string generatorId, bool[] purchased)`

All four passed verbatim into the plan with no changes.

### Upgrade Data Design: generatorId Double Duty

**Problem:** Upgrades need to know:
1. Which generator must be owned to unlock them
2. Which generator they boost (for Generator-effect upgrades)

**Solution:** Single `generatorId` field does both.

**Why:**
- All 4 upgrades in scope have the gating generator = the boost target (Second Monitor gates on Junior Tester, boosts Junior Tester; Explicit Waits gates on Selenium, boosts Selenium; etc.)
- Eliminates a second field and special-case logic
- Reproducible Steps (Click-effect) still sets `generatorId = "junior_tester"` for unlock-gating only (nothing to boost)
- One field, two meanings, no redundancy

**Struct definition:** Locked in as:
```csharp
public struct Upgrade {
    public string id;
    public string generatorId;  // gates unlock AND boost target
    public double cost;
    public UpgradeEffect effect;
    public double multiplier;
}
```

### UI: SetActive vs. Dynamic Instantiate/Destroy

**Question:** How do we show/hide upgrade rows as they unlock?

**Options:**
- Create/destroy rows dynamically (lots of instantiate/destroy calls, layout thrashing)
- SetActive(false) to hide, SetActive(true) to show ✅ **CHOSEN**

**Why:**
- `VerticalLayoutGroup` + `ContentSizeFitter` natively skip inactive children in layout (they take zero space)
- All rows are created upfront once, toggled every `Refresh()` call
- No dynamic instantiate/destroy, no layout recalculation churn
- Simplest code: one flag per row

**Implementation:** Every row starts `SetActive(false)`, `Refresh()` toggles based on `Progression.IsGeneratorUnlocked/IsUpgradeUnlocked`.

### Content Finalization: 4 Upgrades, 10× Cost

**Upgrades chosen:**
1. Reproducible Steps (junior_tester) — ×2 click, cost 150
2. Second Monitor (junior_tester) — ×2 Junior Tester, cost 150
3. Explicit Waits (selenium_script) — ×2 Selenium, cost 11,000
4. Flaky Test Quarantine (ci_pipeline) — ×2 CI Pipeline, cost 120,000

**Cost formula:** 10× the target generator's base cost
- Junior Tester base cost 15 → upgrade 150
- Selenium base cost 1,100 → upgrade 11,000
- CI Pipeline base cost 12,000 → upgrade 120,000

**Why 10×?**
- Proportionate to existing cost curve (~×10 per tier)
- Easy to eyeball-sanity-check during balance review
- Simple to retune later (one column in a table)

**Why these 4 generators?** Covers the early game where players spend the most time (tiers 0-3 out of 12). Keeps scope small and shippable. Prestige, Achievements, P0 events are separate future milestones.

### Row Interleaving in UI

**How upgrades appear in the shop:**
Generator 0 (Junior Tester) → Reproducible Steps → Second Monitor → Generator 1 (Test Case Spreadsheet) → Generator 2 (Selenium) → Explicit Waits → Generator 3 (CI Pipeline) → Flaky Test Quarantine → Generators 4-11 (no upgrades).

**Why interleave, not separate sections?**
- One scrolling list, natural reading order
- Upgrade appears right after the generator it boosts (context clarity)
- No second ScrollRect, no "sync two lists" logic
- Mirrors the mental model: "I own Junior Testers, so here's the Second Monitor upgrade I can now buy"

**BuildShop loop logic:**
```csharp
for (var i = 0; i < GameData.Generators.Length; i++)
{
    BuildRow(contentGO.transform, i);
    for (var u = 0; u < GameData.Upgrades.Length; u++)
        if (GameData.Upgrades[u].generatorId == GameData.Generators[i].id)
            BuildUpgradeRow(contentGO.transform, u);
}
```

### Test Coverage Decisions

**What gets automated tests:**
- `Progression.cs` — pure logic, all 8 test methods (unlock chain, multiplier math)
- `Localization.cs` — extended to cover new `upg.*` keys and format placeholders
- `Economy.cs` — unchanged, but runs as regression check

**What stays manual (Play Mode):**
- `Game.cs` — verifies click multiplier actually applies, purchases work, rate recalculates
- `GameUI.cs` — verifies rows appear/disappear, purchased upgrades show "OWNED", language toggle works on upgrade labels

**Why:** Game/GameUI are MonoBehaviours, hard to test headlessly. The logic they orchestrate (Progression, Localization) is tested; the integration is verified by hand in one Play session.

### Workflow Decision: Commits via GitKraken

**Rule:** Implementer subagents stage files and report commit messages; human commits via GitKraken.

**Why:**
- Preserves clean git authorship (all commits by Felipe, no Claude credits in history)
- Human involvement enforces a review gate (you see what's being committed before it lands)
- Avoids git config global pollution (identity stays repo-local)
- Matches this project's established workflow (used for i18n milestone, works well)

**How it works in SDD loop:**
1. Subagent implements, runs tests, stages files
2. Subagent reports: "Status: DONE, files staged, commit message: ..."
3. Controller relays message to human
4. Human commits via GitKraken GUI (one click)
5. Controller verifies commit via `git log`, generates review package, proceeds

### Scope Exclusions (Deliberate Out-of-Scope)

**Not in this milestone:**
- Prestige system ("Ship a Release" — next milestone)
- Achievements (separate milestone)
- P0 events (golden-cookie equivalent — Felipe wants to brainstorm separately)
- More than 4 upgrades
- Upgrades for every generator (only tiers 0-3 covered)
- Unlock conditions other than "own ≥1 of X generator" (no lifetime-bug thresholds, no multi-condition gates)

**Why these boundaries?**
- Keeps this iteration shippable and reviewable
- Each milestone is one cohesive feature, tested end-to-end
- P0 events especially — Felipe has a specific creative vision for them, deserves its own design session

### Internationalization Assumption

**Assumption built into the plan:** All new strings have EN/PT-BR translations.

**Why it matters:**
- Localization.Get() falls back to the raw key if missing, silently rendering broken UI
- Tests catch this: `LocalizationTests.Get_UiKeys_NonEmptyInBothLanguages` asserts every key translates in both languages
- Future developer adding a string without translations will see test failures immediately

**For new account:** When adding strings, add them to BOTH En and PtBr dictionaries or tests will fail. This is intentional — the test is the contract.

---

## Quick Start for New Account

1. Clone: `git clone git@github.com:FelpsManzoni/wont-fix.git /home/fmanzoni-lx/Documents/Pessoal/wont-fix`
2. Branch: `git checkout feature/unlockable-upgrades`
3. Read this file entirely, then read `CLAUDE.md` (project conventions)
4. Verify Test Runner: **Window → General → Test Runner → EditMode → Run All** (expect EconomyTests + LocalizationTests to pass)
5. Pick up at **Pending Work → Task 2** section below

---

## Project Overview

**Won't Fix** is a first Unity game: a Cookie Clicker–style incremental clicker with a QA-engineer theme. Click `RUN TEST` to find bugs; buy automated testers and tools (generators) that find bugs for you. The game is fully playable end-to-end.

**Core loop (Milestone 1):** ✅ Complete
- Click `RUN TEST` to accumulate bugs
- Buy from 12 generators (Junior Tester → Production Users)
- Cost curve: ×1.15 per purchase, ~×10 cost per tier, ~×8 output per tier
- Offline progress: idle time accrues bugs
- Save/load via `PlayerPrefs` + `JsonUtility`
- Full internationalization: English and Portuguese-BR (just completed)

**Current milestone (1+2):** 🚧 In progress
- Unlock system: generators hidden until you own ≥1 of the previous one
- Upgrades system: 4 one-off boosts (Reproducible Steps, Second Monitor, Explicit Waits, Flaky Test Quarantine)
- Upgrades unlock on the same rule (own ≥1 of a gating generator)
- All UI changes, no new gameplay mechanics

---

## Repository Structure

```
wont-fix/
├── .git/                                    # Git repository
├── .gitignore                               # Unity patterns
├── CLAUDE.md                                # Project conventions, test/compile commands
├── HANDOVER.md                              # This file
│
├── docs/
│   ├── superpowers/
│   │   ├── specs/
│   │   │   ├── 2026-08-28-internationalization-design.md     # ✅ Approved spec (i18n, completed)
│   │   │   └── 2026-09-02-unlockable-generators-and-upgrades-design.md  # 🚧 Current spec (this milestone)
│   │   └── plans/
│   │       ├── 2026-09-01-internationalization.md            # ✅ Completed plan (i18n)
│   │       └── 2026-09-02-unlockable-generators-and-upgrades.md  # 🚧 Current plan (5 tasks)
│
├── Assets/
│   ├── Scripts/
│   │   ├── WontFix.asmdef                   # Gameplay assembly definition
│   │   ├── Economy.cs                       # Pure math: cost curve, formatting, offline accrual
│   │   ├── GameData.cs                      # Balance table (Generators) + Upgrade content
│   │   ├── Game.cs                          # MonoBehaviour: click, buy, tick, save/load
│   │   ├── GameUI.cs                        # Canvas built at runtime, all UI construction
│   │   ├── Localization.cs                  # EN/PT-BR static dictionaries, self-initializing
│   │   └── Progression.cs                   # 🚧 NEW (Task 2): unlock logic + multiplier math
│   │
│   ├── Tests/
│   │   ├── Editor/
│   │   │   ├── WontFix.Tests.asmdef         # Test assembly definition
│   │   │   ├── EconomyTests.cs              # ✅ Tests: cost curve, offline math
│   │   │   ├── LocalizationTests.cs         # ✅ Tests: key completeness + format placeholders
│   │   │   └── ProgressionTests.cs          # 🚧 NEW (Task 2): unlock chain + multiplier math
│
├── Temp/
│   └── UnityLockfile                        # Present when Editor is open (blocks headless tests)
│
└── .superpowers/
    └── sdd/
        └── 2026-09-02-unlockable-generators-and-upgrades/
            ├── progress.md                  # SDD execution ledger
            ├── task-N-brief.md              # Task requirements (auto-generated)
            ├── task-N-report.md             # Implementer's report (auto-generated)
            └── review-*.diff                # Review packages (auto-generated)
```

---

## Current Git State

**Branch:** `feature/unlockable-upgrades`  
**Base:** `8c4b0d9` (main, post-i18n-merge PR #1)  
**HEAD (last commit):** `fe266e2` "docs: add unlockable generators + upgrades design spec"  
**Last task work:** Task 1 (`GameData.cs`) staged, not yet committed

```
git log --oneline feature/unlockable-upgrades (first 5):
fe266e2 docs: add unlockable generators + upgrades design spec    [← current HEAD, spec only]
8c4b0d9 Merge pull request #1 from FelpsManzoni/feature/i18n      [← branch base, main tip]
ee84f50 feat: i18n - format-guard test, safer persisted-language cast, toggle
0838250 feat: Wire header, RUN TEST button, and language toggle to Localization
fe50ac3 feat: Localize generator shop rows
```

**Remote:** `git@github.com:FelpsManzoni/wont-fix.git` (personal account, public)  
**Local identity:** Felipe Sonntag Manzoni <felipemanzoni3@gmail.com> (configured locally in `.git/config`, NOT global)

---

## Pending Work

### Task 1: GameData.cs — Upgrade Content ✅ STAGED, AWAITING COMMIT

**Status:** Implemented, staged, ready for human commit via GitKraken.

**What was done:**
- Added `enum UpgradeEffect { Click, Generator }`
- Added `struct Upgrade { id, generatorId, cost, effect, multiplier }` with constructor
- Added `static readonly Upgrade[] GameData.Upgrades` with 4 entries (Reproducible Steps, Second Monitor, Explicit Waits, Flaky Test Quarantine)
- Added `static int GameData.IndexOfGenerator(string id)` helper
- Extended `SaveData` with `public bool[] upgradesPurchased`

**Staged files:**
```
Assets/Scripts/GameData.cs
```

**Next step:**
1. **Human: commit via GitKraken** with this message:
   ```
   Add upgrade content to GameData

   Upgrade struct + UpgradeEffect enum + the 4 upgrades' costs/targets,
   alongside the existing Generator content. SaveData gains
   upgradesPurchased for the next task. Nothing consumes this yet.
   ```
2. **New account:** Run Test Runner to confirm regression (should see all existing tests green)
3. **New account:** Proceed to Task 2

### Task 2: Progression.cs — Unlock & Multiplier Logic (NOT STARTED)

**Files to create/modify:**
- Create: `Assets/Scripts/Progression.cs` (pure static class, zero UnityEngine dependency)
- Create: `Assets/Tests/Editor/ProgressionTests.cs` (8 test methods, TDD)

**What it does:**
- `IsGeneratorUnlocked(int index, int[] owned)` — true if index==0 OR owned[index-1]>=1
- `IsUpgradeUnlocked(Upgrade upgrade, int[] owned)` — true if owned[gatingGenerator]>=1
- `ClickMultiplier(bool[] purchased)` — product of all Click-effect upgrade multipliers
- `GeneratorMultiplier(string generatorId, bool[] purchased)` — product of matching Generator-effect upgrade multipliers

**See:** Full plan at `docs/superpowers/plans/2026-09-02-unlockable-generators-and-upgrades.md`, Task 2 section (complete code included)

### Task 3: Localization.cs — Upgrade Translations (NOT STARTED)

**Files to modify:**
- `Assets/Scripts/Localization.cs` — add 9 new keys to both En and PtBr dictionaries
- `Assets/Tests/Editor/LocalizationTests.cs` — extend completeness tests for new keys

**Keys to add:**
```
upg.reproducible_steps.name/flavor
upg.second_monitor.name/flavor
upg.explicit_waits.name/flavor
upg.flaky_test_quarantine.name/flavor
ui.owned
```

**See:** Full plan, Task 3 section (all text included)

### Task 4: Game.cs — Wire Progression & Upgrades (NOT STARTED)

**Files to modify:**
- `Assets/Scripts/Game.cs` — 40 lines changed/added

**What it does:**
- `Click()` now routes through `Progression.ClickMultiplier(UpgradesPurchased)`
- `RecalculateRate()` applies `Progression.GeneratorMultiplier` to each generator's rate
- New `BuyUpgrade(int index)` method (mirrors `Buy`)
- New `CanBuyUpgrade(int index)` method (one-time purchase, not repeatable)
- New `IsGeneratorUnlocked(int index)` / `IsUpgradeUnlocked(int index)` methods
- New `public bool[] UpgradesPurchased` property
- Save/load now persist `upgradesPurchased`

**See:** Full plan, Task 4 section

### Task 5: GameUI.cs — Conditional Rows & Upgrade UI (NOT STARTED)

**Files to modify:**
- `Assets/Scripts/GameUI.cs` — ~70 lines changed/added across 4 methods

**What it does:**
- Adds row-tracking lists: `rowObjects`, `upgradeRowObjects`, and respective `*InfoTexts`, `*CostTexts`, `*Buttons`
- `BuildShop()` interleaves upgrade rows right after their gating generator
- New `BuildUpgradeRow(Transform parent, int index)` method
- All rows start `SetActive(false)`, `Refresh()` toggles visibility per unlock status
- Purchased upgrades show "OWNED" label instead of price
- Refresh loops handle both visibility toggling and text updates

**See:** Full plan, Task 5 section

### Final Whole-Branch Review (NOT STARTED)

After all 5 tasks complete and are individually approved, a final cross-branch review runs on the most capable model, checking for cross-task consistency, integration, and any issues that only emerge when all pieces are present.

---

## Workflow & Constraints

### Absolute Rules (Non-negotiable)

1. **No Unity Editor GUI authoring.** No dragging references into Inspector, no hand-authoring Canvas hierarchies. Everything is code. The ONLY Editor steps are: creating the project (done), adding one component to the scene (done for Milestone 1, preserved). All new work is text files.

2. **Commits via GitKraken ONLY.** Subagents and Claude never run `git commit`. Implementers stage files and report the commit message; a human (you) commits via GitKraken. This preserves commit authorship and keeps the history clean.

3. **Local git identity only.** Identity is configured in `.git/config` (repo-local), NOT `~/.gitconfig` (global). Never touch global config. Never add Claude/Anthropic/SiDi/co-author references to commit messages.

4. **Two assemblies, not one:** `WontFix` (gameplay) and `WontFix.Tests` (tests) are separate `.asmdef` files. Unity compiles custom asmdefs before the default `Assembly-CSharp`, so tests can reference gameplay but not vice versa.

5. **Two test categories:**
   - **EditMode (automated, headless-testable):** `Economy.cs`, `Localization.cs`, `Progression.cs` (pure logic, zero UnityEngine dependency)
   - **Play Mode (manual, interactive):** `Game.cs`, `GameUI.cs` (MonoBehaviours verified by hand via Play button; no automated tests)

### Verification Commands

**Editor open (normal case):**
```
Window → General → Test Runner → EditMode → Run All
```

**Editor closed (if needed):**
```bash
~/Unity/Hub/Editor/6000.5.9f1/Editor/Unity -batchmode -nographics -logFile - \
  -projectPath /home/fmanzoni-lx/Documents/Pessoal/wont-fix \
  -runTests -testPlatform EditMode -testResults /tmp/wont-fix-results.xml
```

---

## Execution Model (Subagent-Driven Development)

This project uses **subagent-driven development** for multi-task features:

1. **Per-task subagents:** Each of the 5 tasks is dispatched to a separate implementer subagent with:
   - The task brief (auto-extracted from the plan)
   - Complete code and test cases (copy-paste verbatim)
   - Global constraints and interfaces from prior tasks
   - Report file path (implementer writes a detailed report)

2. **Per-task reviews:** After each subagent completes, a task reviewer checks spec compliance and code quality before the task is approved.

3. **Fix loops:** If review finds issues, the implementer is resumed (rounds 1-3) or a fresh implementer is dispatched (rounds 4-5 with escalation). Every fix round is scoped and re-reviewed.

4. **Whole-branch review:** After all tasks are individually approved, a final review checks cross-task consistency and integration.

5. **Ledger:** Execution is tracked in `.superpowers/sdd/2026-09-02-unlockable-generators-and-upgrades/progress.md` — the recovery map if context is lost.

**For the new account:** Dispatch via `superpowers:subagent-driven-development` skill, passing the plan file path. The skill will:
- Check for an existing ledger (find it, resume from where you left off)
- Extract task briefs on demand
- Generate review packages
- Manage the fix loop

---

## Project Preferences & Conventions

### Code Style
- No comments unless WHY is non-obvious (hidden constraint, workaround, subtle invariant)
- No docstrings, no multi-line comment blocks
- One-liners for `ponytail` simplifications that cut a corner (naming the ceiling and upgrade path)

### Testing Philosophy
- **Pure logic gets tests:** `Economy.cs`, `Localization.cs`, `Progression.cs` — NUnit EditMode, headless-testable
- **MonoBehaviour logic doesn't:** `Game.cs`, `GameUI.cs` — verified manually via Play Mode
- Tests assert real behavior, not mocks; no test fixtures beyond SetUp/TearDown
- One small runnable check per non-trivial logic block (no test-framework scaffolding)

### Git Practices
- Feature branches (e.g., `feature/i18n`, `feature/unlockable-upgrades`) forked from `main`
- Pull requests on GitHub for code review (even on personal accounts, for the review record)
- Squash or rebase before merge — no merge commits, keep history linear
- Commit messages: imperative mood, body paragraph explaining WHY, no co-author lines

### Localization
- Hand-rolled static dictionaries (`En`/`PtBr` in `Localization.cs`), not Unity's official Localization package
- Key pattern: `<domain>.<id>.<field>` (e.g., `gen.junior_tester.name`, `upg.reproducible_steps.flavor`, `ui.run_test`)
- Every new string is BOTH EN and PT-BR or tests fail
- Format strings use `{0}`, `{1}`, etc.; test guards that placeholders are present

### Balance & Content
- `GameData.cs` is the truth for all numeric balance (costs, rates, upgrade multipliers)
- Cost curve: `baseCost * 1.15^owned` per purchase
- Generator progression: 12 generators, ~×10 cost per tier, ~×8 output per tier
- Upgrade multipliers: all ×2 (doubling)

---

## Editor & Project Setup

**Unity Version:** 6000.5.9f1 (2024 LTS)  
**Render Pipeline:** Built-In (not URP — game is pure Canvas UI)  
**Input Handling:** Input System package only (Active Input Handler = 1 in ProjectSettings); no legacy `StandaloneInputModule`  
**UI Framework:** Legacy `UnityEngine.UI.Text` (NOT TextMeshPro) with OS monospace font fallback  
**Scene:** `SampleScene.unity` — contains one empty GameObject "Game" with the "Game" MonoBehaviour attached (autolinking to GameUI via `[RequireComponent]`)

**First-run steps already done:**
- Project created via Hub
- Git initialized, initial commits on `main`
- Internationalization feature completed and merged (PR #1)
- Branch `feature/unlockable-upgrades` created

**For new account:**
- Just `git clone` and switch to `feature/unlockable-upgrades`
- Open the Editor, confirm it compiles
- Run Test Runner to verify the existing test suite is green
- Ready to dispatch Task 2

---

## Key Files for Reference

| File | Purpose | Status |
|------|---------|--------|
| `CLAUDE.md` | Project conventions, test/compile commands, CI info | ✅ Up-to-date |
| `docs/superpowers/specs/2026-09-02-unlockable-generators-and-upgrades-design.md` | Feature spec: data model, logic, UI, content, testing | 🚧 Current (complete) |
| `docs/superpowers/plans/2026-09-02-unlockable-generators-and-upgrades.md` | Implementation plan: 5 tasks, all code included verbatim, step-by-step | 🚧 Current (complete) |
| `.superpowers/sdd/2026-09-02-unlockable-generators-and-upgrades/progress.md` | SDD execution ledger (recovery map) | 🚧 Current |

---

## Common Questions

**Q: How do I run tests?**  
A: With Editor open: **Window → General → Test Runner → EditMode → Run All**. Or headless (see Verification Commands above).

**Q: How do I add a string the player sees?**  
A: Add it to Localization.cs's `En` and `PtBr` dictionaries with a key matching `<domain>.<id>.<field>`. Tests will fail if you forget either language.

**Q: The Editor holds a lock — can I run tests headlessly?**  
A: Yes, close the Editor and use the batchmode command (see Verification Commands). Or keep it open and use the in-Editor Test Runner.

**Q: What if I need to make a decision not in the spec or plan?**  
A: The spec is the authority. If the spec doesn't cover it, check the plan. If neither does, make the decision with YAGNI as your guide, ledger it in the SDD progress file, and carry it forward into the next task's dispatch.

**Q: Can I add new tests or new test categories?**  
A: Only for new pure-logic files (like Progression.cs). Don't add automated tests for Game.cs/GameUI.cs — they stay manual. Don't add test fixtures or multi-step test suites — keep them minimal.

**Q: I found a bug in completed code (i18n, Milestone 1). Do I fix it?**  
A: Small bug fixes on already-merged code go on a separate branch (`bugfix/...`) and PR, outside the current task series. Don't mix them into the active feature branch.

---

## What Happens Next

**For the current account (this one):**
- This file becomes your handover summary
- Work pauses; the SDD execution state is preserved in the ledger

**For the new account:**
1. Clone the repo and check out `feature/unlockable-upgrades`
2. Open this file and read it fully
3. Read `CLAUDE.md` for project conventions
4. Read the spec: `docs/superpowers/specs/2026-09-02-unlockable-generators-and-upgrades-design.md`
5. Read the plan: `docs/superpowers/plans/2026-09-02-unlockable-generators-and-upgrades.md`
6. Verify Test Runner: **Window → General → Test Runner → EditMode → Run All**
7. Dispatch `superpowers:subagent-driven-development` with the plan file path
8. The skill will resume from Task 2 (it reads the ledger and skips Task 1, which is already staged)
9. After Task 1 is committed (by you via GitKraken), Task 2 begins

---

**Handover complete.** Good luck with the new account. The project is well-structured and the plan is crystal clear — all 5 tasks have complete code ready to copy-paste.
