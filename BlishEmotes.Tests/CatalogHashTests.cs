using System;
using System.Text;
using felix.BlishEmotes.Services.Catalog;

namespace BlishEmotes.Tests
{
    public static class CatalogHashTests
    {
        // Well-known SHA-256 digests.
        private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        public static void Run()
        {
            Console.WriteLine("CatalogHash");
            TestRunner.Run("empty bytes hash to the empty-input digest", () =>
            {
                TestRunner.Equal(EmptySha256, CatalogHash.Sha256Hex(new byte[0]));
            });

            TestRunner.Run("null bytes hash to the empty-input digest", () =>
            {
                TestRunner.Equal(EmptySha256, CatalogHash.Sha256Hex((byte[])null));
            });

            TestRunner.Run("known digest of 'abc'", () =>
            {
                TestRunner.Equal(AbcSha256, CatalogHash.Sha256Hex(Encoding.UTF8.GetBytes("abc")));
            });

            TestRunner.Run("verify matches case-insensitively", () =>
            {
                TestRunner.True(CatalogHash.Verify(Encoding.UTF8.GetBytes("abc"), AbcSha256));
                TestRunner.True(CatalogHash.Verify(Encoding.UTF8.GetBytes("abc"), AbcSha256.ToUpperInvariant()));
            });

            TestRunner.Run("verify rejects mismatched and empty expectations", () =>
            {
                TestRunner.False(CatalogHash.Verify(Encoding.UTF8.GetBytes("abd"), AbcSha256));
                TestRunner.False(CatalogHash.Verify(Encoding.UTF8.GetBytes("abc"), null));
                TestRunner.False(CatalogHash.Verify(Encoding.UTF8.GetBytes("abc"), ""));
            });
        }
    }
}
