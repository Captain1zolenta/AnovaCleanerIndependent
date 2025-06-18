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

            Console.WriteLine($"Full Table (Rows: {fullDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in fullDataset)
            {
                Console.WriteLine(row);
            }

            Console.WriteLine($"Reduced Table (Rows: {reducedDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in reducedDataset)
            {
                Console.WriteLine(row);
            }

            // Очистка таблицы.
            var cleanedDataset = CleanDataset(reducedDataset, factorColumns);

            Console.WriteLine($"Cleaned Table (Rows: {cleanedDataset.Count}):");
            Console.WriteLine($"{string.Join("\t", factorColumns)}\tResult");
            foreach (var row in cleanedDataset)
            {
                Console.WriteLine(row);
            }

            // Сохранение очищенной таблицы в файл, если она пуста — используем reducedDataset.
            if (cleanedDataset.Count == 0)
            {
                Console.WriteLine("Cleaned dataset is empty, saving reduced dataset instead.");
                SaveDatasetToFile(reducedDataset, outputPath, factorColumns, "Result");
            }
            else
            {
                SaveDatasetToFile(cleanedDataset, outputPath, factorColumns, "Result");
            }
            Console.WriteLine($"Table saved to {outputPath}");
        }

        // Метод очистки таблицы (алгоритм из диплома).
        public List<FactorRow> CleanDataset(List<FactorRow> dataset, string[] factorColumns)
        {
            // Шаг 1: Удаление дубликатов перед началом очистки.
            int initialCount = dataset.Count;
            dataset = dataset.GroupBy(row => string.Join(",", row.FactorValues.Take(factorColumns.Length)))
                             .Select(g => g.First()).ToList();
            int duplicatesRemoved = initialCount - dataset.Count;
            if (duplicatesRemoved > 0)
            {
                Console.WriteLine($"Removed {duplicatesRemoved} duplicate rows before cleaning.");
            }

            // Шаг 2: Извлечение уникальных значений факторов.
            var uniqueValues = new Dictionary<int, HashSet<int>>();
            for (int factorIndex = 0; factorIndex < factorColumns.Length; factorIndex++)
            {
                uniqueValues[factorIndex] = new HashSet<int>(dataset.Select(row => row.FactorValues[factorIndex]));
            }

            // Шаг 3: Построение декартова произведения.
            var cartesianProduct = CartesianProduct(uniqueValues.Values.ToList());

            // Шаг 4: Проверка полноты.
            if (IsFull(dataset, cartesianProduct))
            {
                Console.WriteLine("Dataset is already full. No cleaning needed.");
                GenerateReport(dataset, 0, 0.0, cartesianProduct);
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
        private List<FactorRow> CleanDatasetRecursive(
    List<FactorRow> dataset,
    List<FactorRow> cartesianProduct,
    int factorCount,
    Dictionary<int, HashSet<int>> uniqueValues,
    Dictionary<int, Dictionary<int, int>> frequencyCache,
    int depth,
    HashSet<string> previousState = null) // Добавлен параметр для отслеживания состояния
        {
            if (depth > 50)
            {
                Console.WriteLine("Превышен лимит глубины рекурсии, структура не достигнута.");
                GenerateReport(dataset, GetMissingCombinations(cartesianProduct, dataset, factorCount).Count, CalculateLoss(dataset), cartesianProduct);
                return dataset;
            }

            var missingCombinations = GetMissingCombinations(cartesianProduct, dataset, factorCount);
            int originalMissingCount = missingCombinations.Count;

            Console.WriteLine($"Глубина {depth}: Недостающие комбинации ({originalMissingCount}): {string.Join(", ", missingCombinations)}");

            if (missingCombinations.Count == 0)
            {
                Console.WriteLine("Таблица теперь полна после очистки.");
                GenerateReport(dataset, 0, 0.0, cartesianProduct);
                return dataset;
            }

            // Проверка повторяющегося состояния
            var currentState = string.Join("|", dataset.Select(r => string.Join(",", r.FactorValues)));
            if (previousState != null && previousState.Contains(currentState))
            {
                Console.WriteLine($"Глубина {depth}: Повторяющееся состояние, остановка рекурсии.");
                GenerateReport(dataset, missingCombinations.Count, CalculateLoss(dataset), cartesianProduct);
                return dataset;
            }

            var bestDataset = dataset;
            int bestMissingCount = originalMissingCount;
            double bestLoss = CalculateLoss(dataset);
            var candidates = new Dictionary<int, List<int>>();

            // Выбор кандидатов с учетом улучшения покрытия
            for (int factorIndex = 0; factorIndex < factorCount; factorIndex++)
            {
                var freq = frequencyCache[factorIndex];
                var rareValues = freq.Where(kvp => kvp.Value == freq.Values.Min())
                                    .Select(kvp => kvp.Key)
                                    .ToList();
                if (rareValues.Any())
                {
                    candidates[factorIndex] = rareValues;
                }
            }

            int maxRowsToRemove = dataset.Count / 4; // Ограничение до 25%
            int rowsRemoved = 0;
            double meanResult = dataset.Average(r => r.FactorValues.Last());
            double lossThreshold = meanResult * 0.75; // Увеличен порог до 75%
            Console.WriteLine($"Порог потерь: {lossThreshold:F2}");
            Console.WriteLine($"Кандидаты на удаление: {string.Join(", ", candidates.Select(kvp => $"{kvp.Key}:{string.Join(",", kvp.Value)}"))}");

            var alternatives = new List<(List<FactorRow> dataset, Dictionary<int, Dictionary<int, int>> cache)>();
            foreach (var factorIndex in candidates.Keys)
            {
                foreach (var rareValue in candidates[factorIndex])
                {
                    var rowsToRemoveCount = dataset.Count(row => row.FactorValues[factorIndex] == rareValue);
                    if (rowsRemoved + rowsToRemoveCount <= maxRowsToRemove)
                    {
                        var cleanedData = dataset.Where(row => row.FactorValues[factorIndex] != rareValue).ToList();
                        var newCache = CloneDictionary(frequencyCache);
                        foreach (var row in dataset.Where(r => r.FactorValues[factorIndex] == rareValue))
                        {
                            for (int i = 0; i < factorCount; i++)
                            {
                                if (newCache[i].ContainsKey(row.FactorValues[i]))
                                {
                                    newCache[i][row.FactorValues[i]]--;
                                }
                            }
                        }
                        var newUniqueValues = new Dictionary<int, HashSet<int>>();
                        for (int i = 0; i < factorCount; i++)
                        {
                            newUniqueValues[i] = new HashSet<int>(cleanedData.Select(row => row.FactorValues[i]));
                        }
                        var newCartesianProduct = CartesianProduct(newUniqueValues.Values.ToList());
                        var newMissingCount = GetMissingCombinations(newCartesianProduct, cleanedData, factorCount).Count;
                        double loss = CalculateLoss(cleanedData);
                        Console.WriteLine($"После удаления {rowsToRemoveCount} строк: Осталось {cleanedData.Count} строк, недостающих комбинаций: {newMissingCount}");
                        if (loss <= lossThreshold && (newMissingCount < originalMissingCount || cleanedData.Count < dataset.Count))
                        {
                            alternatives.Add((cleanedData, newCache));
                            rowsRemoved += rowsToRemoveCount;
                        }
                    }
                }
            }

            foreach (var (cleanedData, newCache) in alternatives)
            {
                double loss = CalculateLoss(cleanedData);
                if (loss > lossThreshold && GetMissingCombinations(cartesianProduct, cleanedData, factorCount).Count >= originalMissingCount - 1)
                {
                    continue;
                }

                var newMissing = GetMissingCombinations(cartesianProduct, cleanedData, factorCount).Count;
                if (newMissing < bestMissingCount || (newMissing == bestMissingCount && loss < bestLoss))
                {
                    bestDataset = cleanedData;
                    bestMissingCount = newMissing;
                    bestLoss = loss;
                }
            }

            if (bestMissingCount == 0)
            {
                Console.WriteLine($"Удалено {dataset.Count - bestDataset.Count} строк для достижения полной структуры.");
                GenerateReport(bestDataset, 0, bestLoss, cartesianProduct);
                return bestDataset;
            }

            if (bestMissingCount >= originalMissingCount - 1 && depth > 0)
            {
                Console.WriteLine($"Глубина {depth}: Нет значительного улучшения в недостающих комбинациях ({bestMissingCount} >= {originalMissingCount - 1}), остановка рекурсии.");
                GenerateReport(bestDataset, bestMissingCount, bestLoss, cartesianProduct);
                return bestDataset;
            }

            Console.WriteLine($"Глубина {depth}: Улучшено с {originalMissingCount} до {bestMissingCount} недостающих комбинаций, продолжаем.");
            var newPreviousState = previousState ?? new HashSet<string>();
            newPreviousState.Add(currentState);
            return CleanDatasetRecursive(bestDataset, cartesianProduct, factorCount, uniqueValues, frequencyCache, depth + 1, newPreviousState);
        }

        // Получение недостающих комбинаций.
        private HashSet<string> GetMissingCombinations(
            List<FactorRow> cartesianProduct,
            List<FactorRow> dataset,
            int factorCount)
        {
            var missingCombinations = new HashSet<string>(
                cartesianProduct.Select(r => string.Join("|", r.FactorValues)));

            var presentCombinations = new HashSet<string>(
                dataset.Select(r => string.Join("|", r.FactorValues.Take(factorCount))));

            missingCombinations.ExceptWith(presentCombinations);
            return missingCombinations;
        }

        // Проверка, является ли таблица полной (все комбинации присутствуют).
        public bool IsFull(List<FactorRow> dataset, List<FactorRow> cartesianProduct)
        {
            if (dataset.Count == 0) return false; // Пустой набор не считается полным
            var datasetCombinations = new HashSet<string>(
                dataset.Select(row => string.Join("|", row.FactorValues.Take(row.FactorValues.Length - 1)))); // Игнорируем результат
            var requiredCombinations = new HashSet<string>(
                cartesianProduct.Select(row => string.Join("|", row.FactorValues.Take(row.FactorValues.Length - 1)))); // Игнорируем результат
            return requiredCombinations.IsSubsetOf(datasetCombinations); // Проверяем, все ли комбинации покрыты
        }

        // Оценка потерь информации (сумма квадратов отклонений текущих результатов).
        private double CalculateLoss(List<FactorRow> dataset)
        {
            if (dataset.Count == 0 || dataset.Count == 1) return 0.0; // Нет потерь при 0 или 1 строке
            var results = dataset.Select(r => r.FactorValues.Last()).ToList();
            double mean = results.Average();
            return results.Sum(r => Math.Pow(r - mean, 2)) / (dataset.Count - 1); // Нормировка на количество степеней свободы
        }

        // Генерация отчета об очистке.
        private void GenerateReport(List<FactorRow> dataset, int missingCount, double loss, List<FactorRow> cartesianProduct)
        {
            Console.WriteLine($"Отчет об очистке:");
            Console.WriteLine($"Оставшиеся строки: {dataset.Count}");
            Console.WriteLine($"Недостающие комбинации: {missingCount}");
            Console.WriteLine($"Потери информации (сумма квадратов отклонений): {loss:F2}");
            Console.WriteLine($"Полная структура достигнута: {missingCount == 0}");
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
            if (sets == null || sets.Count == 0)
                return new List<FactorRow>();

            var resultTuples = new List<FactorRow>();
            CartesianProductRecursive(sets, 0, new List<int>(), resultTuples);
            return resultTuples;
        }

        // Рекурсивный метод построения декартова произведения.
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
            using var writer = new StreamWriter(filePath);
            writer.WriteLine($"{string.Join("\t", factorColumns)}\t{resultColumn}");
            foreach (var row in dataset)
            {
                writer.WriteLine(row.ToString());
            }
        }

        // Глубокое копирование словаря частот.
        private Dictionary<int, Dictionary<int, int>> CloneDictionary(Dictionary<int, Dictionary<int, int>> source)
        {
            var clone = new Dictionary<int, Dictionary<int, int>>();
            foreach (var kvp in source)
            {
                clone[kvp.Key] = new Dictionary<int, int>(kvp.Value);
            }
            return clone;
        }

        // Генерация случайной таблицы.
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
                                          .Take(rowsToRemove).ToList();

            List<FactorRow> reducedDataset = cartesianProduct
                .Where((x, i) => !indicesToRemove.Contains(i)).ToList();

            // Добавление случайных результатов.
            var fullDatasetWithResults = AddRandomResults(cartesianProduct);
            var reducedDatasetWithResults = AddRandomResults(reducedDataset);

            // Удаление дубликатов из уменьшенной таблицы.
            reducedDatasetWithResults = reducedDatasetWithResults
                .GroupBy(row => string.Join(",", row.FactorValues.Take(columnCount)))
                .Select(g => g.First()).ToList();

            Console.WriteLine($"Сгенерирована уменьшенная таблица с {reducedDatasetWithResults.Count} строками после удаления дубликатов.");
            return new List<List<FactorRow>> { fullDatasetWithResults, reducedDatasetWithResults };
        }
    }
}