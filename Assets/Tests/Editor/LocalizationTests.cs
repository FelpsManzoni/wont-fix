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
