using System;
using felix.BlishEmotes.Services.Catalog;

namespace BlishEmotes.Tests
{
    public static class CatalogPathsTests
    {
        public static void Run()
        {
            Console.WriteLine("CatalogPaths");
            TestRunner.Run("normal catalog paths are safe", () =>
            {
                TestRunner.True(CatalogPaths.IsSafeRelativePath("emotes.json"));
                TestRunner.True(CatalogPaths.IsSafeRelativePath("categories.json"));
                TestRunner.True(CatalogPaths.IsSafeRelativePath("textures/barbecue.png"));
                TestRunner.True(CatalogPaths.IsSafeRelativePath("textures/sub/dir/x.png"));
            });

            TestRunner.Run("backslash variants are safe", () =>
            {
                TestRunner.True(CatalogPaths.IsSafeRelativePath("textures\\barbecue.png"));
            });

            TestRunner.Run("traversal is rejected", () =>
            {
                TestRunner.False(CatalogPaths.IsSafeRelativePath("../secrets.json"));
                TestRunner.False(CatalogPaths.IsSafeRelativePath("textures/../../secrets.json"));
                TestRunner.False(CatalogPaths.IsSafeRelativePath("textures\\..\\..\\secrets.json"));
                TestRunner.False(CatalogPaths.IsSafeRelativePath(".."));
            });

            TestRunner.Run("rooted and empty paths are rejected", () =>
            {
                TestRunner.False(CatalogPaths.IsSafeRelativePath(""));
                TestRunner.False(CatalogPaths.IsSafeRelativePath(null));
                TestRunner.False(CatalogPaths.IsSafeRelativePath("/etc/passwd"));
                TestRunner.False(CatalogPaths.IsSafeRelativePath("C:\\emotes.json"));
            });

            TestRunner.Run("degenerate forms are rejected", () =>
            {
                TestRunner.False(CatalogPaths.IsSafeRelativePath("textures//x.png"));
                TestRunner.False(CatalogPaths.IsSafeRelativePath("textures/"));
                TestRunner.False(CatalogPaths.IsSafeRelativePath("textures/x.png/"));
                TestRunner.False(CatalogPaths.IsSafeRelativePath("textures/x.png "));
            });

            TestRunner.Run("texture file path is textures/<id>.png", () =>
            {
                TestRunner.Equal("textures/barbecue.png", CatalogPaths.TextureFile("barbecue"));
            });
        }
    }
}
