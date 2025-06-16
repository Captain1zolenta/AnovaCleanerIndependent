using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnovaCleaner
{
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

    public class AnovaCleanerConsole
    {
        private static readonly Random random = new Random();

        // Точка входа в приложение
        public static void Main()
        {
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
            AnovaCleaner.Tests.AnovaCleanerTests.RunTests();
        }

        // Метод для генерации, уменьшения и очистки таблицы
        public void GenerateAndCleanTable(string outputPath, int sizeCategory)
        {
            // Определяем параметры таблицы в зависимости от категории
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

            // Названия столбцов
            var factorColumns = Enumerable.Range(0, columnCount).Select(i => $"Factor{i + 1}").ToArray();

            // Генерация полной и уменьшенной таблиц
            var datasets = GenerateRandomTable(sizeCategory, columnCount, maxValuesPerColumn);
            var fullDataset = datasets[0];
            var reducedDataset = datasets[1];

            // Вывод полной таблицы
            Console.WriteLine($"\nFull Table (Rows: {fullDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in fullDataset) Console.WriteLine(row);

            // Вывод уменьшенной таблицы
            Console.WriteLine($"\nReduced Table (Rows: {reducedDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in reducedDataset) Console.WriteLine(row);

            // Очистка таблицы
            var cleanedDataset = CleanDataset(reducedDataset, factorColumns);
            Console.WriteLine($"\nCleaned Table (Rows: {cleanedDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in cleanedDataset) Console.WriteLine(row);

            // Сохранение очищенной таблицы и отчёт
            SaveDatasetToFile(cleanedDataset, outputPath, factorColumns, "Result");
            Console.WriteLine($"\nTable saved to {outputPath}");
            var missingCombinations = GetMissingCombinations(cleanedDataset, CartesianProduct(GetUniqueValues(reducedDataset, factorColumns).Values.ToList()));
            Console.WriteLine($"Report: Removed {reducedDataset.Count - cleanedDataset.Count} rows. Lost Combinations: {missingCombinations.Count}. Structure Full: {IsFull(cleanedDataset, CartesianProduct(GetUniqueValues(reducedDataset, factorColumns).Values.ToList()))}");
            if (missingCombinations.Any())
            {
                Console.WriteLine("Missing Combinations:");
                foreach (var comb in missingCombinations) Console.WriteLine(comb);
            }
        }

        // Генерация полной и уменьшенной таблиц
        private List<List<FactorRow>> GenerateRandomTable(int sizeCategory, int columnCount, int maxValuesPerColumn)
        {
            // Генерация уникальных значений для каждого фактора
            List<HashSet<int>> columnValues = Enumerable.Range(0, columnCount)
                .Select(i => GenerateRandomSet(random, maxValuesPerColumn)).ToList();

            // Полное декартово произведение
            List<FactorRow> cartesianProduct = CartesianProduct(columnValues);

            // Удаление случайных строк для создания неполной таблицы
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

            // Добавление случайных результатов
            var fullDatasetWithResults = AddRandomResults(cartesianProduct);
            var reducedDatasetWithResults = AddRandomResults(reducedDataset);

            // Удаление дубликатов из уменьшенной таблицы
            reducedDatasetWithResults = reducedDatasetWithResults
                .GroupBy(row => string.Join(",", row.FactorValues.Take(columnCount)))
                .Select(g => g.First())
                .ToList();

            Console.WriteLine($"Generated reduced table with {reducedDatasetWithResults.Count} rows after removing duplicates.");
            return new List<List<FactorRow>> { fullDatasetWithResults, reducedDatasetWithResults };
        }

        // Метод очистки таблицы (обёртка для совместимости с тестами)
        public List<FactorRow> CleanDataset(List<FactorRow> dataset, string[] factorColumns)
        {
            var uniqueValues = GetUniqueValues(dataset, factorColumns);
            var cartesianProduct = CartesianProduct(uniqueValues.Values.ToList());
            return CleanDatasetFrequencyBased(dataset, factorColumns.Length, cartesianProduct);
        }

        // Получение уникальных значений факторов
        private Dictionary<int, HashSet<int>> GetUniqueValues(List<FactorRow> dataset, string[] factorColumns)
        {
            var uniqueValues = new Dictionary<int, HashSet<int>>();
            for (int factorIndex = 0; factorIndex < factorColumns.Length; factorIndex++)
            {
                uniqueValues[factorIndex] = new HashSet<int>(dataset.Select(row => row.FactorValues[factorIndex]));
            }
            return uniqueValues;
        }

        // Очистка таблицы на основе частоты встречаемости уникальных значений
        private List<FactorRow> CleanDatasetFrequencyBased(List<FactorRow> dataset, int factorCount, List<FactorRow> cartesianProduct)
        {
            // Генерация словарей с частотой встречаемости уникальных значений каждой колонки
            var frequencyDicts = new Dictionary<int, Dictionary<int, int>>();
            for (int col = 0; col < factorCount; col++)
            {
                var freqDict = new Dictionary<int, int>();
                foreach (var row in dataset)
                {
                    int value = row.FactorValues[col];
                    freqDict[value] = freqDict.GetValueOrDefault(value, 0) + 1;
                }
                frequencyDicts[col] = freqDict;
            }

            // Вывод исходных частот для отладки
            Console.WriteLine("\nInitial Frequency per Column:");
            foreach (var kvp in frequencyDicts)
            {
                Console.WriteLine($"Column {kvp.Key}: {string.Join(", ", kvp.Value.Select(kv => $"{kv.Key}:{kv.Value}"))}");
            }

            // Выбор самых редких значений по каждой колонке
            var rareValues = new List<(int Column, int Value)>();
            foreach (var kvp in frequencyDicts)
            {
                int col = kvp.Key;
                var minFreq = kvp.Value.Values.Min();
                if (minFreq > 0)
                {
                    var rareVals = kvp.Value.Where(kv => kv.Value == minFreq).Select(kv => kv.Key).ToList();
                    if (rareVals.Any())
                    {
                        rareValues.Add((col, rareVals[random.Next(rareVals.Count)]));
                    }
                }
            }

            // Генерация кандидатов с анализом полноты и сохранением уровней
            var candidateDatasets = new List<(List<FactorRow> Dataset, int MissingCount)>();
            var originalUniqueValues = GetUniqueValues(dataset, Enumerable.Range(0, factorCount).Select(i => $"Factor{i + 1}").ToArray());
            var originalMissing = GetMissingCombinations(dataset, cartesianProduct).Count;

            foreach (var (col, val) in rareValues)
            {
                var copy = new List<FactorRow>(dataset);
                copy.RemoveAll(row => row.FactorValues[col] == val);
                var newUniqueValues = GetUniqueValues(copy, Enumerable.Range(0, factorCount).Select(i => $"Factor{i + 1}").ToArray());
                bool allLevelsPreserved = true;
                for (int c = 0; c < factorCount; c++)
                {
                    if (!newUniqueValues[c].SetEquals(originalUniqueValues[c]))
                    {
                        allLevelsPreserved = false;
                        break;
                    }
                }
                if (allLevelsPreserved)
                {
                    var newCartesianProduct = CartesianProduct(newUniqueValues.Values.ToList());
                    var newMissing = GetMissingCombinations(copy, newCartesianProduct).Count;
                    if (newMissing <= originalMissing && copy.Any()) // Разрешаем удаление только если не теряются уровни
                    {
                        candidateDatasets.Add((copy, newMissing));
                    }
                }
            }

            if (!candidateDatasets.Any())
            {
                Console.WriteLine("No valid candidates for removal, returning original dataset.");
                return new List<FactorRow>(dataset);
            }

            // Анализ кандидатов
            var bestCandidates = new List<(List<FactorRow> Dataset, int MissingCount)>();
            int minDifference = int.MaxValue;
            int minMissing = int.MaxValue;

            foreach (var (candidate, missingCount) in candidateDatasets)
            {
                var newFreqDicts = new Dictionary<int, Dictionary<int, int>>();
                for (int col = 0; col < factorCount; col++)
                {
                    var freqDict = new Dictionary<int, int>();
                    foreach (var row in candidate)
                    {
                        int value = row.FactorValues[col];
                        freqDict[value] = freqDict.GetValueOrDefault(value, 0) + 1;
                    }
                    newFreqDicts[col] = freqDict;
                }

                // Вывод частот после удаления для отладки
                Console.WriteLine("\nCandidate Frequency per Column:");
                foreach (var kvp in newFreqDicts)
                {
                    Console.WriteLine($"Column {kvp.Key}: {string.Join(", ", kvp.Value.Select(kv => $"{kv.Key}:{kv.Value}"))}");
                }

                long cartesianSize = 1;
                foreach (var freqDict in newFreqDicts.Values)
                {
                    cartesianSize *= freqDict.Count;
                }

                int difference = (int)(cartesianSize - candidate.Count);

                if (difference < minDifference || (difference == minDifference && missingCount < minMissing))
                {
                    minDifference = difference;
                    minMissing = missingCount;
                    bestCandidates.Clear();
                    bestCandidates.Add((candidate, missingCount));
                }
                else if (difference == minDifference && missingCount == minMissing)
                {
                    bestCandidates.Add((candidate, missingCount));
                }
            }

            // Вывод удалённых строк для отладки
            if (bestCandidates.Any())
            {
                var removedRows = dataset.Except(bestCandidates[0].Dataset).ToList();
                Console.WriteLine("\nRemoved Rows:");
                foreach (var row in removedRows) Console.WriteLine(row);
            }

            if (minDifference == 0)
            {
                Console.WriteLine("Difference is 0, returning best candidate.");
                return new List<FactorRow>(bestCandidates[0].Dataset);
            }

            if (minDifference > 0)
            {
                var bestDataset = new List<FactorRow>(dataset);
                foreach (var (candidate, missingCount) in bestCandidates)
                {
                    var recursiveResult = CleanDatasetFrequencyBased(candidate, factorCount, cartesianProduct);
                    var recursiveFreqDicts = new Dictionary<int, Dictionary<int, int>>();
                    for (int col = 0; col < factorCount; col++)
                    {
                        var freqDict = new Dictionary<int, int>();
                        foreach (var row in recursiveResult)
                        {
                            int value = row.FactorValues[col];
                            freqDict[value] = freqDict.GetValueOrDefault(value, 0) + 1;
                        }
                        recursiveFreqDicts[col] = freqDict;
                    }
                    long recursiveCartesianSize = 1;
                    foreach (var freqDict in recursiveFreqDicts.Values)
                    {
                        recursiveCartesianSize *= freqDict.Count;
                    }
                    int recursiveDifference = (int)(recursiveCartesianSize - recursiveResult.Count);
                    if (recursiveDifference < minDifference)
                    {
                        minDifference = recursiveDifference;
                        bestDataset = recursiveResult;
                    }
                }
                return bestDataset;
            }

            return new List<FactorRow>(dataset);
        }

        // Получение списка отсутствующих комбинаций
        public List<string> GetMissingCombinations(List<FactorRow> dataset, List<FactorRow> cartesianProduct)
        {
            var present = new HashSet<string>(dataset.Select(row => string.Join("|", row.FactorValues.Take(row.FactorValues.Length - 1))));
            return cartesianProduct
                .Select(row => string.Join("|", row.FactorValues))
                .Where(c => !present.Contains(c))
                .ToList();
        }

        // Проверка полноты таблицы
        public bool IsFull(List<FactorRow> dataset, List<FactorRow> cartesianProduct)
        {
            var datasetCombinations = new HashSet<string>(
                dataset.Select(row => string.Join("|", row.FactorValues.Take(row.FactorValues.Length - 1))));
            var requiredCombinations = new HashSet<string>(
                cartesianProduct.Select(row => string.Join("|", row.FactorValues)));

            return requiredCombinations.All(comb => datasetCombinations.Contains(comb));
        }

        // Добавление случайного результата в каждую строку
        private List<FactorRow> AddRandomResults(List<FactorRow> dataset)
        {
            return dataset.Select(row =>
            {
                var newValues = row.FactorValues.ToList();
                newValues.Add((int)(random.NextDouble() * 100)); // Случайный результат (0–100)
                return new FactorRow(newValues.ToArray());
            }).ToList();
        }

        // Генерация случайного набора значений для фактора
        private HashSet<int> GenerateRandomSet(Random random, int maxValues)
        {
            int count = random.Next(1, maxValues + 1);
            return new HashSet<int>(Enumerable.Range(0, count).Select(_ => random.Next(1, 101)));
        }

        // Построение декартова произведения
        private List<FactorRow> CartesianProduct(List<HashSet<int>> sets)
        {
            if (sets == null || sets.Count == 0) return new List<FactorRow>();
            List<FactorRow> resultTuples = new List<FactorRow>();
            CartesianProductRecursive(sets, 0, new List<int>(), resultTuples);
            return resultTuples;
        }

        // Рекурсивный метод для построения декартова произведения
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

        // Сохранение таблицы в текстовый файл
        private void SaveDatasetToFile(List<FactorRow> dataset, string filePath, string[] factorColumns, string resultColumn)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine($"{string.Join("\t", factorColumns)}\t{resultColumn}");
            foreach (var row in dataset)
            {
                writer.WriteLine(row.ToString());
            }
        }
    }
}