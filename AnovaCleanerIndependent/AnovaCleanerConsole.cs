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

        // Метод очистки таблицы (обёртка для совместимости с тестами)
        public List<FactorRow> CleanDataset(List<FactorRow> dataset, string[] factorColumns)
        {
            // Извлечение уникальных значений факторов.
            var uniqueValues = new Dictionary<int, HashSet<int>>();
            for (int factorIndex = 0; factorIndex < factorColumns.Length; factorIndex++)
            {
                uniqueValues[factorIndex] = new HashSet<int>(
                    dataset.Select(row => row.FactorValues[factorIndex]));
            }

            // Построение полного декартова произведения.
            var cartesianProduct = CartesianProduct(uniqueValues.Values.ToList());
            var cartesianProductWithResults = AddRandomResults(cartesianProduct);

            // Вызов метода обратного жадного отбора.
            return CleanDatasetBackward(dataset, cartesianProductWithResults, factorColumns.Length);
        }

        // Очистка таблицы с использованием обратного жадного отбора
        private List<FactorRow> CleanDatasetBackward(List<FactorRow> dataset, List<FactorRow> cartesianProduct, int factorCount)
        {
            var currentDataset = new List<FactorRow>(dataset);
            var bestDataset = new List<FactorRow>(currentDataset); // Сохраняем лучший набор
            int minMissingCount = GetMissingCombinations(currentDataset, cartesianProduct).Count;

            if (minMissingCount == 0) return currentDataset; // Если таблица уже полная, возвращаем её

            for (int i = 0; i < 500; i++) // Удаляем строки до достижения лимита
            {
                if (currentDataset.Count <= 1) break; // Прерываем, если осталась 1 строка

                var bestRow = currentDataset.Count > 0 ? currentDataset[0] : null;
                int minNewMissing = int.MaxValue;

                // Текущие покрытые комбинации
                var currentCombinations = new HashSet<string>(
                    currentDataset.Select(row => string.Join("|", row.FactorValues.Take(factorCount))));

                // Поиск строки для удаления
                foreach (var row in currentDataset)
                {
                    var tempDataset = currentDataset.Where(r => !r.Equals(row)).ToList();
                    if (tempDataset.Count == 0) continue; // Пропускаем, если удаление оставит пустой набор

                    var newCombinations = new HashSet<string>(
                        tempDataset.Select(r => string.Join("|", r.FactorValues.Take(factorCount))));
                    int newMissingCount = GetMissingCombinations(tempDataset, cartesianProduct).Count;

                    // Выбираем строку, чьё удаление минимизирует пропуски
                    if (newMissingCount < minNewMissing)
                    {
                        minNewMissing = newMissingCount;
                        bestRow = row;
                    }
                }

                if (bestRow == null) break; // Если нет подходящей строки для удаления, прерываем

                currentDataset.Remove(bestRow);
                Console.WriteLine($"Глубина {i}: Удалена строка, оставшиеся пропуски: {minNewMissing}");

                // Обновляем лучший набор, если текущий имеет меньше пропусков
                int currentMissingCount = GetMissingCombinations(currentDataset, cartesianProduct).Count;
                if (currentMissingCount < minMissingCount)
                {
                    minMissingCount = currentMissingCount;
                    bestDataset = new List<FactorRow>(currentDataset);
                }

                // Проверка полноты
                if (IsFull(currentDataset, cartesianProduct))
                {
                    Console.WriteLine("Таблица стала полной после удаления.");
                    return currentDataset;
                }
            }

            Console.WriteLine($"Лучшее количество пропусков: {minMissingCount}");
            return bestDataset; // Возвращаем набор с минимальным числом пропусков
        }

        // Получение списка отсутствующих комбинаций
        private List<string> GetMissingCombinations(List<FactorRow> dataset, List<FactorRow> cartesianProduct)
        {
            var present = new HashSet<string>(dataset.Select(row => string.Join("|", row.FactorValues.Take(row.FactorValues.Length - 1))));
            return cartesianProduct
                .Select(row => string.Join("|", row.FactorValues))
                .Where(c => !present.Contains(c))
                .ToList();
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