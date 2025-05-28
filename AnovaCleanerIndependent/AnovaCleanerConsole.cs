using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnovaCleaner
{
    public class AnovaCleanerConsole
    {
        private static Random random = new Random();

        public static void Main(string[] args)
        {
            AnovaCleaner.Tests.AnovaCleanerTests.RunTests();
            Console.WriteLine("ANOVA Cleaner Console");
            Console.WriteLine("Enter size category (1, 2, or 3):");

            if (!int.TryParse(Console.ReadLine(), out int sizeCategory) || sizeCategory < 1 || sizeCategory > 3)
            {
                Console.WriteLine("Invalid size category. Please enter 1, 2, or 3.");
                return;
            }

            Console.WriteLine("Enter output file path (or press Enter for default 'output.txt'):");
            string outputPath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = "output.txt";

            var cleaner = new AnovaCleanerConsole();
            cleaner.GenerateAndCleanTable(outputPath, sizeCategory);
        }

        public void GenerateAndCleanTable(string outputPath, int sizeCategory)
        {
            // Определяем параметры таблицы
            int columnCount, maxValuesPerColumn;
            switch (sizeCategory)
            {
                case 1:
                    columnCount = random.Next(2, 4);
                    maxValuesPerColumn = random.Next(3, 7);
                    break;
                case 2:
                    columnCount = random.Next(3, 5);
                    maxValuesPerColumn = random.Next(6, 11);
                    break;
                case 3:
                    columnCount = random.Next(2, 5);
                    maxValuesPerColumn = Math.Min(12, random.Next(8, 13));
                    break;
                default:
                    throw new ArgumentException("sizeCategory must be 1, 2, or 3");
            }

            var factorColumns = Enumerable.Range(0, columnCount).Select(i => $"Factor{i + 1}").ToArray();
            var datasets = GenerateRandomTable(sizeCategory, columnCount, maxValuesPerColumn);
            var fullDataset = datasets[0];
            var reducedDataset = datasets[1];

            // Вывод полной таблицы
            Console.WriteLine($"\nFull Table (Rows: {fullDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in fullDataset)
            {
                Console.WriteLine(row);
            }

            // Вывод уменьшенной таблицы (до очистки)
            Console.WriteLine($"\nReduced Table (Rows: {reducedDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in reducedDataset)
            {
                Console.WriteLine(row);
            }

            // Очистка таблицы
            var cleanedDataset = CleanDataset(reducedDataset, factorColumns);
            Console.WriteLine($"\nCleaned Table (Rows: {cleanedDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in cleanedDataset)
            {
                Console.WriteLine(row);
            }

            // Сохранение уменьшенной таблицы в файл
            SaveDatasetToFile(reducedDataset, outputPath, factorColumns, "Result");
            Console.WriteLine($"\nReduced table saved to {outputPath}");
        }

        private List<List<FactorRow>> GenerateRandomTable(int sizeCategory, int columnCount, int maxValuesPerColumn)
        {
            int rowsToRemove;
            switch (sizeCategory)
            {
                case 1:
                    columnCount = random.Next(2, 4);
                    maxValuesPerColumn = random.Next(3, 7);
                    rowsToRemove = random.Next(Math.Max(0, (int)(CartesianProductCount(columnCount, maxValuesPerColumn) * 0.2) - 1), (int)(CartesianProductCount(columnCount, maxValuesPerColumn) * 0.5));
                    break;
                case 2:
                    columnCount = random.Next(3, 5);
                    maxValuesPerColumn = random.Next(6, 11);
                    rowsToRemove = random.Next(Math.Max(0, (int)(CartesianProductCount(columnCount, maxValuesPerColumn) * 0.2) - 1), (int)(CartesianProductCount(columnCount, maxValuesPerColumn) * 0.5));
                    break;
                case 3:
                    columnCount = random.Next(2, 5);
                    maxValuesPerColumn = Math.Min(12, random.Next(8, 13));
                    rowsToRemove = random.Next(Math.Max(0, (int)(CartesianProductCount(columnCount, maxValuesPerColumn) * 0.2) - 1), (int)(CartesianProductCount(columnCount, maxValuesPerColumn) * 0.5));
                    break;
                default:
                    throw new ArgumentException("sizeCategory must be 1, 2, or 3");
            }

            // Генерация уникальных значений для каждого фактора
            List<HashSet<int>> columnValues = Enumerable.Range(0, columnCount)
                .Select(i => GenerateRandomSet(random, maxValuesPerColumn)).ToList();

            // Полное декартово произведение
            List<FactorRow> cartesianProduct = CartesianProduct(columnValues);

            // Удаление случайных строк для создания неполной таблицы
            var indicesToRemove = Enumerable.Range(0, cartesianProduct.Count)
                                           .OrderBy(x => random.Next())
                                           .Take(rowsToRemove)
                                           .ToList();
            List<FactorRow> reducedDataset = cartesianProduct
                .Where((x, i) => !indicesToRemove.Contains(i))
                .ToList();

            // Добавление случайных результатов
            var fullDatasetWithResults = AddRandomResults(cartesianProduct);
            var reducedDatasetWithResults = AddRandomResults(reducedDataset);

            return new List<List<FactorRow>> { fullDatasetWithResults, reducedDatasetWithResults };
        }

        public List<FactorRow> CleanDataset(List<FactorRow> dataset, string[] factorColumns)
        {
            // Шаг 1: Извлечение уникальных значений факторов
            var uniqueValues = new Dictionary<int, HashSet<int>>();
            for (int factorIndex = 0; factorIndex < factorColumns.Length; factorIndex++)
            {
                uniqueValues[factorIndex] = new HashSet<int>(
                    dataset.Select(row => row.FactorValues[factorIndex]));
            }

            // Шаг 2: Построение декартова произведения
            var cartesianProduct = CartesianProduct(uniqueValues.Values.ToList());

            // Шаг 3: Проверка полноты
            if (IsFull(dataset, cartesianProduct))
            {
                Console.WriteLine("Dataset is already full. No cleaning needed.");
                return dataset;
            }

            // Шаг 4: Выделение редких значений
            var frequency = new Dictionary<int, Dictionary<int, int>>();
            for (int factorIndex = 0; factorIndex < factorColumns.Length; factorIndex++)
            {
                frequency[factorIndex] = new Dictionary<int, int>();
                foreach (var value in uniqueValues[factorIndex])
                {
                    frequency[factorIndex][value] = dataset.Count(row => row.FactorValues[factorIndex] == value);
                }
            }

            // Шаг 5: Генерация вариантов очистки
            var alternatives = new List<(List<FactorRow> cleanedData, int removedRows)>();
            foreach (var factorIndex in frequency.Keys)
            {
                var rareValues = frequency[factorIndex]
                    .OrderBy(kv => kv.Value)
                    .Take(1) // Берем только одно самое редкое значение
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var rareValue in rareValues)
                {
                    var cleanedData = dataset
                        .Where(row => row.FactorValues[factorIndex] != rareValue)
                        .ToList();
                    int removedRows = dataset.Count - cleanedData.Count;
                    alternatives.Add((cleanedData, removedRows));
                }
            }

            // Шаг 6: Повторная проверка полноты
            List<FactorRow> bestCleanedData = null;
            int minRemovedRows = int.MaxValue;

            foreach (var (cleanedData, removedRows) in alternatives)
            {
                if (IsFull(cleanedData, cartesianProduct) && removedRows < minRemovedRows)
                {
                    bestCleanedData = cleanedData;
                    minRemovedRows = removedRows;
                }
            }

            // Шаг 7: Рекурсивная очистка, если необходимо
            if (bestCleanedData == null)
            {
                Console.WriteLine("No single removal achieved fullness. Trying recursive cleaning...");
                foreach (var (cleanedData, _) in alternatives.OrderBy(a => a.removedRows))
                {
                    var recursiveResult = CleanDataset(cleanedData, factorColumns);
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
                return dataset; // Возвращаем исходные данные, если не удалось очистить
            }

            Console.WriteLine($"Removed {dataset.Count - bestCleanedData.Count} rows to achieve full structure.");
            return bestCleanedData;
        }

        public bool IsFull(List<FactorRow> dataset, List<FactorRow> cartesianProduct)
        {
            var datasetCombinations = new HashSet<string>(
                dataset.Select(row => string.Join("|", row.FactorValues)));
            var requiredCombinations = new HashSet<string>(
                cartesianProduct.Select(row => string.Join("|", row.FactorValues)));

            return requiredCombinations.All(comb => datasetCombinations.Contains(comb));
        }

        private List<FactorRow> AddRandomResults(List<FactorRow> dataset)
        {
            return dataset.Select(row =>
            {
                var newValues = row.FactorValues.ToList();
                newValues.Add((int)(random.NextDouble() * 100)); // Случайный результат
                return new FactorRow(newValues.ToArray());
            }).ToList();
        }

        private HashSet<int> GenerateRandomSet(Random random, int maxValues)
        {
            int count = random.Next(1, maxValues + 1);
            return new HashSet<int>(Enumerable.Range(0, count).Select(_ => random.Next(1, 101)));
        }

        private List<FactorRow> CartesianProduct(List<HashSet<int>> sets)
        {
            if (sets == null || sets.Count == 0) return new List<FactorRow>();
            List<FactorRow> resultTuples = new List<FactorRow>();
            CartesianProductRecursive(sets, 0, new List<int>(), resultTuples);
            return resultTuples;
        }

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

        private long CartesianProductCount(int columnCount, int maxValuesPerColumn)
        {
            long result = 1;
            for (int i = 0; i < columnCount; i++)
            {
                result *= maxValuesPerColumn;
            }
            return result;
        }
    }

    public class FactorRow
    {
        public int[] FactorValues { get; }

        public FactorRow(int[] values)
        {
            FactorValues = values ?? throw new ArgumentNullException(nameof(values));
        }

        public override string ToString()
        {
            return string.Join("\t", FactorValues);
        }

        public override bool Equals(object obj)
        {
            if (obj is FactorRow other)
            {
                if (FactorValues.Length != other.FactorValues.Length) return false;
                return !FactorValues.Where((t, i) => t != other.FactorValues[i]).Any();
            }
            return false;
        }

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
}