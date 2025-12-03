#region Usings
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
#endregion


#region Domain

#region Value Objects
public readonly struct ProductId
{
    public BigInteger Value { get; }
    public ProductId(BigInteger value) => Value = value;

    public override string ToString() => Value.ToString();
}
#endregion

#region Domain Model
public readonly struct IdRange
{
    public BigInteger Start { get; }
    public BigInteger End { get; }
    public IdRange(BigInteger start, BigInteger end)
    {
        Start = start;
        End = end;
    }
}
#endregion

#region Specification
public interface IInvalidIdSpecification
{
    bool IsInvalid(ProductId id);
}

public sealed class RepeatedSequenceInvalidIdSpecification : IInvalidIdSpecification
{
    public bool IsInvalid(ProductId id)
    {
        var s = id.Value.ToString();

        // Leading zeros forbidden
        if (s.Length > 1 && s[0] == '0')
            return false;

        int len = s.Length;

        // Try all possible sequence lengths
        for (int seqLen = 1; seqLen <= len / 2; seqLen++)
        {
            if (len % seqLen != 0) continue;

            string unit = s.Substring(0, seqLen);
            int repeatCount = len / seqLen;

            if (repeatCount < 2) continue;

            string rebuilt = string.Concat(Enumerable.Repeat(unit, repeatCount));

            if (rebuilt == s)
                return true;
        }

        return false;
    }
}
#endregion

#endregion

#region Infrastructure

#region Range Parser
public interface IRangeParser
{
    IEnumerable<IdRange> Parse(string raw, IList<string> errors);
}

public sealed class RangeParser : IRangeParser
{
    public IEnumerable<IdRange> Parse(string raw, IList<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        var segments = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var parts = segment.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                errors.Add($"Malformed range: '{segment}'");
                continue;
            }

            if (!BigInteger.TryParse(parts[0], out var start) ||
                !BigInteger.TryParse(parts[1], out var end))
            {
                errors.Add($"Non-numeric range: '{segment}'");
                continue;
            }

            if (start > end)
            {
                errors.Add($"Range start > end: '{segment}'");
                continue;
            }

            yield return new IdRange(start, end);
        }
    }
}
#endregion

#endregion

#region Application Layer (Use Case)

public interface IInvalidProductIdFinder
{
    (IReadOnlyCollection<ProductId> InvalidIds, IReadOnlyList<string> Errors)
        Execute(string rawRanges);
}

public sealed class InvalidProductIdFinder : IInvalidProductIdFinder
{
    private readonly IRangeParser _parser;
    private readonly IInvalidIdSpecification _spec;

    public InvalidProductIdFinder(IRangeParser parser, IInvalidIdSpecification spec)
    {
        _parser = parser;
        _spec = spec;
    }

    public (IReadOnlyCollection<ProductId> InvalidIds, IReadOnlyList<string> Errors)
        Execute(string rawRanges)
    {
        var errors = new List<string>();
        var invalid = new HashSet<ProductId>();

        var ranges = _parser.Parse(rawRanges, errors);

        foreach (var range in ranges)
        {
            for (BigInteger id = range.Start; id <= range.End; id++)
            {
                var pid = new ProductId(id);
                if (_spec.IsInvalid(pid))
                    invalid.Add(pid);

                if (id == BigInteger.One * long.MaxValue) break; // safety
            }
        }

        return (invalid.OrderBy(i => i.Value).ToList(), errors);
    }
}
#endregion

#region Composition Root
internal static class Program
{
    static void Main()
    {
        Console.WriteLine("Gift Shop — Identify Invalid Product IDs (Part 2 Rules)");
        Console.WriteLine("Enter ID ranges (comma-separated):");

        var input = Console.ReadLine() ?? "";

        // DI container
        var services = new ServiceCollection()
            .AddSingleton<IRangeParser, RangeParser>()
            .AddSingleton<IInvalidIdSpecification, RepeatedSequenceInvalidIdSpecification>()
            .AddSingleton<IInvalidProductIdFinder, InvalidProductIdFinder>()
            .BuildServiceProvider();

        var finder = services.GetRequiredService<IInvalidProductIdFinder>();

        var (invalidIds, errors) = finder.Execute(input);

        Console.WriteLine("\n--- Errors ---");
        if (errors.Count == 0)
            Console.WriteLine("None");
        else
            errors.ToList().ForEach(e => Console.WriteLine("• " + e));

        Console.WriteLine("\n--- Invalid IDs ---");
        if (invalidIds.Count == 0)
            Console.WriteLine("<none>");
        else
            Console.WriteLine(string.Join(", ", invalidIds.Select(i => i.Value)));

        BigInteger sum = invalidIds.Aggregate(BigInteger.Zero, (acc, id) => acc + id.Value);

        Console.WriteLine("\n--- Sum of Invalid IDs ---");
        Console.WriteLine(sum);

        Console.WriteLine("\nDone.");
    }
}
#endregion
