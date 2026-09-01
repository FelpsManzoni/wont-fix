# Won't Fix

QA-themed incremental clicker. Click `RUN TEST` to find bugs, spend bugs on
generators that find bugs automatically. See `../.claude/plans/i-have-installed-the-inherited-nova.md`
for the full design (generator ladder, later milestones).

## Project

- Unity **6000.5.9f1**, Built-In render pipeline, 2D template.
- Active Input Handling is **Input System package only** (`activeInputHandler: 1`
  in ProjectSettings). Any new UI input wiring must use `InputSystemUIInputModule`,
  not the legacy `StandaloneInputModule` -- the old one throws at runtime here.
- No TextMeshPro. UI uses legacy `UnityEngine.UI.Text` with an OS monospace font
  (`GameUI.ResolveFont`), specifically to avoid the one-time "Import TMP Essentials"
  Editor dialog, which nobody can click through headlessly.

## Code layout

- `Assets/Scripts/` -- gameplay, its own assembly (`WontFix.asmdef`).
- `Assets/Tests/Editor/` -- `WontFix.Tests.asmdef`, references `WontFix`.

Two assemblies, not one, because Unity compiles custom (asmdef) assemblies
*before* the implicit `Assembly-CSharp` -- a test asmdef can never reference
`Assembly-CSharp`. Gameplay code needs its own asmdef so tests can see it.

- `Economy.cs` -- pure math (cost curve, number formatting, offline accrual).
  No UnityEngine dependency, so it's the only thing worth unit testing.
- `GameData.cs` -- the 12-generator content table, in code (not a
  ScriptableObject) because nobody can hand-fill Inspector rows headlessly.
- `Game.cs` -- the one MonoBehaviour: click, buy, tick, save/load via
  `PlayerPrefs` + `JsonUtility`. `[RequireComponent(typeof(GameUI))]` so
  adding `Game` in the Inspector is the only wiring step.
- `GameUI.cs` -- builds the entire Canvas at runtime (no scene authoring
  possible headlessly). Dark "bug tracker" look: near-black background,
  terminal-green numbers.

## Verifying changes

The Editor holds a lock on the project while open (`Temp/UnityLockfile`), so
a second headless instance can't run concurrently. Two ways to check work:

**Editor open (normal case):** switch focus to Unity, let it recompile
(watch the spinner, bottom-right), check the Console for errors. Run tests
via **Window > General > Test Runner > EditMode > Run All**.

**Editor closed:** compile-check and run tests from the terminal:

```bash
~/Unity/Hub/Editor/6000.5.9f1/Editor/Unity -batchmode -nographics -logFile - \
  -projectPath /home/fmanzoni-lx/Documents/Pessoal/wont-fix \
  -runTests -testPlatform EditMode -testResults /tmp/wont-fix-results.xml
```
