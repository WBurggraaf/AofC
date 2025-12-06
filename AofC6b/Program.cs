using System.Numerics;
using Microsoft.Extensions.DependencyInjection;

public sealed class Player
{
    public string Name { get; }
    public Player(string name) => Name = name;
}

public sealed record ColumnGroup(IReadOnlyList<int> Columns);

public interface ICepMathSolver
{
    BigInteger SolveWorksheet(string[] lines);
}

public sealed class CephalopodMathSolver : ICepMathSolver
{
    public BigInteger SolveWorksheet(string[] lines)
    {
        if (lines.Length < 2)
            throw new InvalidOperationException("Input must contain at least one digit row and one operator row.");

        // Normalize width
        int width = lines.Max(l => l.Length);
        string[] padded = lines.Select(l => l.PadRight(width, ' ')).ToArray();

        string operatorRow = padded[^1];
        string[] digitRows = padded[..^1];

        // Parse column groups (right → left)
        var groups = DetectColumnGroups(digitRows, operatorRow, width);

        // Evaluate each group
        BigInteger grandTotal = BigInteger.Zero;

        foreach (var group in groups)
        {
            var numbers = ExtractNumbers(digitRows, group);
            if (numbers.Count == 0)
            {
                Console.WriteLine("[WARN] Group had no valid numbers → skipped");
                continue;
            }

            char? op = DetectOperator(group, operatorRow);
            if (op is null)
            {
                Console.WriteLine("[ERR] Missing operator in group → skipped");
                continue;
            }

            BigInteger result = Evaluate(numbers, op.Value);
            grandTotal += result;
        }

        return grandTotal;
    }

    private static IReadOnlyList<ColumnGroup> DetectColumnGroups(string[] digitRows, string opRow, int width)
    {
        var groups = new List<ColumnGroup>();
        var current = new List<int>();

        for (int col = width - 1; col >= 0; col--)
        {
            bool empty =
                opRow[col] == ' ' &&
                digitRows.All(r => r[col] == ' ');

            if (empty)
            {
                if (current.Count > 0)
                {
                    current.Reverse();
                    groups.Add(new ColumnGroup(current.ToArray()));
                    current.Clear();
                }
            }
            else
            {
                current.Add(col);
            }
        }

        if (current.Count > 0)
        {
            current.Reverse();
            groups.Add(new ColumnGroup(current.ToArray()));
        }

        return groups;
    }

    private static List<BigInteger> ExtractNumbers(string[] digitRows, ColumnGroup group)
    {
        var numbers = new List<BigInteger>();

        foreach (int col in group.Columns)
        {
            Span<char> buffer = stackalloc char[digitRows.Length];
            int count = 0;

            for (int r = 0; r < digitRows.Length; r++)
            {
                char ch = digitRows[r][col];
                if (char.IsDigit(ch))
                    buffer[count++] = ch;
            }

            if (count == 0)
                continue;

            try
            {
                var num = BigInteger.Parse(buffer[..count]);
                numbers.Add(num);
            }
            catch
            {
                Console.WriteLine($"[ERR] Failed BigInt parse at column {col}");
            }
        }

        return numbers;
    }

    private static char? DetectOperator(ColumnGroup group, string opRow)
    {
        foreach (int col in group.Columns)
        {
            char c = opRow[col];
            if (c == '+' || c == '*')
                return c;
        }
        return null;
    }

    private static BigInteger Evaluate(List<BigInteger> nums, char op)
    {
        BigInteger res = nums[0];

        for (int i = 1; i < nums.Count; i++)
        {
            if (op == '+')
                res += nums[i];
            else
                res *= nums[i];
        }

        return res;
    }
}

public sealed class WorksheetRunner
{
    private readonly Player _player;
    private readonly ICepMathSolver _solver;

    public WorksheetRunner(Player player, ICepMathSolver solver)
    {
        _player = player;
        _solver = solver;
    }

    public void Run()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "input.txt");

        if (!File.Exists(path))
        {
            Console.WriteLine($"ERROR: Cannot find input.txt at {path}");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        Console.WriteLine($"Loaded {lines.Length} lines of worksheet.");

        BigInteger total = _solver.SolveWorksheet(lines);

        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine($"Player:          {_player.Name}");
        Console.WriteLine($"Final Grand Total (BigInteger): {total}");
        Console.WriteLine("==================================================");
    }
}

public class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new Player("WBurggraaf"));
        services.AddSingleton<ICepMathSolver, CephalopodMathSolver>();
        services.AddTransient<WorksheetRunner>();

        var provider = services.BuildServiceProvider();

        var runner = provider.GetRequiredService<WorksheetRunner>();
        runner.Run();
    }
}
