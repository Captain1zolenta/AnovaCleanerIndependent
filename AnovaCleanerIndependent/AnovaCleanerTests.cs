using System;
using System.Collections.Generic;
using System.Linq;
using AnovaCleaner; // Импортируем пространство имен для использования FactorRow.

namespace AnovaCleaner.Tests
{
    // Класс для хранения параметров тестового случая.
    public class TestCase
    {
        public int FactorCount { get; } // Количество факторов.
        public int[] Levels { get; } // Количество уровней для каждого фактора.
        public double MissingPercentage { get; } // Процент пропущенных комбинаций.

        public TestCase(int factorCount, int[] levels, double missingPercentage)
        {
            FactorCount = factorCount;
            Levels = levels;
            MissingPercentage = missingPercentage;
        }
    }

    // Класс для выполнения тестов очистки таблиц.
    public class AnovaCleanerTests
    {
        private static readonly Random random = new Random();

        // Основной метод для запуска всех тестов.
        public static void RunTests()
        {
            Console.WriteLine("\nRunning ANOVA Cleaner Tests...");

            // Список тестовых случаев (взяты из главы 4 диплома).
            var testCases = new List<TestCase>
            {
                new TestCase(2, new[] { 3, 2 }, 0.167), // 2 фактора, уровни 3 и 2, 16.7% пропущено
                new TestCase(3, new[] { 2, 2, 2 }, 0.25), // 3 фактора, уровни 2, 25% пропущено
                new TestCase(3, new[] { 3, 3, 2 }, 0.296), // 3 фактора, уровни 3, 3, 2, 29.6% пропущено
                new TestCase(2, new[] { 4, 3 }, 0.125), // 2 фактора, уровни 4 и 3, 12.5% пропущено
                new TestCase(3, new[] { 2, 2, 3 }, 0.20), // 3 фактора, уровни 2, 2, 3, 20% пропущено
            };

            int testNumber = 1;
            foreach (var testCase in testCases)
            {
                RunTest(testNumber++, testCase);
            }
        }

        // Метод для выполнения одного теста.
        private static void RunTest(int testNumber, TestCase testCase)
        {
            Console.WriteLine($"\nTest #{testNumber}: Factors={testCase.FactorCount}, Levels={string.Join("x", testCase.Levels)}, Missing={testCase.MissingPercentage * 100:F1}%");

            // Генерация значений факторов.
            var columnValues = Enumerable.Range(0, testCase.FactorCount)
                .Select(i => new HashSet<int>(Enumerable.Range(1, testCase.Levels[i])))
                .ToList();

            // Построение полной таблицы.
            var cartesianProduct = CartesianProduct(columnValues);
            var fullDataset = AddRandomResults(cartesianProduct);

            // Удаление строк для создания неполной таблицы.
            int rowsToRemove = (int)(fullDataset.Count * testCase.MissingPercentage);
            var indicesToRemove = Enumerable.Range(0, fullDataset.Count)
                                           .OrderBy(x => random.Next())
                                           .Take(rowsToRemove)
                                           .ToList();
            var reducedDataset = fullDataset
                .Where((x, i) => !indicesToRemove.Contains(i))
                .ToList();

            // Очистка таблицы.
            var cleaner = new AnovaCleanerConsole();
            var factorColumns = Enumerable.Range(0, testCase.FactorCount).Select(i => $"Factor{i + 1}").ToArray();
            var cleanedDataset = cleaner.CleanDataset(reducedDataset, factorColumns);

            // Проверка полноты после очистки.
            var finalCartesianProduct = CartesianProduct(columnValues);
            bool isFull = cleaner.IsFull(cleanedDataset, finalCartesianProduct);

            // Вывод результатов теста.
            int removedRows = reducedDataset.Count - cleanedDataset.Count;
            Console.WriteLine($"Original Rows: {fullDataset.Count}");
            Console.WriteLine($"Reduced Rows: {reducedDataset.Count}");
            Console.WriteLine($"Cleaned Rows: {cleanedDataset.Count}");
            Console.WriteLine($"Removed During Cleaning: {removedRows}");
            Console.WriteLine($"Structure Full: {(isFull ? "Yes" : "No")}");
        }

        // Построение декартова произведения.
        private static List<FactorRow> CartesianProduct(List<HashSet<int>> sets)
        {
            if (sets == null || sets.Count == 0) return new List<FactorRow>();
            List<FactorRow> resultTuples = new List<FactorRow>();
            CartesianProductRecursive(sets, 0, new List<int>(), resultTuples);
            return resultTuples;
        }

        // Рекурсивный метод для построения декартова произведения.
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

        // Добавление случайного результата в строки.
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