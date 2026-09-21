using System;
using felix.BlishEmotes.Services.Catalog;
using System.Collections.Generic;

namespace BlishEmotes.Tests
{
    public static class CatalogValidatorTests
    {
        public static void Run()
        {
            Console.WriteLine("CatalogValidator");

            TestRunner.Run("a minimal valid catalog passes", () =>
            {
                var catalog = ValidCatalog();
                TestRunner.Empty(CatalogValidator.Validate(catalog));
            });

            TestRunner.Run("null catalog fails", () =>
            {
                var errors = CatalogValidator.Validate(null);
                TestRunner.True(errors.Count == 1);
            });

            TestRunner.Run("too-new schemaVersion aborts", () =>
            {
                var catalog = ValidCatalog();
                catalog.Manifest.SchemaVersion = CatalogValidator.SupportedSchemaVersion + 1;
                var errors = CatalogValidator.Validate(catalog);
                TestRunner.True(errors.Count == 1 && errors[0].Contains("schemaVersion"));
            });

            TestRunner.Run("empty manifest files map fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Manifest.Files = new Dictionary<string, string>();
                TestRunner.True(CatalogValidator.Validate(catalog).Count > 0);
            });

            TestRunner.Run("unsafe manifest file path fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Manifest.Files["../evil.json"] = "00";
                TestRunner.True(CatalogValidator.Validate(catalog).Count > 0);
            });

            TestRunner.Run("empty emotes list fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Emotes = new List<CatalogEmote>();
                TestRunner.True(CatalogValidator.Validate(catalog).Count > 0);
            });

            TestRunner.Run("duplicate emote ids fail", () =>
            {
                var catalog = ValidCatalog();
                catalog.Emotes.Add(Clone(catalog.Emotes[0]));
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("duplicate id")));
            });

            TestRunner.Run("command without slash fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Emotes[0].Command = "barbecue";
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("does not start with '/'")));
            });

            TestRunner.Run("alternative command without slash fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Emotes[0].AlternativeCommands = new List<string> { "bbq" };
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("alternative command 'bbq'")));
            });

            TestRunner.Run("alternative command duplicating primary fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Emotes[0].AlternativeCommands = new List<string> { "/barbecue" };
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("duplicates primary")));
            });

            TestRunner.Run("missing en name fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Emotes[0].Names = new Dictionary<string, string> { { "de", "Grillen" } };
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("no 'en' name")));
            });

            TestRunner.Run("unknown category reference fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Emotes[0].Categories = new List<string> { "nonexistent" };
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("unknown category")));
            });

            TestRunner.Run("emote in no category fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Emotes[0].Categories = new List<string>();
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("in no category")));
            });

            TestRunner.Run("missing texture entry in manifest fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Manifest.Files.Remove("textures/barbecue.png");
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("missing texture")));
            });

            TestRunner.Run("duplicate category ids fail", () =>
            {
                var catalog = ValidCatalog();
                catalog.Categories.Add(Clone(catalog.Categories[0]));
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("duplicate category id")));
            });

            TestRunner.Run("category without en name fails", () =>
            {
                var catalog = ValidCatalog();
                catalog.Categories[0].Names = new Dictionary<string, string> { { "de", "Gruß" } };
                TestRunner.True(CatalogValidator.Validate(catalog).Exists(e => e.Contains("category greeting: no 'en' name")));
            });
        }

        private static CatalogData ValidCatalog()
        {
            return new CatalogData
            {
                Manifest = new CatalogManifest
                {
                    SchemaVersion = CatalogValidator.SupportedSchemaVersion,
                    Version = 1,
                    Files = new Dictionary<string, string>
                    {
                        { "emotes.json", "00" },
                        { "categories.json", "00" },
                        { "textures/barbecue.png", "00" },
                        { "textures/beckon.png", "00" },
                    },
                },
                Emotes = new List<CatalogEmote>
                {
                    new CatalogEmote
                    {
                        Id = "barbecue",
                        Command = "/barbecue",
                        AlternativeCommands = new List<string> { "/bbq" },
                        Names = new Dictionary<string, string> { { "en", "Barbecue" }, { "de", "Grillen" } },
                        Categories = new List<string> { "miscellaneous" },
                    },
                    new CatalogEmote
                    {
                        Id = "beckon",
                        Command = "/beckon",
                        Names = new Dictionary<string, string> { { "en", "Beckon" } },
                        Categories = new List<string> { "greeting", "miscellaneous" },
                    },
                },
                Categories = new List<CatalogCategory>
                {
                    new CatalogCategory { Id = "greeting", Names = new Dictionary<string, string> { { "en", "Greeting" } } },
                    new CatalogCategory { Id = "miscellaneous", Names = new Dictionary<string, string> { { "en", "Miscellaneous" } } },
                },
            };
        }

        private static CatalogEmote Clone(CatalogEmote emote)
        {
            return new CatalogEmote
            {
                Id = emote.Id,
                Command = emote.Command,
                AlternativeCommands = emote.AlternativeCommands != null
                    ? new List<string>(emote.AlternativeCommands)
                    : null,
                Names = emote.Names != null ? new Dictionary<string, string>(emote.Names) : null,
                Categories = emote.Categories != null ? new List<string>(emote.Categories) : null,
            };
        }

        private static CatalogCategory Clone(CatalogCategory category)
        {
            return new CatalogCategory
            {
                Id = category.Id,
                Names = category.Names != null ? new Dictionary<string, string>(category.Names) : null,
            };
        }
    }
}
