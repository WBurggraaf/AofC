using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

#region Domain
public record RotationInstruction(char Direction, int Distance);

public class Dial
{
    private readonly int[] _ring = new int[100];
    private int _pointer;

    public const int Range = 100;

    public Dial(int start = 50)
    {
        for (int i = 0; i < Range; i++)
            _ring[i] = i;

        if (start < 0 || start >= Range)
            throw new ArgumentException("Dial start outside range 0–99");

        _pointer = start;
    }

    public int Current => _ring[_pointer];

    // Rotate left without modulo
    public void RotateLeft(int distance)
    {
        int d = distance;

        while (d >= Range)
            d -= Range;

        _pointer -= d;
        while (_pointer < 0)
            _pointer += Range;
    }

    // Rotate right without modulo
    public void RotateRight(int distance)
    {
        int d = distance;

        while (d >= Range)
            d -= Range;

        _pointer += d;
        while (_pointer >= Range)
            _pointer -= Range;
    }
}

public class RotationDocument
{
    public IReadOnlyList<RotationInstruction> Instructions { get; }

    public bool IsEmpty => Instructions.Count == 0;

    public RotationDocument(IReadOnlyList<RotationInstruction> instructions)
    {
        Instructions = instructions;
    }
}

public class ElfDecorator
{
    public string Name { get; }

    public ElfDecorator(string name) => Name = name;

    public void AttemptToOpenDoor(IVaultService vaultService)
    {
        Console.WriteLine($"Elf {Name} begins Use Case UC-1…");
        vaultService.OpenSecretEntrance(this);
    }
}

public interface IPasswordComputationMethod
{
    string MethodId { get; }
    int Compute(Dial dial, RotationDocument doc);
}

public class ClickByClickMethod : IPasswordComputationMethod
{
    public string MethodId => "0x434C49434B";

    public int Compute(Dial dial, RotationDocument doc)
    {
        int zeroHits = 0;

        foreach (var instr in doc.Instructions)
        {
            int distance = instr.Distance;

            // Count full 0–99 cycles
            int fullCycles = distance / Dial.Range;
            zeroHits += fullCycles;

            int remainder = distance - (fullCycles * Dial.Range);

            // Process remaining clicks
            if (instr.Direction == 'L')
            {
                for (int i = 0; i < remainder; i++)
                {
                    dial.RotateLeft(1);
                    if (dial.Current == 0)
                        zeroHits++;
                }
            }
            else
            {
                for (int i = 0; i < remainder; i++)
                {
                    dial.RotateRight(1);
                    if (dial.Current == 0)
                        zeroHits++;
                }
            }
        }

        return zeroHits;
    }
}

#endregion

#region App

public interface IVaultService
{
    void OpenSecretEntrance(ElfDecorator actor);
}

public class VaultService : IVaultService
{
    private readonly IPasswordComputationMethod _method;
    private readonly LegacyEndOfRotationMethod _legacy;

    public VaultService(IPasswordComputationMethod method)
    {
        _method = method;
        _legacy = new LegacyEndOfRotationMethod();
    }

    public void OpenSecretEntrance(ElfDecorator actor)
    {
        Console.WriteLine($"System: Ready to compute password via virtual dial using method {_method.MethodId}.");

        string filePath = "input.txt";

        RotationDocument doc;
        try
        {
            doc = ParseRotationDocument(filePath);
        }
        catch (FormatException ex)
        {
            Console.WriteLine($"E1: Invalid rotation syntax ({ex.Message}).");
            return;
        }

        if (doc.IsEmpty)
        {
            Console.WriteLine("Warning: Document contains zero rotations.");
            Console.Write("Continue with password=0? (y/n): ");
            if (Console.ReadKey().Key != ConsoleKey.Y)
            {
                Console.WriteLine("\nUser declined; returning to idle state.");
                return;
            }
            Console.WriteLine();
        }

        Dial dial;
        try
        {
            dial = new Dial(50);
        }
        catch
        {
            Console.WriteLine("E2: Dial initialization failure.");
            return;
        }

        int password = _method.Compute(dial, doc);
        Console.WriteLine($"System: Computed password = {password}");

        Console.Write("Use this password to unlock the door? (y/n): ");
        if (Console.ReadKey().Key != ConsoleKey.Y)
        {
            Console.WriteLine("\nUser refused password.");
            return;
        }
        Console.WriteLine();

        bool accepted = SimulateDoorController(password);

        if (!accepted)
        {
            Console.WriteLine("E3: Door rejected password – outdated method suspected.");
            int legacyGuess = _legacy.Compute(new Dial(50), doc);
            Console.WriteLine($"Legacy method would have produced: {legacyGuess}");
            return;
        }

        Console.WriteLine("Door unlocked successfully! Use Case UC-1 complete.");
    }

    private RotationDocument ParseRotationDocument(string path)
    {
        var lines = File.ReadAllLines(path);
        var items = new List<RotationInstruction>();

        int lineNo = 0;
        foreach (string raw in lines)
        {
            lineNo++;
            string line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            char d = line[0];
            if (d != 'L' && d != 'R')
                throw new FormatException($"Line {lineNo}: Must start with L or R.");

            if (!int.TryParse(line.Substring(1), out int dist) || dist < 0)
                throw new FormatException($"Line {lineNo}: Distance must be non-negative integer.");

            items.Add(new RotationInstruction(d, dist));
        }

        return new RotationDocument(items);
    }

    private bool SimulateDoorController(int password)
    {
        if (password <= 0)
            return false;

        return true;
    }
}

public class LegacyEndOfRotationMethod : IPasswordComputationMethod
{
    public string MethodId => "LEGACY";

    public int Compute(Dial dial, RotationDocument doc)
    {
        int count = 0;

        foreach (var instr in doc.Instructions)
        {
            if (instr.Direction == 'L')
                dial.RotateLeft(instr.Distance);
            else
                dial.RotateRight(instr.Distance);

            if (dial.Current == 0)
                count++;
        }

        return count;
    }
}

#endregion

#region Root
public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ElfDecorator>(new ElfDecorator("Pinebrush"));
        services.AddSingleton<IPasswordComputationMethod, ClickByClickMethod>();
        services.AddSingleton<IVaultService, VaultService>();

        var provider = services.BuildServiceProvider();

        var elf = provider.GetRequiredService<ElfDecorator>();
        var vaultService = provider.GetRequiredService<IVaultService>();

        Console.WriteLine("=== North Pole Secret Entrance Control System ===");

        elf.AttemptToOpenDoor(vaultService);
    }
}

#endregion
