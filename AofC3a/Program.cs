using Microsoft.Extensions.DependencyInjection;

#region Domain Model

public class BatteryBank
{
    public string Digits { get; }
    public int Index { get; }

    public BatteryBank(string digits, int index)
    {
        Digits = digits;
        Index = index;
    }
}

public class JoltageResult
{
    public int BankIndex { get; }
    public int MaxJoltage { get; }

    public JoltageResult(int bankIndex, int maxJoltage)
    {
        BankIndex = bankIndex;
        MaxJoltage = maxJoltage;
    }
}

#endregion

#region Actors

public class MaintenanceTechnician
{
    public string Name => "Maintenance Technician";
}

public class EscalatorSystem
{
    public string Name => "Escalator System";
}

public class ElfTechnician
{
    public string Name => "Elf Technician";
}

#endregion

#region Services

public interface IInputValidator
{
    List<BatteryBank> Validate(string rawInput);
}

public class InputValidator : IInputValidator
{
    public List<BatteryBank> Validate(string rawInput)
    {
        var result = new List<BatteryBank>();
        var lines = rawInput.Split('\n', StringSplitOptions.None);

        Console.WriteLine("Step 2: System validates each line.\n");

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                Console.WriteLine($"Line {i + 1}: Skipped empty line.");
                continue;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(line, "^[1-9]+$"))
                throw new Exception($"ERROR: Line {i + 1} contains invalid characters. Only digits 1–9 allowed.");

            if (line.Length < 2)
                throw new Exception($"ERROR: Line {i + 1} has fewer than two batteries.");

            Console.WriteLine($"Line {i + 1}: Validated as a battery bank.");
            result.Add(new BatteryBank(line, i + 1));
        }

        return result;
    }
}

public interface IJoltageCalculator
{
    JoltageResult Compute(BatteryBank bank);
}

public class JoltageCalculator : IJoltageCalculator
{
    public JoltageResult Compute(BatteryBank bank)
    {
        Console.WriteLine($"\nProcessing Bank {bank.Index}: {bank.Digits}");

        int maxPair = -1;
        int firstIdx = -1;
        int secondIdx = -1;
        var digits = bank.Digits;

        for (int i = 0; i < digits.Length - 1; i++)
        {
            for (int j = i + 1; j < digits.Length; j++)
            {
                int pair = int.Parse(digits[i].ToString() + digits[j].ToString());
                if (pair > maxPair)
                {
                    maxPair = pair;
                    firstIdx = i;
                    secondIdx = j;
                }
            }
        }

        Console.WriteLine($"  Selected digits '{digits[firstIdx]}' (index {firstIdx}) and '{digits[secondIdx]}' (index {secondIdx}).");
        Console.WriteLine($"  Bank {bank.Index} Maximum Joltage: {maxPair}");

        return new JoltageResult(bank.Index, maxPair);
    }
}

#endregion

#region Use Case

public interface IGenerateMaxJoltageUseCase
{
    void Execute(string input);
}

public class GenerateMaxJoltageUseCase : IGenerateMaxJoltageUseCase
{
    private readonly IInputValidator _validator;
    private readonly IJoltageCalculator _calculator;
    private readonly MaintenanceTechnician _technician;
    private readonly EscalatorSystem _escalator;
    private readonly ElfTechnician _elf;

    public GenerateMaxJoltageUseCase(
        IInputValidator validator,
        IJoltageCalculator calculator)
    {
        _validator = validator;
        _calculator = calculator;
        _technician = new MaintenanceTechnician();
        _escalator = new EscalatorSystem();
        _elf = new ElfTechnician();
    }

    public void Execute(string input)
    {
        Console.WriteLine($"Step 1: {_technician.Name} submits the puzzle input.\n");

        List<BatteryBank> banks;

        try
        {
            banks = _validator.Validate(input);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine("Computation aborted due to input error.");
            return;
        }

        Console.WriteLine("\nStep 3: System begins processing validated banks.");

        int total = 0;
        var results = new List<JoltageResult>();

        foreach (var bank in banks)
        {
            var result = _calculator.Compute(bank);
            results.Add(result);
            total += result.MaxJoltage;
        }

        Console.WriteLine("\nStep 7: System outputs final results.");
        Console.WriteLine($"Per-bank maximum joltages: {string.Join(", ", results.ConvertAll(r => r.MaxJoltage))}");
        Console.WriteLine($"Total output joltage: {total}\n");

        Console.WriteLine($"{_escalator.Name} receives the computed joltage and can now operate.");
        Console.WriteLine($"{_elf.Name} can continue fixing the elevators.\n");
    }
}

#endregion

#region Program

public class Program
{
    public static void Main()
    {
        // Register DI services
        var services = new ServiceCollection();
        services.AddSingleton<IInputValidator, InputValidator>();
        services.AddSingleton<IJoltageCalculator, JoltageCalculator>();
        services.AddSingleton<IGenerateMaxJoltageUseCase, GenerateMaxJoltageUseCase>();

        var provider = services.BuildServiceProvider();
        var useCase = provider.GetRequiredService<IGenerateMaxJoltageUseCase>();

        Console.WriteLine("Reading input from input.txt in the execution directory...\n");

        string inputFile = Path.Combine(Directory.GetCurrentDirectory(), "input.txt");

        if (!File.Exists(inputFile))
        {
            Console.WriteLine($"ERROR: Could not find {inputFile}");
            return;
        }

        string input = File.ReadAllText(inputFile);

        Console.WriteLine("Loaded input:\n" + input + "\n");

        useCase.Execute(input);
    }
}

#endregion
