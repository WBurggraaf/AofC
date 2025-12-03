using System.Numerics;
using Microsoft.Extensions.DependencyInjection;

#region Domain Interfaces

public interface IBankValidator
{
    ValidationResult Validate(string bank);
}

public interface IJoltageSelector
{
    string Select12Digits(string bank);
}

public interface IJoltageProcessorService
{
    JoltageProcessingResult ProcessBanks(IEnumerable<string> banks);
}

public interface IInputProvider
{
    IEnumerable<string> ReadInputBanks();
}

public interface IOutputWriter
{
    void WriteResult(JoltageProcessingResult result);
}

#endregion

#region Domain Models

public record ValidationResult(bool IsValid, string Reason);

public class JoltageProcessingResult
{
    public BigInteger Total { get; set; }
    public List<string> ValidBankResults { get; } = new();
    public List<string> InvalidBankMessages { get; } = new();
}

#endregion

#region Domain Services (Logic enforces all use-case rules)
public class BankValidator : IBankValidator
{
    public ValidationResult Validate(string bank)
    {
        // Extension 2b: Must contain only digits 1–9
        if (!System.Text.RegularExpressions.Regex.IsMatch(bank, "^[1-9]+$"))
            return new ValidationResult(false, "Invalid characters detected (only digits 1–9 allowed).");

        // Extension 2a: Bank must have 12+ digits
        if (bank.Length < 12)
            return new ValidationResult(false, "Bank contains fewer than 12 digits.");

        return new ValidationResult(true, "");
    }
}

public class JoltageSelector : IJoltageSelector
{
    public string Select12Digits(string bank)
    {
        int needed = 12;
        int index = 0;
        var result = new char[12];
        int rPos = 0;

        while (needed > 0)
        {
            char maxDigit = '0';
            int maxPos = index;

            // Respect the rule: Only search where enough digits remain
            int limit = bank.Length - needed;

            for (int pos = index; pos <= limit; pos++)
            {
                char d = bank[pos];
                if (d > maxDigit)
                {
                    maxDigit = d;
                    maxPos = pos;
                }
            }

            // Rule 3a: deterministic — choose leftmost max
            result[rPos++] = maxDigit;

            index = maxPos + 1;
            needed--;
        }

        return new string(result);
    }
}

public class JoltageProcessorService : IJoltageProcessorService
{
    private readonly IBankValidator _validator;
    private readonly IJoltageSelector _selector;

    public JoltageProcessorService(IBankValidator validator, IJoltageSelector selector)
    {
        _validator = validator;
        _selector = selector;
    }

    public JoltageProcessingResult ProcessBanks(IEnumerable<string> banks)
    {
        var result = new JoltageProcessingResult();
        BigInteger total = BigInteger.Zero; // Supports rule 6a

        int index = 1;
        foreach (var bank in banks)
        {
            var validation = _validator.Validate(bank);

            if (!validation.IsValid)
            {
                // Extension 2a or 2b
                result.InvalidBankMessages.Add($"Bank {index}: INVALID → {validation.Reason}");
                index++;
                continue;
            }

            // Valid → perform the selection algorithm
            string joltage = _selector.Select12Digits(bank);
            result.ValidBankResults.Add($"Bank {index} result: {joltage}");

            // Add to total (arbitrary precision)
            total += BigInteger.Parse(joltage);

            index++;
        }

        result.Total = total;
        return result;
    }
}

#endregion

#region Infrastructure

public class InputProvider : IInputProvider
{
    public IEnumerable<string> ReadInputBanks()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "input.txt");

        if (!File.Exists(path))
            throw new FileNotFoundException("input.txt not found in execution directory.");

        foreach (var line in File.ReadAllLines(path))
            yield return line.Trim();
    }
}

public class OutputWriter : IOutputWriter
{
    public void WriteResult(JoltageProcessingResult result)
    {
        Console.WriteLine("=== Joltage Computation Results ===");
        Console.WriteLine();

        foreach (var msg in result.InvalidBankMessages)
            Console.WriteLine(msg);

        Console.WriteLine();

        foreach (var r in result.ValidBankResults)
            Console.WriteLine(r);

        Console.WriteLine();
        Console.WriteLine("TOTAL JOLTAG E (BigInteger):");
        Console.WriteLine(result.Total);
        Console.WriteLine();
    }
}

#endregion

#region Program Entry (Application Layer)

class Program
{
    static void Main()
    {
        var services = new ServiceCollection();

        // Register domain + infra services
        services.AddSingleton<IBankValidator, BankValidator>();
        services.AddSingleton<IJoltageSelector, JoltageSelector>();
        services.AddSingleton<IJoltageProcessorService, JoltageProcessorService>();
        services.AddSingleton<IInputProvider, InputProvider>();
        services.AddSingleton<IOutputWriter, OutputWriter>();

        var provider = services.BuildServiceProvider();

        var input = provider.GetRequiredService<IInputProvider>();
        var processor = provider.GetRequiredService<IJoltageProcessorService>();
        var output = provider.GetRequiredService<IOutputWriter>();

        // Use case step 1: read all banks
        var banks = input.ReadInputBanks();

        // Main success scenario steps 2–6
        var result = processor.ProcessBanks(banks);

        // Output final result
        output.WriteResult(result);
    }
}

#endregion
