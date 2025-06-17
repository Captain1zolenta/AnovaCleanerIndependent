using System;
using System.Collections.Generic;
using System.Linq;

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

    // Класс для выполнения тестов.
    public class AnovaCleanerTests
    {
        private static readonly Random random = new Random();

        // Метод запуска всех тестов.
        public static void RunTests()
        {
            Console.WriteLine("\nRunning ANOVA Cleaner Tests...");

            var testCases = new List<TestCase>
            {
                new TestCase(2, new[] { 3, 2 }, 0.167), // 2 фактора, уровни 3 и 2, 16.7% пропущено
                new TestCase(3, new[] { 2, 2, 2 }, 0.25), // 3 фактора, уровни 2, 2, 2, 25% пропущено
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

            // Полное декартово произведение.
            var cartesianProduct = CartesianProduct(columnValues);
            var fullDataset = AddRandomResults(cartesianProduct);

            // Удаление строк с сохранением полноты.
            int totalCombinations = cartesianProduct.Count;
            int rowsToRemove = (int)(totalCombinations * testCase.MissingPercentage);
            var indicesToRemove = GenerateSafeRemovalIndices(fullDataset, cartesianProduct, rowsToRemove);

            var reducedDataset = fullDataset
                .Where((x, i) => !indicesToRemove.Contains(i))
                .ToList();

            // Удаление дубликатов.
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

            // Создание экземпляра очистки.
            var cleaner = new AnovaCleanerConsole();
            var factorColumns = Enumerable.Range(0, testCase.FactorCount).Select(i => $"Factor{i + 1}").ToArray();

            // Очистка таблицы.
            var cleanedDataset = cleaner.CleanDataset(reducedDataset, factorColumns);

            // Проверка полноты после очистки.
            var finalCartesianProduct = CartesianProduct(columnValues);
            bool isFull = cleaner.IsFull(cleanedDataset, finalCartesianProduct);

            // Отчёт по тесту.
            int removedRows = reducedDataset.Count - cleanedDataset.Count();
            int lostCombinations = finalCartesianProduct.Count - CountPresentCombinations(cleanedDataset, finalCartesianProduct);

            Console.WriteLine($"Original Rows: {fullDataset.Count}");
            Console.WriteLine($"Reduced Rows: {reducedDataset.Count}");
            Console.WriteLine($"Cleaned Rows: {cleanedDataset.Count}");
            Console.WriteLine($"Removed During Cleaning: {removedRows}");
            Console.WriteLine($"Lost Combinations: {lostCombinations}");
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

        // Рекурсивный метод построения декартова произведения.
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

        // Добавление случайного результата к строкам.
        private static List<FactorRow> AddRandomResults(List<FactorRow> dataset)
        {
            return dataset.Select(row =>
            {
                var values = row.FactorValues.ToList();
                values.Add(random.Next(0, 100)); // Результат
                return new FactorRow(values.ToArray());
            }).ToList();
        }

        // Генерация безопасных индексов для удаления (не затрагивает уникальные комбинации).
        private static List<int> GenerateSafeRemovalIndices(List<FactorRow> fullDataset, List<FactorRow> cartesianProduct, int rowsToRemove)
        {
            var present = cartesianProduct.ToDictionary(
                r => string.Join("|", r.FactorValues),
                r => new List<int>()
            );

            for (int i = 0; i < fullDataset.Count; i++)
            {
                var key = string.Join("|", fullDataset[i].FactorValues.Take(cartesianProduct[0].FactorValues.Length));
                if (present.ContainsKey(key))
                {
                    present[key].Add(i);
                }
            }

            var removableIndices = new List<int>();
            var usedKeys = new HashSet<string>();

            foreach (var kvp in present)
            {
                var indices = kvp.Value;
                if (indices.Count > 1)
                {
                    // Можно удалить все, кроме одной
                    for (int i = 1; i < indices.Count && removableIndices.Count < rowsToRemove; i++)
                    {
                        removableIndices.Add(indices[i]);
                    }
                }
                else
                {
                    usedKeys.Add(kvp.Key); // Эта комбинация обязательна
                }
            }

            // Если нужно удалить больше строк — удаляем из повторяющихся
            while (removableIndices.Count < rowsToRemove)
            {
                var extra = fullDataset
                    .Select((r, i) => new { Index = i, Key = string.Join("|", r.FactorValues) })
                    .Where(x => !usedKeys.Contains(x.Key))
                    .OrderBy(x => random.Next())
                    .Take(rowsToRemove - removableIndices.Count)
                    .Select(x => x.Index)
                    .ToList();

                removableIndices.AddRange(extra);
            }

            return removableIndices;
        }

        // Подсчёт количества существующих комбинаций в датасете.
        private static int CountPresentCombinations(List<FactorRow> dataset, List<FactorRow> cartesianProduct)
        {
            var present = new HashSet<string>(
                dataset.Select(r => string.Join("|", r.FactorValues.Take(dataset[0].FactorValues.Length - 1)))
            );
            var required = new HashSet<string>(
                cartesianProduct.Select(r => string.Join("|", r.FactorValues))
            );
            return required.Count(comb => present.Contains(comb));
        }
    }
}