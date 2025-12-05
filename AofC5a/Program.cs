using Microsoft.Extensions.DependencyInjection;

public sealed class IngredientId
{
    public long Value { get; }
    public IngredientId(long value) => Value = value;
}

public sealed class FreshRange
{
    public long Start { get; }
    public long End { get; }

    public FreshRange(long start, long end)
    {
        Start = start;
        End = end;
    }

    public bool Contains(long id) => id >= Start && id <= End;
}

public interface IIngredientClassifier
{
    ClassificationResult Classify(
        IReadOnlyList<FreshRange> ranges,
        IReadOnlyList<IngredientId> ids);
}

public sealed class ClassificationResult
{
    public IReadOnlyList<(IngredientId Id, bool IsFresh)> Items { get; }
    public int FreshCount => Items.Count(x => x.IsFresh);

    public ClassificationResult(IReadOnlyList<(IngredientId, bool)> items)
    {
        Items = items;
    }
}

public sealed class IngredientClassifier : IIngredientClassifier
{
    public ClassificationResult Classify(
        IReadOnlyList<FreshRange> ranges,
        IReadOnlyList<IngredientId> ids)
    {
        var classified = new List<(IngredientId, bool)>();

        foreach (var id in ids)
        {
            bool isFresh = ranges.Any(r => r.Contains(id.Value));
            classified.Add((id, isFresh));
        }

        return new ClassificationResult(classified);
    }
}

public sealed class KitchenInventorySystem
{
    private readonly IIngredientClassifier _classifier;

    public KitchenInventorySystem(IIngredientClassifier classifier)
    {
        _classifier = classifier;
    }

    public void RunUseCase(string inputPath)
    {
        Console.WriteLine("=== Use Case: Determine Fresh Ingredient IDs ===");

        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"ERROR: File '{inputPath}' not found.");
            return;
        }

        Console.WriteLine("Step 1: Elf provides input database file.");

        string raw = File.ReadAllText(inputPath);

        if (string.IsNullOrWhiteSpace(raw))
        {
            Console.WriteLine("ERROR: File is empty.");
            return;
        }

        Console.WriteLine("Step 2: System reads and splits ranges/IDs.");

        var sections = raw.Split(new string[] { "\n\n", "\r\n\r\n" },
            StringSplitOptions.RemoveEmptyEntries);

        if (sections.Length < 2)
        {
            Console.WriteLine("ERROR: Missing blank line separator between ranges and ingredient IDs.");
            return;
        }

        var rangeLines = sections[0]
            .Split('\n', '\r')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var idLines = sections[1]
            .Split('\n', '\r')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var ranges = new List<FreshRange>();
        var ids = new List<IngredientId>();

        Console.WriteLine("Step 3: Validating ranges...");

        // FIXED: now using long.TryParse instead of int.TryParse!
        foreach (var line in rangeLines)
        {
            var parts = line.Split('-');
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], out long start) ||
                !long.TryParse(parts[1], out long end))
            {
                Console.WriteLine($"WARNING: Invalid range '{line}', skipping.");
                continue;
            }

            if (start > end)
            {
                Console.WriteLine($"WARNING: Range start > end in '{line}', skipping.");
                continue;
            }

            ranges.Add(new FreshRange(start, end));
        }

        if (ranges.Count == 0)
        {
            Console.WriteLine("ERROR: No valid ranges remain. Cannot classify.");
            return;
        }

        Console.WriteLine($"Validated {ranges.Count} ranges.");

        Console.WriteLine("Step 4: Validating ingredient IDs...");

        foreach (var line in idLines)
        {
            if (!long.TryParse(line, out long val))
            {
                Console.WriteLine($"WARNING: Invalid ingredient ID '{line}', skipping.");
                continue;
            }

            ids.Add(new IngredientId(val));
        }

        if (ids.Count == 0)
        {
            Console.WriteLine("ERROR: No ingredient IDs to classify.");
            return;
        }

        Console.WriteLine($"Loaded {ids.Count} ingredient IDs.");

        Console.WriteLine("Step 5: Classifying IDs against ranges...");

        var result = _classifier.Classify(ranges, ids);

        Console.WriteLine("Step 6: Outputting results...");
        Console.WriteLine();
        Console.WriteLine($"Fresh ingredients: {result.FreshCount}");
        Console.WriteLine();

        foreach (var item in result.Items)
        {
            Console.WriteLine($"{item.Id.Value}: {(item.IsFresh ? "Fresh" : "Spoiled")}");
        }

        Console.WriteLine();
        Console.WriteLine("Use Case completed.");
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IIngredientClassifier, IngredientClassifier>();
        services.AddSingleton<KitchenInventorySystem>();

        using var provider = services.BuildServiceProvider();

        var system = provider.GetRequiredService<KitchenInventorySystem>();

        string inputPath = Path.Combine(Directory.GetCurrentDirectory(), "input.txt");

        system.RunUseCase(inputPath);
    }
}
