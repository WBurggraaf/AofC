#nullable enable
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AccessiblePaperRolls
{
    // ======================= DOMAIN LAYER ====================================

    public readonly record struct Coordinates(int Row, int Col);

    public class Cell
    {
        public Coordinates Position { get; }
        public char Value { get; }
        public bool IsPaperRoll => Value == '@';

        public Cell(int row, int col, char value)
        {
            Position = new Coordinates(row, col);
            Value = value;
        }
    }

    public class Grid
    {
        private readonly List<List<Cell>> _rows;

        public int Height => _rows.Count;
        public int Width => _rows[0].Count;

        public Grid(string[] input)
        {
            if (input.Length == 0)
                throw new EmptyGridException("Grid contains no rows.");

            int expectedLen = input[0].Length;
            if (expectedLen == 0)
                throw new EmptyGridException("Grid has empty rows.");

            _rows = new();

            for (int r = 0; r < input.Length; r++)
            {
                if (input[r].Length != expectedLen)
                    throw new InconsistentRowLengthException($"Row {r + 1} has inconsistent length.");

                if (!input[r].All(ch => ch == '@' || ch == '.'))
                    throw new InvalidGridFormatException($"Row {r + 1} contains invalid characters.");

                var row = input[r]
                    .Select((ch, c) => new Cell(r, c, ch))
                    .ToList();

                _rows.Add(row);
            }
        }

        public Cell GetCell(int r, int c) => _rows[r][c];

        public bool InBounds(int r, int c) =>
            r >= 0 && r < Height &&
            c >= 0 && c < Width;
    }

    // ======================= DOMAIN EXCEPTIONS ================================

    public class EmptyGridException : Exception { public EmptyGridException(string msg) : base(msg) { } }
    public class InvalidGridFormatException : Exception { public InvalidGridFormatException(string msg) : base(msg) { } }
    public class InconsistentRowLengthException : Exception { public InconsistentRowLengthException(string msg) : base(msg) { } }

    // ======================= DOMAIN SERVICE ===================================

    public interface IAccessibilityService
    {
        int CountAccessibleRolls(Grid grid);
    }

    public class AccessibilityService : IAccessibilityService
    {
        private static readonly (int r, int c)[] Neighbors =
        {
            (-1,-1),(-1,0),(-1,1),
            (0,-1),       (0,1),
            (1,-1),(1,0),(1,1)
        };

        public int CountAccessibleRolls(Grid grid)
        {
            int result = 0;

            for (int r = 0; r < grid.Height; r++)
            {
                for (int c = 0; c < grid.Width; c++)
                {
                    var cell = grid.GetCell(r, c);
                    if (!cell.IsPaperRoll)
                        continue;

                    int adjacent = Neighbors.Count(dir =>
                    {
                        int nr = r + dir.r;
                        int nc = c + dir.c;
                        return grid.InBounds(nr, nc) && grid.GetCell(nr, nc).IsPaperRoll;
                    });

                    if (adjacent < 4)
                        result++;
                }
            }

            return result;
        }
    }

    // ======================= APPLICATION LAYER ================================

    public interface IUseCaseExecutor
    {
        void ExecuteFromFile(string path);
    }

    public class AccessibleRollsUseCase : IUseCaseExecutor
    {
        private readonly IAccessibilityService _accessibility;

        public AccessibleRollsUseCase(IAccessibilityService accessibility)
        {
            _accessibility = accessibility;
        }

        public void ExecuteFromFile(string path)
        {
            Console.WriteLine("=== Use Case Execution ===");

            try
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException($"input.txt not found in execution directory: {path}");

                Console.WriteLine($"Loading input.txt from: {path}");

                string[] rows = File.ReadAllLines(path)
                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                    .ToArray();

                Console.WriteLine("Parsing grid...");
                var grid = new Grid(rows);

                Console.WriteLine("Computing accessibility...");
                int result = _accessibility.CountAccessibleRolls(grid);

                Console.WriteLine($"Accessible rolls = {result}");
                Console.WriteLine("=== Use Case Completed Successfully ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine("=== ERROR ===");
                Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // ======================= ACTOR ===========================================

    public class ForkliftOptimizationActor
    {
        private readonly IUseCaseExecutor _executor;

        public ForkliftOptimizationActor(IUseCaseExecutor executor)
        {
            _executor = executor;
        }

        public void RunWithInputFile()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "input.txt");
            _executor.ExecuteFromFile(path);
        }
    }

    // ======================= PROGRAM ENTRY ===================================

    public class Program
    {
        public static void Main()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAccessibilityService, AccessibilityService>();
            services.AddSingleton<IUseCaseExecutor, AccessibleRollsUseCase>();
            services.AddSingleton<ForkliftOptimizationActor>();

            var provider = services.BuildServiceProvider();

            var actor = provider.GetRequiredService<ForkliftOptimizationActor>();

            actor.RunWithInputFile();
        }
    }
}
