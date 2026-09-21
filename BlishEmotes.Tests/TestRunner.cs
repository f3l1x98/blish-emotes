using System;
using System.Collections.Generic;

namespace BlishEmotes.Tests
{
    /// <summary>Minimal xUnit-less test runner: BlishEmotes targets .NET Framework 4.7.2 and keeps
    /// its dependencies lean, so the suite is a plain console exe built and run under mono.</summary>
    public static class TestRunner
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> Failures = new List<string>();

        public static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine($"  PASS {name}");
            }
            catch (Exception e)
            {
                _failed++;
                string detail = e is TestFailure ? e.Message : $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}";
                Failures.Add($"{name}: {detail}");
                Console.WriteLine($"  FAIL {name}\n       {detail}");
            }
        }

        public static int Finish()
        {
            Console.WriteLine($"\n{_passed} passed, {_failed} failed.");
            if (_failed > 0)
            {
                Console.WriteLine("Failures:");
                foreach (var failure in Failures)
                {
                    Console.WriteLine("  - " + failure);
                }
                return 1;
            }
            return 0;
        }

        public static void Equal<T>(T expected, T actual, string context = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                Fail($"expected '{expected}' but got '{actual}'" + (context != null ? $" ({context})" : ""));
            }
        }

        public static void True(bool condition, string context = null)
        {
            if (!condition)
            {
                Fail("expected true" + (context != null ? $" ({context})" : ""));
            }
        }

        public static void False(bool condition, string context = null)
        {
            if (condition)
            {
                Fail("expected false" + (context != null ? $" ({context})" : ""));
            }
        }

        public static void NotNull(object value, string context = null)
        {
            if (value == null)
            {
                Fail("expected non-null" + (context != null ? $" ({context})" : ""));
            }
        }

        public static void Empty<T>(IReadOnlyList<T> values, string context = null)
        {
            if (values != null && values.Count > 0)
            {
                Fail($"expected empty but got {values.Count} item(s): {string.Join("; ", values)}"
                    + (context != null ? $" ({context})" : ""));
            }
        }

        public static T Throws<T>(Action action, string context = null) where T : Exception
        {
            try
            {
                action();
            }
            catch (T e)
            {
                return e;
            }
            catch (Exception other)
            {
                Fail($"expected {typeof(T).Name} but {other.GetType().Name} was thrown: {other.Message}"
                    + (context != null ? $" ({context})" : ""));
                return null;
            }
            Fail($"expected {typeof(T).Name} but nothing was thrown" + (context != null ? $" ({context})" : ""));
            return null;
        }

        private static void Fail(string message)
        {
            throw new TestFailure(message);
        }

        private class TestFailure : Exception
        {
            public TestFailure(string message) : base(message) { }
        }
    }
}
