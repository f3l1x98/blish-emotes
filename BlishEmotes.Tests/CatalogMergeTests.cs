using felix.BlishEmotes.Services.Catalog;
using felix.BlishEmotes.Services.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BlishEmotes.Tests
{
    /// <summary>
    /// The catalog merge into user-persisted categories: the one-time V2 migration (stamp by en
    /// name match — "if you touched the name, it's yours" — then place emotes that are new to this
    /// user), seeding of an empty state, and the update merge (place new emotes into every linked
    /// category matching their tags, auto-create untombstoned categories the user doesn't have,
    /// prune nothing, idempotent).
    /// </summary>
    public static class CatalogMergeTests
    {
        private static readonly CultureInfo Locale = new CultureInfo("en");

        public static void Run()
        {
            Console.WriteLine("CatalogMerge");

            TestRunner.Run("1.3.8 upgrader migrates: name matches stamped, touched names stay user-owned, new content is placed", () =>
            {
                var catalog = RealCatalog();
                // A customized 1.3.8 state: Favourites, renamed "Greetings!" (was Greeting),
                // four untouched defaults, "Dance" deleted outright, plus hand-created categories.
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory>
                    {
                        Cat("Favourites", favourite: true, emoteIds: new[] { "wave" }),
                        Cat("Greetings!", emoteIds: new[] { "wave" }), // renamed -> user-owned
                        Cat("Reaction", emoteIds: new[] { "cry", "facepalm" }),
                        Cat("Fun"),
                        Cat("Pose"),
                        Cat("Miscellaneous"),
                        Cat("My Emotes"),   // hand-created -> user-owned
                        Cat("Dance Moves"), // hand-created, catalog name inside -> still user-owned
                    },
                };

                var result = CatalogMerge.Apply(document, catalog, null, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Migrated, result.Outcome);
                TestRunner.Equal(catalog.Manifest.Version, document.MergedCatalogVersion);
                // Untouched defaults are linked to their catalog category.
                TestRunner.Equal("reaction", ByName(document, "Reaction").CatalogId);
                TestRunner.Equal("fun", ByName(document, "Fun").CatalogId);
                TestRunner.Equal("pose", ByName(document, "Pose").CatalogId);
                TestRunner.Equal("miscellaneous", ByName(document, "Miscellaneous").CatalogId);
                // Renamed, hand-created and Favourites categories stay purely user-owned.
                TestRunner.True(ByName(document, "Greetings!").CatalogId == null);
                TestRunner.True(ByName(document, "My Emotes").CatalogId == null);
                TestRunner.True(ByName(document, "Dance Moves").CatalogId == null);
                TestRunner.True(ByName(document, "Favourites").CatalogId == null);
                // Renamed/deleted defaults return carrying only the new content tagged with them.
                TestRunner.Equal(10, document.Categories.Count);
                var greeting = ByName(document, "Greeting");
                TestRunner.Equal("greeting", greeting.CatalogId);
                TestRunner.True(greeting.EmoteIds.Contains("beckon"));
                TestRunner.True(greeting.EmoteIds.Contains("bow"));
                TestRunner.True(greeting.EmoteIds.Contains("salute"));
                TestRunner.False(greeting.EmoteIds.Contains("wave"), "already-known emote is not re-placed");
                var dance = ByName(document, "Dance");
                TestRunner.Equal("dance", dance.CatalogId);
                TestRunner.True(dance.EmoteIds.Count > 0);
                TestRunner.True(ByName(document, "Favourites").EmoteIds.SequenceEqual(new[] { "wave" }));
                TestRunner.True(ByName(document, "Greetings!").EmoteIds.SequenceEqual(new[] { "wave" }));
                // Outcome reports created categories and placed emotes.
                TestRunner.Equal(2, result.CreatedCategoryNames.Count);
                TestRunner.True(result.CreatedCategoryNames.Contains("Greeting"));
                TestRunner.True(result.CreatedCategoryNames.Contains("Dance"));
                TestRunner.Equal(61, result.PlacedEmoteCount);
            });

            TestRunner.Run("migration matches on the en name, not a localized one", () =>
            {
                var catalog = BuildCatalog(1, categoryDefs: new[] { CatDef("greeting", en: "Greeting", de: "Gruss") },
                    emotes: new[] { EmoteDef("wave", "greeting") });
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory> { Cat("Gruss") }, // matches only the de name
                };

                var result = CatalogMerge.Apply(document, catalog, null, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Migrated, result.Outcome);
                TestRunner.True(ByName(document, "Gruss").CatalogId == null);
            });

            TestRunner.Run("migration is idempotent", () =>
            {
                var catalog = RealCatalog();
                var document = UpgradedDefaultsDocument();
                CatalogMerge.Apply(document, catalog, null, Locale);

                var second = CatalogMerge.Apply(document, catalog, null, Locale);
                TestRunner.Equal(CatalogMergeOutcome.Noop, second.Outcome);
            });

            TestRunner.Run("renamed default + new tagged emote auto-creates linked category with only the new emotes", () =>
            {
                var catalog = BuildCatalog(1,
                    categoryDefs: new[] { CatDef("greeting"), CatDef("fun") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("cheer", "fun"), EmoteDef("hail", "greeting") });
                // "Greetings!" is a renamed default; "Fun" is untouched.
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory>
                    {
                        Cat("Favourites", favourite: true),
                        Cat("Greetings!", emoteIds: new[] { "wave" }),
                        Cat("Fun", emoteIds: new[] { "cheer" }),
                    },
                };

                var result = CatalogMerge.Apply(document, catalog, null, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Migrated, result.Outcome);
                TestRunner.Equal("fun", ByName(document, "Fun").CatalogId);
                TestRunner.True(ByName(document, "Greetings!").CatalogId == null);
                // The renamed default's catalog category returns carrying only the new emote.
                var greeting = ByName(document, "Greeting");
                TestRunner.Equal("greeting", greeting.CatalogId);
                TestRunner.True(greeting.EmoteIds.SequenceEqual(new[] { "hail" }));
                // Existing known emotes stay where the user put them.
                TestRunner.True(ByName(document, "Greetings!").EmoteIds.SequenceEqual(new[] { "wave" }));
                TestRunner.True(ByName(document, "Fun").EmoteIds.SequenceEqual(new[] { "cheer" }));
                TestRunner.Equal(1, result.CreatedCategoryNames.Count);
                TestRunner.True(result.CreatedCategoryNames.Contains("Greeting"));
                TestRunner.Equal(1, result.PlacedEmoteCount);
            });

            TestRunner.Run("deleted default with no new tagged content stays absent", () =>
            {
                var catalog = BuildCatalog(1,
                    categoryDefs: new[] { CatDef("fun"), CatDef("dance") },
                    emotes: new[] { EmoteDef("cheer", "fun"), EmoteDef("boogie", "dance") });
                // The user deleted the Dance default and moved its emote elsewhere.
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory>
                    {
                        Cat("Favourites", favourite: true),
                        Cat("Fun", emoteIds: new[] { "cheer" }),
                        Cat("My Emotes", emoteIds: new[] { "boogie" }),
                    },
                };

                var result = CatalogMerge.Apply(document, catalog, null, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Migrated, result.Outcome);
                TestRunner.True(document.Categories.All(c => c.CatalogId != "dance"), "deleted default must not be resurrected without new content");
                TestRunner.Empty(result.CreatedCategoryNames);
                TestRunner.Equal(0, result.PlacedEmoteCount);
                // Moved emote stays exactly where the user put it.
                TestRunner.True(ByName(document, "My Emotes").EmoteIds.SequenceEqual(new[] { "boogie" }));
            });

            TestRunner.Run("favourites-only emote still lands in its tagged category", () =>
            {
                var catalog = BuildCatalog(1,
                    categoryDefs: new[] { CatDef("greeting"), CatDef("fun") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("beckon", "greeting") });
                // wave lives only in Favourites; it must still be placed in Greeting. An empty
                // non-Favourites category keeps us on the migration path (not seeding).
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory>
                    {
                        Cat("Favourites", favourite: true, emoteIds: new[] { "wave" }),
                        Cat("Fun"),
                    },
                };

                var result = CatalogMerge.Apply(document, catalog, null, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Migrated, result.Outcome);
                var greeting = ByName(document, "Greeting");
                TestRunner.Equal("greeting", greeting.CatalogId);
                TestRunner.True(greeting.EmoteIds.Contains("wave"));
                TestRunner.True(greeting.EmoteIds.Contains("beckon"));
                TestRunner.Equal(2, result.PlacedEmoteCount);
            });

            TestRunner.Run("removed emote is re-placed into its tagged category", () =>
            {
                var catalog = BuildCatalog(1,
                    categoryDefs: new[] { CatDef("greeting") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("beckon", "greeting") });
                // beckon was removed from all non-Favourites categories; it is re-placed.
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory>
                    {
                        Cat("Favourites", favourite: true, emoteIds: new[] { "beckon" }),
                        Cat("Greeting", emoteIds: new[] { "wave" }),
                    },
                };

                var result = CatalogMerge.Apply(document, catalog, null, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Migrated, result.Outcome);
                TestRunner.True(ByName(document, "Greeting").EmoteIds.SequenceEqual(new[] { "wave", "beckon" }));
                TestRunner.Equal(1, result.PlacedEmoteCount);
            });

            TestRunner.Run("migration ignores missing, stale and garbage pending-merge records", () =>
            {
                var catalog = BuildCatalog(1,
                    categoryDefs: new[] { CatDef("greeting") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("beckon", "greeting") });
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory>
                    {
                        Cat("Favourites", favourite: true, emoteIds: new[] { "wave" }),
                        Cat("Greeting", emoteIds: new[] { "wave" }),
                    },
                };

                var withNull = CatalogMerge.Apply(Clone(document), catalog, null, Locale);
                var withGarbage = CatalogMerge.Apply(Clone(document), catalog, new PendingCatalogUpdate
                {
                    CatalogVersion = 99,
                    NewEmoteIds = new List<string> { "garbage" },
                    NewCategoryIds = new List<string> { "garbage" },
                }, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Migrated, withNull.Outcome);
                TestRunner.Equal(CatalogMergeOutcome.Migrated, withGarbage.Outcome);
                TestRunner.Equal(1, withNull.PlacedEmoteCount);
                TestRunner.Equal(1, withGarbage.PlacedEmoteCount);
            });

            TestRunner.Run("empty state (only Favourites) is seeded with every catalog default", () =>
            {
                var catalog = RealCatalog();
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory> { Cat("Favourites", favourite: true, emoteIds: new[] { "wave" }) },
                };

                var result = CatalogMerge.Apply(document, catalog, null, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Seeded, result.Outcome);
                // Six linked categories appended at the end, in catalog order, with en names.
                TestRunner.Equal(7, document.Categories.Count);
                var created = document.Categories.Skip(1).ToList();
                TestRunner.Equal(6, created.Count);
                TestRunner.Equal("greeting", created[0].CatalogId);
                TestRunner.Equal("Greeting", created[0].Name);
                TestRunner.Equal("miscellaneous", created[5].CatalogId);
                // Every emote placed per its tags, exactly like the 1.x defaults.
                TestRunner.Equal(4, created[0].EmoteIds.Count);
                TestRunner.Equal(16, created[1].EmoteIds.Count);
                TestRunner.Equal(6, created[2].EmoteIds.Count);
                TestRunner.Equal(21, created[3].EmoteIds.Count);
                TestRunner.Equal(8, created[4].EmoteIds.Count);
                TestRunner.Equal(9, created[5].EmoteIds.Count);
                TestRunner.True(created[1].EmoteIds.Contains("facepalm"));
                // Favourites is never merged.
                TestRunner.True(ByName(document, "Favourites").EmoteIds.SequenceEqual(new[] { "wave" }));
                TestRunner.Equal(64, result.PlacedEmoteCount);
            });

            TestRunner.Run("new emote lands in every linked category matching its placement tags", () =>
            {
                var catalog = BuildCatalog(2,
                    categoryDefs: new[] { CatDef("greeting"), CatDef("fun") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("cheer", "fun"), EmoteDef("waltz", "greeting", "fun") });
                var document = MigratedState(catalog, 1, Cat("Hello", catalogId: "greeting"), Cat("Joy", catalogId: "fun"));
                var pending = Pending(2, new[] { "waltz" });

                var result = CatalogMerge.Apply(document, catalog, pending, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Updated, result.Outcome);
                TestRunner.True(ByName(document, "Hello").EmoteIds.Contains("waltz"));
                TestRunner.True(ByName(document, "Joy").EmoteIds.Contains("waltz"));
                TestRunner.Equal(2, result.PlacedEmoteCount);
            });

            TestRunner.Run("no untouched-vs-modified distinction: an emptied linked category still receives", () =>
            {
                var catalog = BuildCatalog(2,
                    categoryDefs: new[] { CatDef("greeting") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("waltz", "greeting") });
                // The user removed every emote from their Greeting category.
                var document = MigratedState(catalog, 1, Cat("Greeting", catalogId: "greeting"));
                var pending = Pending(2, new[] { "waltz" });

                CatalogMerge.Apply(document, catalog, pending, Locale);

                TestRunner.True(ByName(document, "Greeting").EmoteIds.SequenceEqual(new[] { "waltz" }));
            });

            TestRunner.Run("tombstoned category: never resurrected, its emotes land uncategorized", () =>
            {
                var catalog = BuildCatalog(2,
                    categoryDefs: new[] { CatDef("dance") },
                    emotes: new[] { EmoteDef("boogie", "dance"), EmoteDef("waltz", "dance") });
                var document = MigratedState(catalog, 1);
                document.DeletedCatalogCategoryIds.Add("dance");
                var pending = Pending(2, new[] { "waltz" }, new[] { "dance" });

                var result = CatalogMerge.Apply(document, catalog, pending, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Updated, result.Outcome);
                TestRunner.Empty(result.CreatedCategoryNames);
                TestRunner.Equal(0, result.PlacedEmoteCount);
                TestRunner.Equal(1, document.Categories.Count); // only Favourites
                foreach (var category in document.Categories)
                {
                    TestRunner.False(category.EmoteIds.Contains("waltz"), $"{category.Name} must not contain waltz");
                }
            });

            TestRunner.Run("new catalog category is auto-created at the end of the user's order", () =>
            {
                var catalog = BuildCatalog(2,
                    categoryDefs: new[] { CatDef("greeting"), CatDef("ceremony") },
                    emotes: new[] { EmoteDef("wave", "greeting") });
                var document = MigratedState(catalog, 1,
                    Cat("Greeting", catalogId: "greeting"), Cat("Zoo", emoteIds: "rank")); // hand-created, last
                var pending = Pending(2, new string[0], new[] { "ceremony" });

                var result = CatalogMerge.Apply(document, catalog, pending, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Updated, result.Outcome);
                var created = document.Categories.Last();
                TestRunner.Equal("ceremony", created.CatalogId);
                TestRunner.Equal("Ceremony", created.Name);
                TestRunner.Equal(4, document.Categories.Count);
                TestRunner.True(ReferenceEquals(created, document.Categories[document.Categories.Count - 1]), "appended at the end");
                TestRunner.True(ByName(document, "Zoo").EmoteIds.Contains("rank"), "hand-created category untouched");
            });

            TestRunner.Run("deleted pre-2.0 default returns only when new content is tagged with it", () =>
            {
                // "Dance" was deleted in 1.x: no tombstone exists (the list is new in V2), so it is
                // simply absent — and stays absent until an update actually needs the category.
                var catalogV1 = BuildCatalog(1, categoryDefs: new[] { CatDef("dance") }, emotes: new[] { EmoteDef("boogie", "dance") });
                var document = MigratedState(catalogV1, 1); // migrated without a Dance category

                // An update that changes nothing about dance must not resurrect it.
                var unrelatedCatalog = BuildCatalog(2, categoryDefs: new[] { CatDef("dance"), CatDef("fun") },
                    emotes: new[] { EmoteDef("boogie", "dance"), EmoteDef("cheer", "fun") });
                var documentCopy = Clone(document);
                var unrelated = CatalogMerge.Apply(documentCopy, unrelatedCatalog, Pending(2, new[] { "cheer" }, new[] { "fun" }), Locale);
                TestRunner.Equal(CatalogMergeOutcome.Updated, unrelated.Outcome);
                TestRunner.True(documentCopy.Categories.All(c => c.CatalogId != "dance"), "untagged update must not resurrect Dance");

                // An update with a new emote tagged dance does create it (and places the emote).
                var danceCatalog = BuildCatalog(2, categoryDefs: new[] { CatDef("dance"), CatDef("fun") },
                    emotes: new[] { EmoteDef("boogie", "dance"), EmoteDef("cheer", "fun"), EmoteDef("waltz", "dance") });
                var result = CatalogMerge.Apply(document, danceCatalog, Pending(2, new[] { "waltz" }), Locale);
                TestRunner.Equal(CatalogMergeOutcome.Updated, result.Outcome);
                var dance = document.Categories.FirstOrDefault(c => c.CatalogId == "dance");
                TestRunner.NotNull(dance);
                TestRunner.Equal("Dance", dance.Name);
                TestRunner.True(dance.EmoteIds.Contains("waltz"));
            });

            TestRunner.Run("unique-name conflict skips auto-create; emotes land uncategorized", () =>
            {
                var catalog = BuildCatalog(2,
                    categoryDefs: new[] { CatDef("ceremony") },
                    emotes: new[] { EmoteDef("wave", "ceremony"), EmoteDef("waltz", "ceremony") });
                // The user hand-created a category that already owns the catalog's name.
                var document = MigratedState(catalog, 1, Cat("Ceremony"));
                var pending = Pending(2, new[] { "waltz" }, new[] { "ceremony" });

                var result = CatalogMerge.Apply(document, catalog, pending, Locale);

                TestRunner.Equal(CatalogMergeOutcome.Updated, result.Outcome);
                TestRunner.Empty(result.CreatedCategoryNames);
                TestRunner.Equal(1, result.SkippedNameConflicts.Count);
                // The user's category is never stamped or filled by the merge.
                TestRunner.True(ByName(document, "Ceremony").CatalogId == null);
                TestRunner.Empty(ByName(document, "Ceremony").EmoteIds);
                TestRunner.Equal(0, result.PlacedEmoteCount);
            });

            TestRunner.Run("update never re-adds an emote the user removed and never prunes", () =>
            {
                var catalog = BuildCatalog(2,
                    categoryDefs: new[] { CatDef("greeting") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("beckon", "greeting"), EmoteDef("waltz", "greeting") });
                // The user removed "beckon" from Greeting; "ghost" is a stale id from an emote that
                // no longer resolves (removal is a non-event by policy — nothing cleans it).
                var document = new UserCategoriesDocument
                {
                    Categories = new List<PersistedCategory>
                    {
                        Cat("Favourites", favourite: true, emoteIds: new[] { "beckon" }),
                        Cat("Greeting", catalogId: "greeting", emoteIds: new[] { "wave", "ghost" }),
                    },
                    MergedCatalogVersion = 1,
                };
                var pending = Pending(2, new[] { "waltz" });

                var result = CatalogMerge.Apply(document, catalog, pending, Locale);

                var greeting = ByName(document, "Greeting");
                TestRunner.True(greeting.EmoteIds.Contains("waltz"));
                TestRunner.False(greeting.EmoteIds.Contains("beckon"), "user-removed emote must stay removed");
                TestRunner.True(greeting.EmoteIds.Contains("ghost"), "stale ids are never pruned");
                TestRunner.True(ByName(document, "Favourites").EmoteIds.SequenceEqual(new[] { "beckon" }), "Favourites untouched");
            });

            TestRunner.Run("applying the same update twice is a noop (idempotent)", () =>
            {
                var catalog = BuildCatalog(2,
                    categoryDefs: new[] { CatDef("greeting") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("waltz", "greeting") });
                var document = MigratedState(catalog, 1, Cat("Greeting", catalogId: "greeting", emoteIds: new[] { "wave" }));
                var pending = Pending(2, new[] { "waltz" });

                var first = CatalogMerge.Apply(document, catalog, pending, Locale);
                TestRunner.Equal(CatalogMergeOutcome.Updated, first.Outcome);
                var second = CatalogMerge.Apply(document, catalog, pending, Locale);
                TestRunner.Equal(CatalogMergeOutcome.Noop, second.Outcome);
                TestRunner.True(ByName(document, "Greeting").EmoteIds.SequenceEqual(new[] { "wave", "waltz" }));
            });

            TestRunner.Run("stale and orphaned pending updates are discarded", () =>
            {
                var catalogV1 = BuildCatalog(1, categoryDefs: new[] { CatDef("greeting") }, emotes: new[] { EmoteDef("wave", "greeting") });
                var document = MigratedState(catalogV1, 1, Cat("Greeting", catalogId: "greeting", emoteIds: new[] { "wave" }));

                // Same version as already merged: already applied.
                var stale = CatalogMerge.Apply(document, catalogV1, Pending(1, new[] { "wave" }), Locale);
                TestRunner.Equal(CatalogMergeOutcome.Noop, stale.Outcome);

                // Orphan: describes a catalog that never landed (its cache swap failed) — the
                // loaded catalog is still v1, so the v2 record must not apply.
                var orphan = CatalogMerge.Apply(document, catalogV1, Pending(2, new[] { "waltz" }), Locale);
                TestRunner.Equal(CatalogMergeOutcome.Noop, orphan.Outcome);
                TestRunner.True(ByName(document, "Greeting").EmoteIds.SequenceEqual(new[] { "wave" }));

                // No pending at all: noop.
                TestRunner.Equal(CatalogMergeOutcome.Noop, CatalogMerge.Apply(document, catalogV1, null, Locale).Outcome);
            });

            TestRunner.Run("a catalog update applies to a state migrated against an older version", () =>
            {
                var catalogV1 = BuildCatalog(1, categoryDefs: new[] { CatDef("greeting") }, emotes: new[] { EmoteDef("wave", "greeting") });
                var catalogV2 = BuildCatalog(2, categoryDefs: new[] { CatDef("greeting") },
                    emotes: new[] { EmoteDef("wave", "greeting"), EmoteDef("waltz", "greeting") });
                var document = MigratedState(catalogV1, 1, Cat("Greeting", catalogId: "greeting", emoteIds: new[] { "wave" }));

                var result = CatalogMerge.Apply(document, catalogV2, Pending(2, new[] { "waltz" }), Locale);

                TestRunner.Equal(CatalogMergeOutcome.Updated, result.Outcome);
                TestRunner.Equal(2, document.MergedCatalogVersion);
                TestRunner.True(ByName(document, "Greeting").EmoteIds.Contains("waltz"));
            });
        }

        // ---- fixtures -------------------------------------------------------------

        private static CatalogData RealCatalog()
        {
            string catalogDir = CatalogParsingTests.RepoPath("catalog");
            return CatalogData.FromJson(
                File.ReadAllText(Path.Combine(catalogDir, "manifest.json"), Encoding.UTF8),
                File.ReadAllText(Path.Combine(catalogDir, "emotes.json"), Encoding.UTF8),
                File.ReadAllText(Path.Combine(catalogDir, "categories.json"), Encoding.UTF8));
        }

        /// <summary>A minimal valid-enough catalog for the merge (it touches manifest version, emotes and categories only).</summary>
        private static CatalogData BuildCatalog(long version, CatalogCategory[] categoryDefs, CatalogEmote[] emotes)
        {
            return new CatalogData
            {
                Manifest = new CatalogManifest { SchemaVersion = 1, Version = version, Files = new Dictionary<string, string>() },
                Categories = categoryDefs.ToList(),
                Emotes = emotes.ToList(),
            };
        }

        private static CatalogCategory CatDef(string id, string en = null, string de = null)
        {
            var names = new Dictionary<string, string> { { "en", en ?? CatalogNames.TitleCaseId(id) } };
            if (de != null)
            {
                names["de"] = de;
            }
            return new CatalogCategory { Id = id, Names = names };
        }

        private static CatalogEmote EmoteDef(string id, params string[] categories)
        {
            return new CatalogEmote
            {
                Id = id,
                Command = "/" + id,
                Names = new Dictionary<string, string> { { "en", CatalogNames.TitleCaseId(id) } },
                Categories = categories.ToList(),
            };
        }

        private static PersistedCategory Cat(string name, string catalogId = null, bool favourite = false, params string[] emoteIds)
        {
            return new PersistedCategory
            {
                Id = Guid.NewGuid(),
                Name = name,
                CatalogId = catalogId,
                IsFavourite = favourite,
                EmoteIds = emoteIds.ToList(),
            };
        }

        private static PendingCatalogUpdate Pending(long version, string[] newEmotes, string[] newCategories = null)
        {
            return new PendingCatalogUpdate
            {
                CatalogVersion = version,
                NewEmoteIds = (newEmotes ?? new string[0]).ToList(),
                NewCategoryIds = (newCategories ?? new string[0]).ToList(),
            };
        }

        /// <summary>A document that has already been migrated (or is a fresh 2.0 default state): Favourites first.</summary>
        private static UserCategoriesDocument MigratedState(CatalogData catalog, long mergedVersion, params PersistedCategory[] extra)
        {
            var document = new UserCategoriesDocument
            {
                Categories = new List<PersistedCategory> { Cat("Favourites", favourite: true) },
                MergedCatalogVersion = mergedVersion,
            };
            document.Categories.AddRange(extra);
            return document;
        }

        /// <summary>The uncustomized 1.3.8 default state, as SetupDefaultCategories writes it.</summary>
        private static UserCategoriesDocument UpgradedDefaultsDocument()
        {
            return new UserCategoriesDocument
            {
                Categories = new List<PersistedCategory>
                {
                    Cat("Favourites", favourite: true),
                    Cat("Greeting", emoteIds: new[] { "beckon", "bow", "salute", "wave" }),
                    Cat("Reaction"),
                    Cat("Fun"),
                    Cat("Pose"),
                    Cat("Dance"),
                    Cat("Miscellaneous"),
                },
            };
        }

        private static UserCategoriesDocument Clone(UserCategoriesDocument document)
        {
            return new UserCategoriesDocument
            {
                Categories = document.Categories.Select(Clone).ToList(),
                DeletedCatalogCategoryIds = new List<string>(document.DeletedCatalogCategoryIds),
                MergedCatalogVersion = document.MergedCatalogVersion,
            };
        }

        private static PersistedCategory Clone(PersistedCategory category)
        {
            return new PersistedCategory
            {
                Id = category.Id,
                Name = category.Name,
                CatalogId = category.CatalogId,
                IsFavourite = category.IsFavourite,
                TextureFileName = category.TextureFileName,
                EmoteIds = new List<string>(category.EmoteIds),
            };
        }

        private static PersistedCategory ByName(UserCategoriesDocument document, string name)
        {
            return document.Categories.First(category => category.Name == name);
        }
    }
}
