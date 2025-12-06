using System.Numerics;
using Microsoft.Extensions.DependencyInjection;


public class Player
{
    public string Name { get; }
    public Player(string name) => Name = name;

    public void PresentResult(BigInteger total)
    {
        Console.WriteLine($"Grand Total = {total}");
    }
}

public class SolverSystem
{
    private readonly IWorksheetSolver _solver;
    private readonly Player _player;

    public SolverSystem(IWorksheetSolver solver, Player player)
    {
        _solver = solver;
        _player = player;
    }

    public void Execute(string worksheetPath)
    {
        var total = _solver.SolveWorksheet(worksheetPath);
        _player.PresentResult(total);
    }
}

public class Worksheet
{
    public IReadOnlyList<string> Lines { get; }
    public Worksheet(IEnumerable<string> lines) => Lines = lines.ToList();
}

public class ProblemColumn
{
    public int Start { get; }
    public int End { get; }
    public ProblemColumn(int start, int end)
    {
        Start = start;
        End = end;
    }
}

public class ParsedProblem
{
    public List<BigInteger> Operands { get; } = new();
    public char Operator { get; set; }
}
public interface IWorksheetParser
{
    Worksheet LoadWorksheet(string path);
    List<ProblemColumn> IdentifyProblems(Worksheet sheet);
    ParsedProblem ExtractProblem(Worksheet sheet, ProblemColumn colGroup);
}

public interface IProblemEvaluator
{
    BigInteger Evaluate(ParsedProblem problem);
}

public interface IWorksheetSolver
{
    BigInteger SolveWorksheet(string path);
}
public class WorksheetParser : IWorksheetParser
{
    public Worksheet LoadWorksheet(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Worksheet file not found: {path}");

        var lines = File.ReadAllLines(path).ToList();

        // Normalize widths
        int maxWidth = lines.Max(l => l.Length);
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length < maxWidth)
                lines[i] = lines[i].PadRight(maxWidth, ' ');
        }

        return new Worksheet(lines);
    }

    public List<ProblemColumn> IdentifyProblems(Worksheet sheet)
    {
        int width = sheet.Lines[0].Length;
        var columns = new List<ProblemColumn>();

        bool IsSeparatorColumn(int col)
        {
            return sheet.Lines.All(row => row[col] == ' ');
        }

        // Identify contiguous problem column regions
        int currentStart = -1;

        for (int col = 0; col < width; col++)
        {
            bool sep = IsSeparatorColumn(col);

            if (!sep && currentStart == -1)
            {
                currentStart = col;
            }
            else if (sep && currentStart != -1)
            {
                columns.Add(new ProblemColumn(currentStart, col - 1));
                currentStart = -1;
            }
        }

        if (currentStart != -1)
            columns.Add(new ProblemColumn(currentStart, width - 1));

        return columns;
    }

    public ParsedProblem ExtractProblem(Worksheet sheet, ProblemColumn colGroup)
    {
        var problem = new ParsedProblem();

        int rowCount = sheet.Lines.Count;
        int operatorRow = rowCount - 1;

        // Extract operands (all rows except last)
        for (int r = 0; r < operatorRow; r++)
        {
            string slice = Slice(sheet.Lines[r], colGroup.Start, colGroup.End).Trim();

            if (slice.Length == 0)
                continue;

            if (!BigInteger.TryParse(slice, out BigInteger value))
                throw new Exception($"Invalid operand at row {r} col {colGroup.Start}-{colGroup.End}: '{slice}'");

            problem.Operands.Add(value);
        }

        // Extract operator
        string opSlice = Slice(sheet.Lines[operatorRow], colGroup.Start, colGroup.End).Trim();
        if (opSlice.Length == 0)
            throw new Exception($"Missing operator in problem at col {colGroup.Start}-{colGroup.End}");

        char op = opSlice.First(c => c == '+' || c == '*');
        problem.Operator = op;

        return problem;
    }

    private string Slice(string line, int start, int end)
    {
        if (start >= line.Length) return "";
        int length = Math.Min(end - start + 1, line.Length - start);
        return line.Substring(start, length);
    }
}

public class ProblemEvaluator : IProblemEvaluator
{
    public BigInteger Evaluate(ParsedProblem problem)
    {
        if (problem.Operands.Count == 0)
            return 0;

        return problem.Operator switch
        {
            '+' => problem.Operands.Aggregate(BigInteger.Zero, (acc, v) => acc + v),
            '*' => problem.Operands.Aggregate(BigInteger.One, (acc, v) => acc * v),
            _ => throw new Exception($"Unknown operator {problem.Operator}")
        };
    }
}

public class WorksheetSolver : IWorksheetSolver
{
    private readonly IWorksheetParser _parser;
    private readonly IProblemEvaluator _evaluator;

    public WorksheetSolver(IWorksheetParser parser, IProblemEvaluator evaluator)
    {
        _parser = parser;
        _evaluator = evaluator;
    }

    public BigInteger SolveWorksheet(string path)
    {
        var sheet = _parser.LoadWorksheet(path);
        var groups = _parser.IdentifyProblems(sheet);

        BigInteger total = 0;

        foreach (var g in groups)
        {
            var parsed = _parser.ExtractProblem(sheet, g);
            var result = _evaluator.Evaluate(parsed);
            total += result;
        }

        return total;
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        string inputPath = Path.Combine(Directory.GetCurrentDirectory(), "input.txt");

        var services = new ServiceCollection()
            .AddSingleton<IWorksheetParser, WorksheetParser>()
            .AddSingleton<IProblemEvaluator, ProblemEvaluator>()
            .AddSingleton<IWorksheetSolver, WorksheetSolver>()
            .AddSingleton(new Player("WBurggraaf"))
            .AddSingleton<SolverSystem>()
            .BuildServiceProvider();

        var system = services.GetRequiredService<SolverSystem>();
        system.Execute(inputPath);
    }
}
