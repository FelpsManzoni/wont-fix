# Internationalization (English / Portuguese-BR) — Design

Status: approved
Date: 2026-08-28

## Context

Milestone 1 (playable core loop) shipped with every UI string hardcoded in English:
button labels, the bug-count/rate header, and all 12 generators' names and flavor
text in `GameData.cs` / `GameUI.cs`. Felipe wants the game to always support at
least English (US) and Portuguese (BR) going forward. This is being designed
*before* the next gameplay milestone (auto-clicker reveal-on-unlock + upgrades)
specifically to avoid piling up more hardcoded strings that would need a second
retrofit.

Constraint carried over from milestone 1: no Unity Editor GUI authoring. Rules
out Unity's official Localization package, whose authoring model (String Table
Collection assets) is built around Editor windows.

## Approach

Hand-rolled static dictionaries, mirroring the pattern `GameData.cs` already uses
for generator content: data lives in code, not in Inspector-authored assets.
Rejected alternatives: JSON/CSV resource files (adds a parsing layer and a
schema for zero benefit when both editors of the content are code-literate) and
Unity's Localization package (its authoring flow assumes Editor-window access,
which we don't have).

## Data model

`Assets/Scripts/Localization.cs`, in the `WontFix` assembly:

```csharp
public enum Language { English, PortugueseBR }

public static class Localization
{
    public static Language Current { get; private set; }
    public static event Action Changed;

    public static void SetLanguage(Language language);
    public static string Get(string key);
}
```

- Two `Dictionary<string, string>` tables (`En`, `PtBr`), hand-written, keyed by
  plain string constants (e.g. `"ui.run_test"`, `"ui.bugs_found"`,
  `"gen.junior_tester.name"`).
- `Get(key)` falls back to returning `key` itself if a translation is missing,
  so a gap degrades to a visible bug rather than a blank label or a crash.

## Persistence & first-run detection

Language preference lives in its **own** `PlayerPrefs` key, independent of
`Game`'s save blob — it's infrastructure, not game progress, and this avoids
any future coupling if progress ever gets reset independently of language.

`Localization` is **self-initializing on first use**, not wired to any
MonoBehaviour's `Awake()`. Unity does not guarantee `Game.Awake()` runs before
`GameUI.Awake()` just because `GameUI` is a required component, so instead of
depending on that order, `Get()`/`Current` lazily trigger one-time init the
first time either is touched.

First-run detection: if the dedicated `PlayerPrefs` key doesn't exist,
`Application.systemLanguage == SystemLanguage.Portuguese` maps to
`PortugueseBR`, everything else maps to `English`. The detected value is
persisted immediately, so detection runs at most once per install.

## `GameData.cs` retrofit

Each `Generator` currently carries raw English `name`/`flavor` strings. These
become a stable `id` (e.g. `"junior_tester"`), decoupling game balance (cost,
rate) from display text. The two localization dictionaries carry
`"gen.<id>.name"` and `"gen.<id>.flavor"` entries for both languages.

## UI retrofit

- **Language toggle**: a small button in the `Header` panel showing the
  *other* language's code (`"PT-BR"` while in English, `"EN"` while in
  Portuguese). This label is a fixed literal, not a translated string — it
  names a language, not UI content. Clicking calls `Localization.SetLanguage`.
- **Live text swap**: `GameUI.Refresh()` currently only fires from
  `Game.Changed` and only touches dynamic numbers; static labels (button
  captions, generator names/flavors) are set once in `Build*()` and never
  revisited. `Refresh()` becomes the single path that re-renders *both*
  dynamic numbers and all localized labels, every time it runs. `GameUI`
  subscribes to `Game.Changed` **and** the new `Localization.Changed`, both
  driving the same `Refresh()`. Previously-local text references (the
  `RUN TEST` label, the toggle button's label) become stored fields so
  `Refresh()` can reach them, same as `bugsText`/`rateText` already are.
- No locale-aware number formatting — `Economy.Format` output (`"1.23 K"`)
  stays identical in both languages. Real localized number formatting (decimal
  comma, etc.) is out of scope: no player-facing benefit here, real complexity
  to add.

## Testing

`Assets/Tests/Editor/LocalizationTests.cs`: a completeness guard — iterate
every key constant used anywhere in the game and assert both the English and
Portuguese dictionaries have a non-empty entry for each. Turns "forgot to
translate a string" into a red test instead of a silent blank label.

## Out of scope

- Locale-aware number formatting.
- Any language beyond English / Portuguese-BR.
- Retrofitting future milestones' strings (auto-clicker reveal, upgrades,
  prestige, achievements) — those add their own keys as they're built, using
  this same system.
