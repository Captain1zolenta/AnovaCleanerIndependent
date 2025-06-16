using System;
using System.Collections.Generic;
using System.Linq;

namespace AnovaCleaner.Tests
{
    public class TestCase
    {
        public int FactorCount { get; }
        public int[] Levels { get; }
        public double MissingPercentage { get; }

        public TestCase(int factorCount, int[] levels, double missingPercentage)
        {
            FactorCount = factorCount;
            Levels = levels;
            MissingPercentage = missingPercentage;
        }
    }

    public class AnovaCleanerTests
    {
        private static readonly Random random = new Random();

        public static void RunTests()
        {
            Console.WriteLine("\nRunning ANOVA Cleaner Tests...");

            var testCases = new List<TestCase>
            {
                new TestCase(2, new[] { 3, 2 }, 0.10),
                new TestCase(3, new[] { 2, 2, 2 }, 0.15),
                new TestCase(3, new[] { 3, 3, 2 }, 0.20),
                new TestCase(2, new[] { 4, 3 }, 0.10),
                new TestCase(3, new[] { 2, 2, 3 }, 0.15)
            };

            int testNumber = 1;
            foreach (var testCase in testCases)
            {
                RunTest(testNumber++, testCase);
            }
        }

        private static void RunTest(int testNumber, TestCase testCase)
        {
            Console.WriteLine($"\nTest #{testNumber}: Factors={testCase.FactorCount}, Levels={string.Join("x", testCase.Levels)}, Missing={testCase.MissingPercentage * 100:F1}%");

            var columnValues = Enumerable.Range(0, testCase.FactorCount)
                .Select(i => new HashSet<int>(Enumerable.Range(1, testCase.Levels[i])))
                .ToList();
            var cartesianProduct = CartesianProduct(columnValues);
            var fullDataset = AddRandomResults(cartesianProduct);

            int totalCombinations = cartesianProduct.Count;
            int rowsToRemove = Math.Min((int)(totalCombinations * testCase.MissingPercentage), totalCombinations - testCase.FactorCount);
            var indicesToRemove = Enumerable.Range(0, fullDataset.Count)
                                           .OrderBy(x => random.Next())
                                           .Take(rowsToRemove)
                                           .ToList();
            var reducedDataset = fullDataset
                .Where((x, i) => !indicesToRemove.Contains(i))
                .ToList();

            int initialReducedCount = reducedDataset.Count;
            reducedDataset = reducedDataset
                .GroupBy(row => string.Join(",", row.FactorValues.Take(testCase.FactorCount)))
                .Select(g => g.First())
                .ToList();
            int duplicatesRemoved = initialReducedCount - reducedDataset.Count();
            if (duplicatesRemoved > 0)
            {
                Console.WriteLine($"Removed {duplicatesRemoved} duplicate rows from reduced dataset.");
            }

            var cleaner = new AnovaCleanerConsole();
            var factorColumns = Enumerable.Range(0, testCase.FactorCount).Select(i => $"Factor{i + 1}").ToArray();
            var cleanedDataset = cleaner.CleanDataset(reducedDataset, factorColumns);

            var finalCartesianProduct = CartesianProduct(columnValues);
            bool isFull = cleaner.IsFull(cleanedDataset, finalCartesianProduct);

            int removedRows = reducedDataset.Count() - cleanedDataset.Count();
            Console.WriteLine($"Original Rows: {fullDataset.Count}");
            Console.WriteLine($"Reduced Rows: {reducedDataset.Count()}");
            Console.WriteLine($"Cleaned Rows: {cleanedDataset.Count()}");
            Console.WriteLine($"Removed During Cleaning: {removedRows}");
            Console.WriteLine($"Lost Combinations: {cleaner.GetMissingCombinations(cleanedDataset, finalCartesianProduct).Count}");
            Console.WriteLine($"Structure Full: {(isFull ? "Yes" : "No")}");
        }

        private static List<FactorRow> CartesianProduct(List<HashSet<int>> sets)
        {
            if (sets == null || sets.Count == 0) return new List<FactorRow>();
            List<FactorRow> resultTuples = new List<FactorRow>();
            CartesianProductRecursive(sets, 0, new List<int>(), resultTuples);
            return resultTuples;
        }

        private static void CartesianProductRecursive(List<HashSet<int>> sets, int index, List<int> current, List<FactorRow> result)
        {
            if (index == sets.Count)
            {
                result.Add(new FactorRow(current.ToArray()));
                return;
            }
            foreach (var value in sets[index])
            {
                current.Add(value);
                CartesianProductRecursive(sets, index + 1, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }

        private static List<FactorRow> AddRandomResults(List<FactorRow> dataset)
        {
            return dataset.Select(row =>
            {
                var newValues = row.FactorValues.ToList();
                newValues.Add((int)(random.NextDouble() * 100));
                return new FactorRow(newValues.ToArray());
            }).ToList();
        }
    }
}