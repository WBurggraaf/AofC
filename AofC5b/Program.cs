using System.Numerics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

public sealed class FreshRange
{
    public BigInteger Start { get; }
    public BigInteger End { get; }

    public FreshRange(BigInteger start, BigInteger end)
    {
        if (start > end) throw new ArgumentException("start > end");

        Start = start;
        End = end;
    }

    public bool OverlapsOrTouches(FreshRange other)
        => other.Start <= End + BigInteger.One;

    public FreshRange Merge(FreshRange other)
        => new FreshRange(
            BigInteger.Min(Start, other.Start),
            BigInteger.Max(End, other.End)
        );

    public BigInteger Count => (End - Start + BigInteger.One);

    public override string ToString() => $"{Start}-{End}";
}

public interface IRangeParserService
{
    IEnumerable<FreshRange> ParseRanges(string text, ILog log);
}

public sealed class RangeParserService : IRangeParserService
{
    private readonly Regex _pattern = new(@"^\s*(-?\d+)\s*-\s*(-?\d+)\s*$");

    public IEnumerable<FreshRange> ParseRanges(string text, ILog log)
    {
        var lines = text.Replace("\r", "").Split("\n");
        int blankIndex = Array.FindIndex(lines, l => l.Trim() == "");

        if (blankIndex == -1)
        {
            log.Error("ERROR: Missing blank line separation — cannot locate range block.");
            return Enumerable.Empty<FreshRange>();
        }

        var rangeLines = lines.Take(blankIndex);
        var result = new List<FreshRange>();

        foreach (var raw in rangeLines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var match = _pattern.Match(raw);
            if (!match.Success)
            {
                log.Warn($"WARNING: Invalid range '{raw}' — skipping.");
                continue;
            }

            BigInteger start, end;

            try
            {
                start = BigInteger.Parse(match.Groups[1].Value);
                end = BigInteger.Parse(match.Groups[2].Value);
            }
            catch
            {
                log.Warn($"WARNING: Could not parse numbers in '{raw}' — skipping.");
                continue;
            }

            if (start > end)
            {
                log.Warn($"WARNING: Range start > end for '{raw}' — skipping.");
                continue;
            }

            result.Add(new FreshRange(start, end));
        }

        if (result.Count == 0)
            log.Error("ERROR: No valid fresh ranges found — cannot compute fresh IDs.");

        return result;
    }
}

public interface IRangeMergerService
{
    IReadOnlyList<FreshRange> Merge(IEnumerable<FreshRange> ranges);
}

public sealed class RangeMergerService : IRangeMergerService
{
    public IReadOnlyList<FreshRange> Merge(IEnumerable<FreshRange> ranges)
    {
        var ordered = ranges.OrderBy(r => r.Start).ThenBy(r => r.End).ToList();
        if (ordered.Count == 0) return Array.Empty<FreshRange>();

        var merged = new List<FreshRange>();
        var current = ordered[0];

        for (int i = 1; i < ordered.Count; i++)
        {
            var next = ordered[i];
            if (current.OverlapsOrTouches(next))
            {
                current = current.Merge(next);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }
}

public interface IComputeFreshIdsUseCase
{
    BigInteger Execute(string filePath, ILog log);
}

public sealed class ComputeFreshIdsUseCase : IComputeFreshIdsUseCase
{
    private readonly IRangeParserService _parser;
    private readonly IRangeMergerService _merger;

    public ComputeFreshIdsUseCase(IRangeParserService parser, IRangeMergerService merger)
    {
        _parser = parser;
        _merger = merger;
    }

    public BigInteger Execute(string filePath, ILog log)
    {
        if (!File.Exists(filePath))
        {
            log.Error($"ERROR: File '{filePath}' not found.");
            return BigInteger.MinusOne;
        }

        string content;
        try
        {
            content = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            log.Error($"ERROR: Cannot read file — {ex.Message}");
            return BigInteger.MinusOne;
        }

        var ranges = _parser.ParseRanges(content, log).ToList();
        if (ranges.Count == 0)
            return BigInteger.MinusOne;

        log.Info("Sorted ranges:");
        foreach (var r in ranges.OrderBy(r => r.Start))
            log.Info($"  {r}");

        var merged = _merger.Merge(ranges);

        log.Info("Merged ranges:");
        foreach (var r in merged)
            log.Info($"  {r}");

        BigInteger total = merged.Aggregate(BigInteger.Zero, (sum, r) => sum + r.Count);

        log.Success($"The fresh ingredient ID ranges cover {total} distinct ingredient IDs.");
        log.Success("Use case completed successfully.");

        return total;
    }
}

public interface ILog
{
    void Info(string msg);
    void Warn(string msg);
    void Error(string msg);
    void Success(string msg);
}

public sealed class ConsoleLog : ILog
{
    public void Info(string msg) => Console.WriteLine(msg);
    public void Warn(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(msg);
        Console.ResetColor();
    }
    public void Error(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(msg);
        Console.ResetColor();
    }
    public void Success(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);
        Console.ResetColor();
    }
}

public class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IRangeParserService, RangeParserService>();
        services.AddSingleton<IRangeMergerService, RangeMergerService>();
        services.AddSingleton<IComputeFreshIdsUseCase, ComputeFreshIdsUseCase>();
        services.AddSingleton<ILog, ConsoleLog>();

        var provider = services.BuildServiceProvider();

        var useCase = provider.GetRequiredService<IComputeFreshIdsUseCase>();
        var log = provider.GetRequiredService<ILog>();

        const string fileName = "input.txt";

        BigInteger result = useCase.Execute(fileName, log);

        Console.WriteLine();
        Console.WriteLine("-----------------------------------------------");
        Console.WriteLine($"FINAL ANSWER: {result}");
        Console.WriteLine("-----------------------------------------------");

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
