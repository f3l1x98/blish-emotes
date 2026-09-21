using System;
using System.Globalization;
using felix.BlishEmotes.Services.Catalog;

namespace BlishEmotes.Tests
{
    public static class CatalogNamesTests
    {
        public static void Run()
        {
            Console.WriteLine("CatalogNames");
            TestRunner.Run("exact locale match", () =>
            {
                var names = new System.Collections.Generic.Dictionary<string, string> { { "en", "Barbecue" }, { "de", "Grillen" } };
                TestRunner.Equal("Grillen", CatalogNames.Resolve(names, new CultureInfo("de"), "barbecue"));
            });

            TestRunner.Run("missing locale falls back to en", () =>
            {
                var names = new System.Collections.Generic.Dictionary<string, string> { { "en", "Bless" }, { "fr", "Bénir" } };
                TestRunner.Equal("Bless", CatalogNames.Resolve(names, new CultureInfo("es"), "bless"));
            });

            TestRunner.Run("untranslated emote (only en present) resolves for any locale", () =>
            {
                var names = new System.Collections.Generic.Dictionary<string, string> { { "en", "Bless" } };
                TestRunner.Equal("Bless", CatalogNames.Resolve(names, new CultureInfo("de"), "bless"));
            });

            TestRunner.Run("empty en name falls through to title-cased id", () =>
            {
                var names = new System.Collections.Generic.Dictionary<string, string> { { "en", "" } };
                TestRunner.Equal("Bless", CatalogNames.Resolve(names, new CultureInfo("de"), "bless"));
            });

            TestRunner.Run("null names falls back to title-cased id", () =>
            {
                TestRunner.Equal("Thumbsdown", CatalogNames.Resolve(null, new CultureInfo("fr"), "thumbsdown"));
            });

            TestRunner.Run("unknown locale + no en falls back to title-cased id", () =>
            {
                var names = new System.Collections.Generic.Dictionary<string, string> { { "ko", "환영" } };
                TestRunner.Equal("Wave", CatalogNames.Resolve(names, new CultureInfo("de"), "wave"));
            });

            TestRunner.Run("korean locale resolves when translated", () =>
            {
                var names = new System.Collections.Generic.Dictionary<string, string> { { "en", "Wave" }, { "ko", "환영" } };
                TestRunner.Equal("환영", CatalogNames.Resolve(names, new CultureInfo("ko"), "wave"));
            });

            TestRunner.Run("null locale resolves en", () =>
            {
                var names = new System.Collections.Generic.Dictionary<string, string> { { "en", "Wave" } };
                TestRunner.Equal("Wave", CatalogNames.Resolve(names, null, "wave"));
            });

            TestRunner.Run("title-casing lowercases first and handles multi-word ids invariantly", () =>
            {
                TestRunner.Equal("Happydance", CatalogNames.TitleCaseId("happydance"));
                TestRunner.Equal("Happydance", CatalogNames.TitleCaseId("HappyDance"));
                TestRunner.Equal("", CatalogNames.TitleCaseId(null));
            });
        }
    }
}
