using System;
using System.Collections.Concurrent;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Experimental.DotNetExtensions;
using System.Collections;
using System.Diagnostics;

namespace DotNetExtensionsTestProject1
{
    [TestClass()]
    public class DictionaryExtensionsTests
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }
        public static Dictionary<int, string> TestDictionary;

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void DictionaryTestClassInitialize(TestContext testContext)
        {
            TestDictionary = new Dictionary<int, string>();
        }

        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        [TestInitialize()]
        public void DictionaryTestInitialize()
        {
            TestDictionary.Clear();

            TestDictionary[1] = "a";
            TestDictionary[2] = "b";
            TestDictionary[3] = "c";
        }

        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion

        private static void AssertDictionaryInitialized()
        {
            Assert.AreEqual(TestDictionary[1], "a");
            Assert.AreEqual(TestDictionary[2], "b");
            Assert.AreEqual(TestDictionary[3], "c");
            Assert.IsFalse(TestDictionary.Values.Contains("d"));
            Assert.IsFalse(TestDictionary.Values.Contains("e"));
            Assert.IsFalse(TestDictionary.Values.Contains("f"));
        }

        //[TestMethod()]
        //public void TestAddUnconditionally()
        //{
        //    AssertDictionaryInitialized();

        //    Assert.IsFalse(TestDictionary.AddUnconditionally(5, "e"));
        //    Assert.IsTrue(TestDictionary.Values.Contains("e"));
        //    Assert.AreEqual(TestDictionary[5], "e");

        //    Assert.IsTrue(TestDictionary.AddUnconditionally(5, "f"));
        //    Assert.IsFalse(TestDictionary.Values.Contains("e"));
        //    Assert.AreEqual(TestDictionary[5], "f");
        //}

        [TestMethod()]
        public void TestAddConditionally()
        {
            AssertDictionaryInitialized();

            var dict = TestDictionary
                .AddConditionally(4, "d")
                .AddConditionally(3, "e")
                ;

            Assert.IsNotNull(dict);
            Assert.ReferenceEquals(dict, TestDictionary);

            Assert.AreEqual(dict[3], "c");
            Assert.AreEqual(dict[4], "d");

            Assert.IsFalse(dict.Values.Contains("e"));

            var maybe = TestDictionary.TryGetValue(3);
            Assert.IsTrue(maybe.HasValue);
            Assert.AreEqual(maybe.Value, "c");

            maybe = TestDictionary.TryGetValue(5);
            Assert.IsNull(maybe.Exception);
            Assert.IsFalse(maybe.HasValue);

            dict.Clear();
            Assert.AreEqual(0, dict.Values.Count());
            Assert.AreEqual(0, dict.Keys.Count());

            const int range = 1280;
            var kvps = Enumerable
                .Range(0, range)
                .Select(i => new KeyValuePair<int, string>(i, Convert.ToChar(i).ToString()))
                ;

            dict = kvps.Aggregate(dict, (d, kvp) => d.AddConditionally(kvp));

            Assert.AreEqual(range, dict.Count());

            var cc = new CollectionComparer<KeyValuePair<int, string>>();
            Assert.IsTrue(cc.Equals(kvps, dict));

            CollectionAssert.AreEquivalent(kvps.ToList(), dict.ToList());

            kvps = Enumerable
                .Range(0, range)
                .Select(i => new KeyValuePair<int, string>(i, Convert.ToChar(3 * i + 7).ToString()))
                ;

            dict = kvps.Aggregate(dict, (d, kvp) => d.AddConditionally(kvp));

            CollectionAssert.AreNotEquivalent(kvps.ToList(), dict.ToList());
        }

        [TestMethod()]
        public void TestAddUnConditionally()
        {
            AssertDictionaryInitialized();

            var dict = TestDictionary
                .AddUnconditionally(4, "d")
                .AddUnconditionally(3, "e")
                ;

            Assert.IsNotNull(dict);
            Assert.ReferenceEquals(dict, TestDictionary);

            Assert.AreEqual(dict[3], "e");
            Assert.AreEqual(dict[4], "d");

            Assert.IsFalse(dict.Values.Contains("c"));

            var maybe = TestDictionary.TryGetValue(3);
            Assert.IsTrue(maybe.HasValue);
            Assert.AreEqual(maybe.Value, "e");

            maybe = TestDictionary.TryGetValue(5);
            Assert.IsNull(maybe.Exception);
            Assert.IsFalse(maybe.HasValue);

            dict.Clear();
            Assert.AreEqual(0, dict.Values.Count());
            Assert.AreEqual(0, dict.Keys.Count());

            const int range = 1280;
            var kvps = Enumerable
                .Range(0, range)
                .Select(i => new KeyValuePair<int, string>(i, Convert.ToChar(i).ToString()))
                ;

            dict = kvps.Aggregate(dict, (d, kvp) => d.AddUnconditionally(kvp));

            Assert.AreEqual(range, dict.Count());

            var cc = new CollectionComparer<KeyValuePair<int, string>>();
            Assert.IsTrue(cc.Equals(kvps, dict));

            CollectionAssert.AreEquivalent(kvps.ToList(), dict.ToList());

            kvps = Enumerable
                .Range(0, range)
                .Select(i => new KeyValuePair<int, string>(i, Convert.ToChar(3 * i + 7).ToString()))
                ;

            dict = kvps.Aggregate(dict, (d, kvp) => d.AddUnconditionally(kvp));

            CollectionAssert.AreEquivalent(kvps.ToList(), dict.ToList());
        }

        [TestMethod()]
        public void TestExPerf()
        {
            const int repetitions = 10000;
            AssertDictionaryInitialized();

            TestDictionary = new Dictionary<int, string>();
            var random = new Random();
            var stopwatch = new Stopwatch();
            var kvps = Enumerable
                .Range(0, repetitions)
                .Select(_ => random.Next())
                .Select(i => new KeyValuePair<int, string>(
                    i,
                    i.GetHashCode().ToString()))
                .ToList()
                ;



            stopwatch.Start();
            
            
            
            var dict = kvps.Aggregate(
                TestDictionary as IDictionary<int, string>,
                (d, kvp) => d.AddUnconditionally(kvp));
            Trace.WriteLine(string.Format(
                "MONADIC UNCONDITIONAL DICTIONARY COUNT: {0}",
                dict.Count()));
            var dict1 = dict.ToList();
            var ticks = stopwatch.ElapsedTicks;
            
            
            
            stopwatch.Reset();



            Trace.WriteLine(string.Format(
                "MONADIC UNCONDITIONAL DICTIONARY PERF: Milliseconds to insert {0} items = {1} monadically",
                repetitions,
                Math.Round(ticks / 1.0e4, 3)));



            dict = new Dictionary<int, string>();
            stopwatch.Start();
            
            
            
            foreach (var kvp in kvps)
                dict[kvp.Key] = kvp.Value;
            Trace.WriteLine(string.Format(
                "NON-MONADIC UNCONDITIONAL DICTIONARY COUNT: {0}",
                dict.Count()));
            var dict2 = dict.ToList();
            ticks = stopwatch.ElapsedTicks;


            
            stopwatch.Reset();



            Trace.WriteLine(string.Format(
                "NON-MONADIC UNCONDITIONAL DICTIONARY PERF: Milliseconds to insert {0} items = {1} NON-monadically",
                repetitions,
                Math.Round(ticks / 1.0e4, 3)));


            CollectionAssert.AreEquivalent(dict1, dict2);


            
            dict = new Dictionary<int, string>();
            stopwatch.Start();



            dict = kvps.Aggregate(
                TestDictionary as IDictionary<int, string>,
                (d, kvp) => d.AddConditionally(kvp));
            Trace.WriteLine(string.Format(
                "MONADIC CONDITIONAL DICTIONARY COUNT: {0}",
                dict.Count()));
            dict1 = dict.ToList();
            ticks = stopwatch.ElapsedTicks;



            stopwatch.Reset();



            Trace.WriteLine(string.Format(
                "MONADIC CONDITIONAL DICTIONARY PERF: Milliseconds to insert {0} items = {1} monadically",
                repetitions,
                Math.Round(ticks / 1.0e4, 3)));



            dict = new Dictionary<int, string>();
            stopwatch.Start();



            foreach (var kvp in kvps)
            {
                string value;
                if (!dict.TryGetValue(kvp.Key, out value))
                    dict[kvp.Key] = kvp.Value;
            }
            Trace.WriteLine(string.Format(
                "NON-MONADIC CONDITIONAL DICTIONARY COUNT: {0}",
                dict.Count()));
            dict2 = dict.ToList();
            ticks = stopwatch.ElapsedTicks;

            

            stopwatch.Reset();



            Trace.WriteLine(string.Format(
                "NON-MONADIC CONDITIONAL DICTIONARY PERF: Milliseconds to insert {0} items = {1} NON-monadically",
                repetitions,
                Math.Round(ticks / 1.0e4, 3)));



            CollectionAssert.AreEquivalent(dict1, dict2);
        }
    }

    public class CollectionComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        public bool Equals(IEnumerable<T> first, IEnumerable<T> second)
        {
            if ((first == null) != (second == null))
                return false;
            // At this point, either both null or both not null.
            if (!object.ReferenceEquals(first, second) && (first != null))
            {
                // At this point, both not null.
                if (first.Count() != second.Count())
                    return false;
                if ((first.Count() != 0) && HaveMismatchedElement(first, second))
                    return false;
            }
            return true;
        }

        private static bool HaveMismatchedElement(IEnumerable<T> first, IEnumerable<T> second)
        {
            int firstCount;
            int secondCount;

            var firstTallies = GetElementTally(first, out firstCount);
            var secondTallies = GetElementTally(second, out secondCount);
            // Number of nulls doesn't match
            if (firstCount != secondCount)
                return true;

            foreach (var kvp in firstTallies)
            {
                firstCount = kvp.Value;
                secondTallies.TryGetValue(kvp.Key, out secondCount);
                // Some element tally doesn't match
                if (firstCount != secondCount)
                    return true;
            }
            // All element tallies match
            return false;
        }
        private static Dictionary<T, int> GetElementTally(
            IEnumerable<T> enumerable,
            out int nullTally)
        {
            var dictionary = new Dictionary<T, int>();
            nullTally = 0;
            foreach (T element in enumerable)
            {
                if (element == null)
                {
                    nullTally++;
                }
                else
                {
                    int num;
                    dictionary.TryGetValue(element, out num);
                    num++;
                    dictionary[element] = num;
                }
            }
            return dictionary;
        }
        public int GetHashCode(IEnumerable<T> enumerable)
        {
            int hash = 17;
            foreach (T val in enumerable.OrderBy(x => x))
                hash = hash * 23 + val.GetHashCode();
            return hash;
        }
    }
}

