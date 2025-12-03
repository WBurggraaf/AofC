using System.Numerics;
using Microsoft.Extensions.DependencyInjection;


namespace GiftShop.InvalidProductIds
{
    #region Domain
    public interface IRangeParser
    {
        IEnumerable<(long Start, long End)> ParseRanges(string input, IList<string> errors);
    }

    public interface IInvalidIdSpecification
    {
        bool IsInvalidId(long id);
    }

    public interface IInvalidIdFinder
    {
        (IReadOnlyCollection<long> InvalidIds, IReadOnlyCollection<string> Errors)
            FindInvalidIds(string rangeInput);
    }
    #endregion

    #region DomainImpl

    public sealed class RangeParser : IRangeParser
    {
        public IEnumerable<(long Start, long End)> ParseRanges(string input, IList<string> errors)
        {
            if (string.IsNullOrWhiteSpace(input))
                yield break;

            // Use Case: "All ranges appear on one line, separated by commas."
            var segments = input.Split(',');

            foreach (var rawSegment in segments)
            {
                var segment = rawSegment.Trim();

                // Errors & Issues 1: trailing/leading commas or blanks
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                if (!segment.Contains('-'))
                {
                    // Errors & Issues 1: malformed range (missing dash)
                    errors.Add($"Malformed range (missing dash): '{segment}'");
                    continue;
                }

                var parts = segment.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    errors.Add($"Malformed range: '{segment}'");
                    continue;
                }

                var startStr = parts[0].Trim();
                var endStr = parts[1].Trim();

                // Use long to correctly handle large ID values (Errors & Issues 2: data integrity).
                if (!long.TryParse(startStr, out var start) ||
                    !long.TryParse(endStr, out var end))
                {
                    errors.Add($"Malformed range (non-numeric or out-of-range): '{segment}'");
                    continue;
                }

                // Errors & Issues 2: Out-of-order ranges
                if (start > end)
                {
                    errors.Add($"Invalid range (start > end): '{segment}'");
                    continue;
                }

                yield return (start, end);
            }
        }
    }

    public sealed class RepeatedTwiceInvalidIdSpecification : IInvalidIdSpecification
    {
        public bool IsInvalidId(long id)
        {
            var text = id.ToString();

            // 4.3c — IDs with leading zeros are not considered IDs at all.
            if (text.Length > 1 && text[0] == '0')
                return false;

            // 4.3b — odd-length numbers cannot be formed by repeating a sequence twice.
            if (text.Length % 2 != 0)
                return false;

            var half = text.Length / 2;
            var left = text.Substring(0, half);
            var right = text.Substring(half);

            // Pattern Definition: must be exactly two identical halves.
            return left == right;
        }
    }

    public sealed class InvalidIdFinder : IInvalidIdFinder
    {
        private readonly IRangeParser _rangeParser;
        private readonly IInvalidIdSpecification _invalidIdSpec;

        public InvalidIdFinder(IRangeParser rangeParser, IInvalidIdSpecification invalidIdSpec)
        {
            _rangeParser = rangeParser;
            _invalidIdSpec = invalidIdSpec;
        }

        public (IReadOnlyCollection<long> InvalidIds, IReadOnlyCollection<string> Errors)
            FindInvalidIds(string rangeInput)
        {
            var errors = new List<string>();

            // Use Case: Main Success Scenario step 4.1 —
            // For each ID range: parse lower and upper bounds.
            var invalidIds = new HashSet<long>(); // Errors & Issues 5: deduplicate

            foreach (var (start, end) in _rangeParser.ParseRanges(rangeInput, errors))
            {
                // Use Case: step 4.2 — Enumerate through each ID within the bounds.
                for (long id = start; id <= end; id++)
                {
                    // Use Case: step 4.3 — Check whether the ID matches the repeated sequence rule.
                    if (_invalidIdSpec.IsInvalidId(id))
                    {
                        // Use Case: step 4.3a — If it matches, classify as invalid.
                        invalidIds.Add(id);
                    }

                    // Defensive: prevent overflow if someone ever gives a range that ends at long.MaxValue.
                    if (id == long.MaxValue)
                        break;
                }
            }

            // Use Case: step 6 — compile the list of invalid IDs (unique).
            var orderedInvalidIds = invalidIds.OrderBy(id => id).ToArray();
            return (orderedInvalidIds, errors);
        }
    }

    #endregion

    #region Root
    internal static class Bootstrapper
    {
        public static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Register domain services with clean IoC naming.
            services.AddSingleton<IRangeParser, RangeParser>();
            services.AddSingleton<IInvalidIdSpecification, RepeatedTwiceInvalidIdSpecification>();
            services.AddSingleton<IInvalidIdFinder, InvalidIdFinder>();

            return services.BuildServiceProvider();
        }
    }

    #endregion

    #region Application Entry Point (Use Case orchestration)

    internal static class Program
    {
        private static void Main()
        {
            // Use Case step 1: The Visiting Helper enters the Gift Shop.
            Console.WriteLine("North Pole Gift Shop — Invalid Product ID Finder");
            Console.WriteLine("This tool identifies invalid product IDs made of a digit sequence repeated exactly twice.\n");

            // Use Case step 3: The Gift Shop System provides a set of ID ranges.
            Console.WriteLine("Enter ID ranges (comma-separated, e.g. 10-200, 3000-3555):");
            var input = Console.ReadLine() ?? string.Empty;

            // Build IoC container and resolve the application service.
            var serviceProvider = Bootstrapper.ConfigureServices();
            var finder = serviceProvider.GetRequiredService<IInvalidIdFinder>();

            // Use Case steps 4–7: parse ranges, enumerate IDs, classify invalids, and aggregate results.
            var (invalidIds, errors) = finder.FindInvalidIds(input);

            // Errors & Issues 1 & 2: Report any malformed or invalid ranges to the Visiting Helper.
            Console.WriteLine("\nErrors & Issues (if any):");
            if (errors.Count == 0)
            {
                Console.WriteLine(" - None");
            }
            else
            {
                foreach (var error in errors)
                    Console.WriteLine(" - " + error);
            }

            // Use Case step 6: Provide the compiled list of invalid IDs.
            Console.WriteLine("\nInvalid IDs (unique):");
            if (invalidIds.Count == 0)
            {
                Console.WriteLine(" <none>");
            }
            else
            {
                Console.WriteLine(string.Join(", ", invalidIds));
            }

            // Use Case step 7: Compute the total sum of all invalid IDs.
            // Errors & Issues 5: Use BigInteger to avoid arithmetic overflow.
            BigInteger sum = BigInteger.Zero;
            foreach (var id in invalidIds)
            {
                sum += id;
            }

            Console.WriteLine("\nTotal Sum of Invalid IDs:");
            Console.WriteLine(sum);

            // Use Case steps 8–10: Provide results so the clerk can clean the database,
            // then allow the Visiting Helper to proceed.
            Console.WriteLine("\nThe clerk now has the information needed to cleanse the database.");
            Console.WriteLine("You may proceed to the lobby and beyond.");
        }
    }

    #endregion
}
