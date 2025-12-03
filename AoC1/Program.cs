// Program.cs
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

#region Interfaces // IoC abstractions
public interface IRangeParser
{
    IEnumerable<(int Start, int End)> ParseRanges(string input, IList<string> errors);
}

public interface IInvalidIdDetector
{
    bool IsInvalid(string idStr);
}

public interface IIdProcessor
{
    (IEnumerable<int> InvalidIds, IEnumerable<string> Errors) Process(string rangeInput);
}
#endregion

#region Implementations

// Implements: Use Case Steps 4.1 + Error Handling 1
public class RangeParser : IRangeParser
{
    public IEnumerable<(int Start, int End)> ParseRanges(string input, IList<string> errors)
    {
        var segments = input.Split(',');

        foreach (var rawSeg in segments)
        {
            string seg = rawSeg.Trim();
            if (string.IsNullOrWhiteSpace(seg))
                continue; // Use Case: Errors & Issues (trailing/leading commas)

            if (!seg.Contains('-'))
            {
                errors.Add($"Malformed range (missing dash): '{seg}'");
                continue;
            }

            var parts = seg.Split('-');
            if (parts.Length != 2)
            {
                errors.Add($"Malformed range: '{seg}'");
                continue;
            }

            string startStr = parts[0].Trim();
            string endStr = parts[1].Trim();

            if (!int.TryParse(startStr, out int start) || !int.TryParse(endStr, out int end))
            {
                errors.Add($"Malformed range (non-numeric): '{seg}'");
                continue;
            }

            if (start > end)
            {
                errors.Add($"Invalid range (start > end): '{seg}'"); // Errors & Issues 2
                continue;
            }

            yield return (start, end);
        }
    }
}

// Implements: Use Case Step 4.3 — Pattern Definition
public class InvalidIdDetector : IInvalidIdDetector
{
    public bool IsInvalid(string idStr)
    {
        // Use Case 4.3c & Leading zero rule
        if (idStr.Length > 1 && idStr[0] == '0') return false;

        // Use Case 4.3b — Odd length cannot be invalid
        if (idStr.Length % 2 != 0) return false;

        int half = idStr.Length / 2;
        string left = idStr[..half];
        string right = idStr[half..];
        return left == right; // Must match exactly twice
    }
}

// Implements: Steps 4–7 of Main Success Scenario
public class IdProcessor : IIdProcessor
{
    private readonly IRangeParser _parser;
    private readonly IInvalidIdDetector _detector;

    public IdProcessor(IRangeParser parser, IInvalidIdDetector detector)
    {
        _parser = parser;
        _detector = detector;
    }

    public (IEnumerable<int> InvalidIds, IEnumerable<string> Errors) Process(string rangeInput)
    {
        var errors = new List<string>();
        var invalidSet = new HashSet<int>(); // Deduplication (Errors & Issues 5)

        foreach (var (start, end) in _parser.ParseRanges(rangeInput, errors))
        {
            for (int id = start; id <= end; id++)
            {
                string idStr = id.ToString();
                if (_detector.IsInvalid(idStr))
                    invalidSet.Add(id);
            }
        }

        return (invalidSet.OrderBy(x => x), errors);
    }
}

#endregion

#region Composition Root
// IoC Container (minimal for single-file demo)
public static class IoC
{
    public static IIdProcessor BuildProcessor() =>
        new IdProcessor(new RangeParser(), new InvalidIdDetector());
}
#endregion

#region Program Entry Point

class Program
{
    static void Main()
    {
        // Build IoC container using Microsoft.Extensions.DependencyInjection
        var services = new ServiceCollection();
        services.AddSingleton<IRangeParser, RangeParser>();
        services.AddSingleton<IInvalidIdDetector, InvalidIdDetector>();
        services.AddSingleton<IIdProcessor, IdProcessor>();

        var provider = services.BuildServiceProvider();

        Console.WriteLine("North Pole Gift Shop — Invalid Product ID Finder");
        Console.WriteLine("Enter ID ranges (comma-separated, e.g. 10-200, 3000-3500):");

        string input = Console.ReadLine() ?? string.Empty;

        // Resolve processor
        var processor = provider.GetRequiredService<IIdProcessor>();
        var (invalidIds, errors) = processor.Process(input);

        Console.WriteLine("\nErrors:");
        foreach (var err in errors)
            Console.WriteLine(" - " + err);

        Console.WriteLine("\nInvalid IDs:");
        Console.WriteLine(string.Join(", ", invalidIds));

        System.Numerics.BigInteger total = invalidIds
            .Select(id => new System.Numerics.BigInteger(id))
            .Aggregate(new System.Numerics.BigInteger(0), (a, b) => a + b);

        Console.WriteLine($"\nTotal Sum: {total}");

        Console.WriteLine("\nProcess complete. The clerk may now clean the database.");
        Console.WriteLine("You may proceed to the lobby.");
    }
}
#endregion
