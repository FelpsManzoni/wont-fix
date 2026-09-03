# Unlockable Generators & Upgrades — Design

Status: approved
Date: 2026-09-02

## Context

Milestone 1 shipped all 12 generators visible from the start. This milestone (1+2 in
Felipe's original numbering — he calls the generators "auto-clickers") retrofits them
to be hidden until unlocked, Cookie Clicker style, and layers an Upgrades system on
top: one-off boosts to the click value or a specific generator's output, unlocked the
same way. Explicitly out of scope: Prestige, Achievements, and P0 events — P0 events
especially, since Felipe has a specific idea for that one and will brainstorm it
separately.

Constraint carried through every prior milestone: no Unity Editor GUI authoring.
Everything here is a plain text file edit, verified via NUnit EditMode tests
(headless batchmode or the in-Editor Test Runner) plus manual Play Mode checks for
anything UI-only.

## Data model

`GameData.cs` gains an `Upgrade` struct and a static `Upgrades` array, alongside the
existing `Generator`/`Generators` — same kind of content, same file:

```csharp
public enum UpgradeEffect { Click, Generator }

public struct Upgrade
{
    public string id;
    public string generatorId;   // gates unlock (own ≥1 of this); also the boost
                                  // target when effect == Generator
    public double cost;
    public UpgradeEffect effect;
    public double multiplier;
}
```

`generatorId` deliberately does double duty: it's both "the generator that must be
owned to reveal this upgrade" and, for `Generator`-effect upgrades, "the generator
this upgrade boosts." All four upgrades in scope boost the same generator that gates
their own unlock, so one field covers both without a special case.
`Reproducible Steps` (a `Click`-effect upgrade) still sets `generatorId =
"junior_tester"` purely for unlock-gating — it has nothing to boost a generator.

`GameData.cs` gets one small helper, `IndexOfGenerator(string id)` — linear search
over 12 entries, not worth a dictionary at this size.

### Persistence

Generator unlocking needs **zero new state**: `generator[i]` is visible when
`i == 0 || Owned[i-1] >= 1` — derived from data that already exists and only ever
increases, so "once revealed, stays revealed" falls out for free.

Upgrade purchases need new state, since they're one-time (unlike owning multiples of
a generator). `SaveData` gains `public bool[] upgradesPurchased;`, saved/loaded
exactly like `owned` already is.

## Logic architecture

Unlock checks and multiplier math are genuine branching logic, not display glue, and
none of it needs `MonoBehaviour`. Rather than bury it in `Game.cs` (which this
codebase's convention leaves untested, verified only via manual Play Mode), it moves
into a new pure file mirroring `Economy.cs`'s role:

**`Progression.cs`** (new, static functions, `int[]`/`bool[]` in, values out, zero
`UnityEngine` dependency):

```csharp
public static class Progression
{
    public static bool IsGeneratorUnlocked(int index, int[] owned) =>
        index == 0 || owned[index - 1] >= 1;

    public static bool IsUpgradeUnlocked(Upgrade upgrade, int[] owned) =>
        owned[GameData.IndexOfGenerator(upgrade.generatorId)] >= 1;

    public static double ClickMultiplier(bool[] purchased) =>
        Product(purchased, UpgradeEffect.Click, generatorId: null);

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
```

`Game.cs` becomes thin orchestration calling these: `Click()` becomes
`Add(Progression.ClickMultiplier(UpgradesPurchased))`; `RecalculateRate()`
multiplies each generator's contribution by
`Progression.GeneratorMultiplier(gen.id, UpgradesPurchased)`; a new `BuyUpgrade(int
i)` mirrors `Buy(int i)` (check unlocked + affordable + not-already-purchased,
deduct cost, flip the flag, recalculate, save, fire `Changed`).

## UI restructuring

**Row visibility.** `BuildShop` still creates every row upfront — generators and
upgrades alike — exactly as it does today. The change is that each row starts
hidden, and `Refresh()` toggles `SetActive(isUnlocked)` on it every call. Unity's
`VerticalLayoutGroup`/`ContentSizeFitter` natively skip inactive children in layout,
so a hidden row takes no space — no dynamic instantiate/destroy, no rebuilding the
list.

**Row ordering.** Upgrade rows are interleaved right after the generator that gates
them: generator 0 (Junior Tester) → its upgrades (Reproducible Steps, Second
Monitor) → generator 1 → … → generator 3 (CI Pipeline) → its upgrade (Flaky Test
Quarantine) → generator 4 onward with no upgrades attached. One scrolling list, no
second `ScrollRect`.

**Upgrade row visuals.** Same row shape as a generator row (info text left, button
right), but a one-time purchase instead of a repeatable buy: once
`UpgradesPurchased[i]` is true, the button becomes non-interactable and its label
swaps from the cost to a localized "OWNED" string. `GameUI` gets a second small set
of tracked refs (`upgradeInfoTexts`, `upgradeButtons`), and `Refresh()` gets a second
loop over `GameData.Upgrades` mirroring the generator loop.

## Content — costs and thresholds

All four unlock at owning **1** of the gating generator (matching genre convention —
the upgrade appears the moment you buy your first one). Cost is 10× the target
generator's base cost — proportionate to the existing curve, one column to retune
later if needed.

| Upgrade | Gates on / boosts | Cost | Effect |
|---|---|---|---|
| Reproducible Steps | `junior_tester` | 150 | ×2 click |
| Second Monitor | `junior_tester` | 150 | ×2 Junior Tester output |
| Explicit Waits | `selenium_script` | 11,000 | ×2 Selenium Script output |
| Flaky Test Quarantine | `ci_pipeline` | 120,000 | ×2 CI Pipeline output |

**Localization**: new keys `upg.<id>.name` / `upg.<id>.flavor` for all 4 upgrades in
both languages (flavor text states the mechanical effect directly, e.g. "Doubles
Junior Tester output," so no separate effect-description UI element is needed), plus
one `ui.owned` key for the purchased-state label.

## Testing

- `ProgressionTests.cs` (new): the unlock chain (`IsGeneratorUnlocked` for index 0, a
  locked index, an unlocked index) and the multiplier math (zero purchases → 1×, one
  matching purchase → the multiplier, a purchase for a *different* generator →
  unaffected, a `Click`-effect purchase not leaking into `GeneratorMultiplier`).
- `LocalizationTests.cs` extends its existing completeness loop to also cover the new
  `upg.*` keys, same pattern as the generator keys.
- `GameUI`/`Game` stay manually verified via Play Mode, per established convention.

## Out of scope

- Prestige, Achievements, P0 events (each its own future milestone; P0 events awaits
  Felipe's own brainstorm).
- More than 4 upgrades, or upgrades covering every generator.
- Any unlock condition other than "own ≥1 of a specific generator" (no lifetime-bugs
  thresholds, no multi-condition gating).
