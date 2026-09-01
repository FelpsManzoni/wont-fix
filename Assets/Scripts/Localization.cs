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
