using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnovaCleaner
{
    // Класс FactorRow представляет строку таблицы с значениями факторов и результатом.
    public class FactorRow
    {
        // Массив значений: первые N элементов — значения факторов, последний — результат.
        public int[] FactorValues { get; }

        public FactorRow(int[] values)
        {
            FactorValues = values ?? throw new ArgumentNullException(nameof(values));
        }

        // Переопределение ToString для вывода строки в консоль или файл.
        public override string ToString()
        {
            return string.Join("\t", FactorValues);
        }

        // Переопределение Equals для сравнения строк.
        public override bool Equals(object obj)
        {
            if (obj is FactorRow other)
            {
                if (FactorValues.Length != other.FactorValues.Length) return false;
                return !FactorValues.Where((t, i) => t != other.FactorValues[i]).Any();
            }
            return false;
        }

        // Переопределение GetHashCode для корректной работы HashSet.
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var value in FactorValues)
                {
                    hash = hash * 23 + value.GetHashCode();
                }
                return hash;
            }
        }
    }

    // Основной класс консольного приложения для очистки таблиц ANOVA.
    public class AnovaCleanerConsole
    {
        private static readonly Random random = new Random();

        // Точка входа в приложение.
        public static void Main(string[] args)
        {
            Console.WriteLine("ANOVA Cleaner Console");
            Console.WriteLine("Enter size category (1, 2, or 3):");

            // Ввод категории размера (1 — малый, 2 — средний, 3 — большой).
            if (!int.TryParse(Console.ReadLine(), out int sizeCategory) || sizeCategory < 1 || sizeCategory > 3)
            {
                Console.WriteLine("Invalid size category. Please enter 1, 2, or 3.");
                return;
            }

            Console.WriteLine("Enter output file path (or press Enter for default 'output.txt'):");
            string outputPath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = "output.txt";

            // Генерация и очистка таблицы.
            var cleaner = new AnovaCleanerConsole();
            cleaner.GenerateAndCleanTable(outputPath, sizeCategory);

            // Запуск тестов после выполнения основной логики.
            AnovaCleaner.Tests.AnovaCleanerTests.RunTests();
        }

        // Метод для генерации, уменьшения и очистки таблицы.
        public void GenerateAndCleanTable(string outputPath, int sizeCategory)
        {
            // Определяем параметры таблицы в зависимости от категории.
            int columnCount, maxValuesPerColumn;
            switch (sizeCategory)
            {
                case 1:
                    columnCount = random.Next(2, 4); // 2–3 фактора
                    maxValuesPerColumn = random.Next(3, 7); // 3–6 уровней
                    break;
                case 2:
                    columnCount = random.Next(3, 5); // 3–4 фактора
                    maxValuesPerColumn = random.Next(6, 11); // 6–10 уровней
                    break;
                case 3:
                    columnCount = random.Next(2, 5); // 2–4 фактора
                    maxValuesPerColumn = Math.Min(12, random.Next(8, 13)); // 8–12 уровней
                    break;
                default:
                    throw new ArgumentException("sizeCategory must be 1, 2, or 3");
            }

            // Названия столбцов (Factor1, Factor2, ...).
            var factorColumns = Enumerable.Range(0, columnCount).Select(i => $"Factor{i + 1}").ToArray();

            // Генерация полной и уменьшенной таблиц.
            var datasets = GenerateRandomTable(sizeCategory, columnCount, maxValuesPerColumn);
            var fullDataset = datasets[0];
            var reducedDataset = datasets[1];

            // Вывод полной таблицы.
            Console.WriteLine($"\nFull Table (Rows: {fullDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in fullDataset)
            {
                Console.WriteLine(row);
            }

            // Вывод уменьшенной таблицы.
            Console.WriteLine($"\nReduced Table (Rows: {reducedDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in reducedDataset)
            {
                Console.WriteLine(row);
            }

            // Очистка таблицы.
            var cleanedDataset = CleanDataset(reducedDataset, factorColumns);
            Console.WriteLine($"\nCleaned Table (Rows: {cleanedDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in cleanedDataset)
            {
                Console.WriteLine(row);
            }

            // Сохранение очищенной таблицы в файл (даже если очистка не удалась).
            SaveDatasetToFile(cleanedDataset, outputPath, factorColumns, "Result");
            Console.WriteLine($"\nTable saved to {outputPath}");
        }

        // Генерация полной и уменьшенной таблиц.
        private List<List<FactorRow>> GenerateRandomTable(int sizeCategory, int columnCount, int maxValuesPerColumn)
        {
            // Генерация уникальных значений для каждого фактора.
            List<HashSet<int>> columnValues = Enumerable.Range(0, columnCount)
                .Select(i => GenerateRandomSet(random, maxValuesPerColumn)).ToList();

            // Полное декартово произведение.
            List<FactorRow> cartesianProduct = CartesianProduct(columnValues);

            // Удаление случайных строк для создания неполной таблицы.
            int actualSize = cartesianProduct.Count;
            int minRowsToRemove = Math.Max(0, (int)(actualSize * 0.2) - 1);
            int maxRowsToRemove = (int)(actualSize * 0.5);
            int rowsToRemove = random.Next(minRowsToRemove, maxRowsToRemove + 1);

            var indicesToRemove = Enumerable.Range(0, cartesianProduct.Count)
                                           .OrderBy(x => random.Next())
                                           .Take(rowsToRemove)
                                           .ToList();
            List<FactorRow> reducedDataset = cartesianProduct
                .Where((x, i) => !indicesToRemove.Contains(i))
                .ToList();

            // Добавление случайных результатов.
            var fullDatasetWithResults = AddRandomResults(cartesianProduct);
            var reducedDatasetWithResults = AddRandomResults(reducedDataset);

            // Удаление дубликатов из уменьшенной таблицы.
            reducedDatasetWithResults = reducedDatasetWithResults
                .GroupBy(row => string.Join(",", row.FactorValues.Take(columnCount)))
                .Select(g => g.First())
                .ToList();

            Console.WriteLine($"Generated reduced table with {reducedDatasetWithResults.Count} rows after removing duplicates.");
            return new List<List<FactorRow>> { fullDatasetWithResults, reducedDatasetWithResults };
        }

        // Метод очистки таблицы (алгоритм из диплома).
        public List<FactorRow> CleanDataset(List<FactorRow> dataset, string[] factorColumns)
        {
            // Шаг 1: Удаление дубликатов перед началом очистки.
            int initialCount = dataset.Count;
            dataset = dataset
                .GroupBy(row => string.Join(",", row.FactorValues.Take(factorColumns.Length)))
                .Select(g => g.First())
                .ToList();
            int duplicatesRemoved = initialCount - dataset.Count;
            if (duplicatesRemoved > 0)
            {
                Console.WriteLine($"Removed {duplicatesRemoved} duplicate rows before cleaning.");
            }

            // Шаг 2: Извлечение уникальных значений факторов.
            var uniqueValues = new Dictionary<int, HashSet<int>>();
            for (int factorIndex = 0; factorIndex < factorColumns.Length; factorIndex++)
            {
                uniqueValues[factorIndex] = new HashSet<int>(
                    dataset.Select(row => row.FactorValues[factorIndex]));
            }

            // Шаг 3: Построение декартова произведения.
            var cartesianProduct = CartesianProduct(uniqueValues.Values.ToList());

            // Шаг 4: Проверка полноты.
            if (IsFull(dataset, cartesianProduct))
            {
                Console.WriteLine("Dataset is already full. No cleaning needed.");
                return dataset;
            }

            // Шаг 5: Кэширование счётчиков редких элементов для оптимизации.
            var frequencyCache = new Dictionary<int, Dictionary<int, int>>();
            for (int factorIndex = 0; factorIndex < factorColumns.Length; factorIndex++)
            {
                frequencyCache[factorIndex] = new Dictionary<int, int>();
                foreach (var value in uniqueValues[factorIndex])
                {
                    frequencyCache[factorIndex][value] = dataset.Count(row => row.FactorValues[factorIndex] == value);
                }
            }

            // Шаг 6: Рекурсивная очистка с использованием кэша.
            return CleanDatasetRecursive(dataset, cartesianProduct, factorColumns.Length, uniqueValues, frequencyCache, 0);
        }

        // Рекурсивная очистка таблицы с ограничением глубины и использованием кэша.
        private List<FactorRow> CleanDatasetRecursive(List<FactorRow> dataset, List<FactorRow> cartesianProduct, int factorCount, Dictionary<int, HashSet<int>> uniqueValues, Dictionary<int, Dictionary<int, int>> frequencyCache, int depth)
        {
            // Ограничение глубины рекурсии (увеличено до 50 для сложных случаев).
            if (depth > 50) // Увеличен лимит с 20 до 50 для обработки сложных случаев.
            {
                Console.WriteLine("Failed to achieve full structure due to recursion depth limit.");
                return dataset;
            }

            // Шаг 1: Определяем покрытие комбинаций.
            var missingCombinations = new HashSet<string>(
                cartesianProduct.Select(row => string.Join("|", row.FactorValues)));
            var presentCombinations = new HashSet<string>(
                dataset.Select(row => string.Join("|", row.FactorValues.Take(factorCount))));
            missingCombinations.ExceptWith(presentCombinations);

            Console.WriteLine($"Depth {depth}: Missing combinations ({missingCombinations.Count}): {string.Join(", ", missingCombinations)}");

            if (missingCombinations.Count == 0)
            {
                Console.WriteLine("Dataset is now full after cleaning.");
                return dataset;
            }

            // Шаг 2: Анализ отсутствующих комбинаций для выбора целевого фактора.
            var missingCountPerFactor = new Dictionary<int, int>();
            for (int factorIndex = 0; factorIndex < factorCount; factorIndex++)
            {
                missingCountPerFactor[factorIndex] = missingCombinations
                    .Count(comb => comb.Split('|')[factorIndex] != presentCombinations
                        .Select(p => p.Split('|')[factorIndex]).Distinct().FirstOrDefault());
            }

            // Шаг 3: Выбираем фактор с наибольшим числом отсутствующих комбинаций.
            int targetFactorIndex = missingCountPerFactor.OrderByDescending(kv => kv.Value).First().Key;
            var targetValues = missingCombinations
                .Select(comb => int.Parse(comb.Split('|')[targetFactorIndex]))
                .Distinct()
                .ToList();

            // Шаг 4: Удаляем строки с значениями, связанными с наиболее отсутствующими комбинациями.
            var alternatives = new List<(List<FactorRow> cleanedData, int removedRows, Dictionary<int, Dictionary<int, int>> newCache)>();
            foreach (var targetValue in targetValues.Take(1)) // Берем первое значение для текущей итерации
            {
                var cleanedData = dataset
                    .Where(row => row.FactorValues[targetFactorIndex] != targetValue)
                    .ToList();
                int removedRows = dataset.Count - cleanedData.Count;

                // Обновление кэша счётчиков после удаления строк.
                var newFrequencyCache = new Dictionary<int, Dictionary<int, int>>(frequencyCache);
                newFrequencyCache[targetFactorIndex] = new Dictionary<int, int>(frequencyCache[targetFactorIndex]);
                foreach (var row in dataset.Where(row => row.FactorValues[targetFactorIndex] == targetValue))
                {
                    for (int i = 0; i < factorCount; i++)
                    {
                        if (!newFrequencyCache[i].ContainsKey(row.FactorValues[i])) continue;
                        newFrequencyCache[i][row.FactorValues[i]]--;
                    }
                }
                alternatives.Add((cleanedData, removedRows, newFrequencyCache));
            }

            // Шаг 5: Повторная проверка полноты.
            List<FactorRow> bestCleanedData = null;
            int minRemovedRows = int.MaxValue;
            Dictionary<int, Dictionary<int, int>> bestCache = frequencyCache;

            foreach (var (cleanedData, removedRows, newCache) in alternatives)
            {
                if (IsFull(cleanedData, cartesianProduct) && removedRows < minRemovedRows)
                {
                    bestCleanedData = cleanedData;
                    minRemovedRows = removedRows;
                    bestCache = new Dictionary<int, Dictionary<int, int>>(newCache);
                }
            }

            // Шаг 6: Рекурсивная очистка, если необходимо.
            if (bestCleanedData == null)
            {
                Console.WriteLine("No single removal achieved fullness. Trying recursive cleaning...");
                foreach (var (cleanedData, _, newCache) in alternatives.OrderBy(a => a.removedRows))
                {
                    var recursiveResult = CleanDatasetRecursive(cleanedData, cartesianProduct, factorCount, uniqueValues, newCache, depth + 1);
                    if (IsFull(recursiveResult, cartesianProduct))
                    {
                        bestCleanedData = recursiveResult;
                        break;
                    }
                }
            }

            if (bestCleanedData == null)
            {
                Console.WriteLine("Failed to achieve full structure.");
                return dataset; // Возвращаем исходные данные, если не удалось очистить.
            }

            Console.WriteLine($"Removed {dataset.Count - bestCleanedData.Count} rows to achieve full structure.");
            return bestCleanedData;
        }

        // Проверка, является ли таблица полной (все комбинации присутствуют).
        public bool IsFull(List<FactorRow> dataset, List<FactorRow> cartesianProduct)
        {
            var datasetCombinations = new HashSet<string>(
                dataset.Select(row => string.Join("|", row.FactorValues.Take(row.FactorValues.Length - 1))));
            var requiredCombinations = new HashSet<string>(
                cartesianProduct.Select(row => string.Join("|", row.FactorValues)));

            return requiredCombinations.All(comb => datasetCombinations.Contains(comb));
        }

        // Добавление случайного результата в каждую строку.
        private List<FactorRow> AddRandomResults(List<FactorRow> dataset)
        {
            return dataset.Select(row =>
            {
                var newValues = row.FactorValues.ToList();
                newValues.Add((int)(random.NextDouble() * 100)); // Случайный результат (0–100).
                return new FactorRow(newValues.ToArray());
            }).ToList();
        }

        // Генерация случайного набора значений для фактора.
        private HashSet<int> GenerateRandomSet(Random random, int maxValues)
        {
            int count = random.Next(1, maxValues + 1);
            return new HashSet<int>(Enumerable.Range(0, count).Select(_ => random.Next(1, 101)));
        }

        // Построение декартова произведения.
        private List<FactorRow> CartesianProduct(List<HashSet<int>> sets)
        {
            if (sets == null || sets.Count == 0) return new List<FactorRow>();
            List<FactorRow> resultTuples = new List<FactorRow>();
            CartesianProductRecursive(sets, 0, new List<int>(), resultTuples);
            return resultTuples;
        }

        // Рекурсивный метод для построения декартова произведения.
        private void CartesianProductRecursive(List<HashSet<int>> sets, int index, List<int> current, List<FactorRow> result)
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

        // Сохранение таблицы в текстовый файл.
        private void SaveDatasetToFile(List<FactorRow> dataset, string filePath, string[] factorColumns, string resultColumn)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine($"{string.Join("\t", factorColumns)}\t{resultColumn}");
                foreach (var row in dataset)
                {
                    writer.WriteLine(row.ToString());
                }
            }
        }
    }
}