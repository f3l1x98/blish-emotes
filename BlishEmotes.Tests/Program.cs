using System;

namespace BlishEmotes.Tests
{
    public static class Program
    {
        public static int Main()
        {
            Console.WriteLine("BlishEmotes test suite\n");
            CatalogNamesTests.Run();
            CatalogPathsTests.Run();
            CatalogHashTests.Run();
            CatalogValidatorTests.Run();
            CatalogParsingTests.Run();
            CatalogCacheTests.Run();
            CatalogClientTests.Run();
            return TestRunner.Finish();
        }
    }
}
