using Microsoft.Extensions.DependencyInjection;

public interface IGridRepository
{
    Grid Load();
}

public class FileGridRepository : IGridRepository
{
    private readonly string _filePath;

    public FileGridRepository(string filePath)
    {
        _filePath = filePath;
    }

    public Grid Load()
    {
        if (!File.Exists(_filePath))
            throw new FileNotFoundException("input.txt not found", _filePath);

        var lines = File.ReadAllLines(_filePath);
        return Grid.Parse(lines);
    }
}

public class Grid
{
    private readonly char[][] _cells;

    public int Rows => _cells.Length;
    public int Cols => _cells[0].Length;

    public Grid(char[][] cells)
    {
        _cells = cells;
    }

    public static Grid Parse(string[] lines)
    {
        var cells = new char[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
            cells[i] = lines[i].ToCharArray();

        return new Grid(cells);
    }

    public char Get(int r, int c) => _cells[r][c];
    public void Set(int r, int c, char value) => _cells[r][c] = value;

    public Grid Clone()
    {
        var copy = new char[Rows][];
        for (int i = 0; i < Rows; i++)
            copy[i] = (char[])_cells[i].Clone();
        return new Grid(copy);
    }

    public override string ToString()
    {
        var list = new List<string>();
        foreach (var row in _cells)
            list.Add(new string(row));
        return string.Join("\n", list);
    }
}

public interface IAccessibilityService
{
    bool IsAccessible(Grid grid, int r, int c);
    int CountNeighbors(Grid grid, int r, int c);
}

public class AccessibilityService : IAccessibilityService
{
    private static readonly (int dr, int dc)[] Directions = new[]
    {
        (-1,-1),(-1,0),(-1,1),
        ( 0,-1),       ( 0,1),
        ( 1,-1),( 1,0),( 1,1)
    };

    public int CountNeighbors(Grid grid, int r, int c)
    {
        int count = 0;
        foreach ((int dr, int dc) in Directions)
        {
            int rr = r + dr, cc = c + dc;
            if (rr >= 0 && rr < grid.Rows &&
                cc >= 0 && cc < grid.Cols &&
                grid.Get(rr, cc) == '@')
                count++;
        }
        return count;
    }

    public bool IsAccessible(Grid grid, int r, int c)
    {
        if (grid.Get(r, c) != '@')
            return false;

        int neighbors = CountNeighbors(grid, r, c);
        return neighbors < 4;
    }
}

public interface IRemovalService
{
    int RemoveAllAccessible(Grid grid);
}

public class RemovalService : IRemovalService
{
    private readonly IAccessibilityService _accessibility;

    public RemovalService(IAccessibilityService accessibility)
    {
        _accessibility = accessibility;
    }

    public int RemoveAllAccessible(Grid grid)
    {
        var accessible = new List<(int r, int c)>();

        for (int r = 0; r < grid.Rows; r++)
        {
            for (int c = 0; c < grid.Cols; c++)
            {
                if (_accessibility.IsAccessible(grid, r, c))
                    accessible.Add((r, c));
            }
        }

        foreach (var (r, c) in accessible)
            grid.Set(r, c, '.');

        return accessible.Count;
    }
}

public interface IPaperRollUseCase
{
    void Execute();
}

public class PaperRollUseCase : IPaperRollUseCase
{
    private readonly IGridRepository _repo;
    private readonly IRemovalService _removal;

    public PaperRollUseCase(IGridRepository repo, IRemovalService removal)
    {
        _repo = repo;
        _removal = removal;
    }

    public void Execute()
    {
        Grid grid = _repo.Load();
        Console.WriteLine("Initial Grid:");
        Console.WriteLine(grid.ToString());
        Console.WriteLine();

        int totalRemoved = 0;
        int iteration = 0;

        while (true)
        {
            iteration++;
            Console.WriteLine($"----- ITERATION {iteration} -----");

            int removed = _removal.RemoveAllAccessible(grid);
            Console.WriteLine($"Removed this round: {removed}");
            Console.WriteLine(grid.ToString());
            Console.WriteLine();

            if (removed == 0)
                break;

            totalRemoved += removed;
        }

        Console.WriteLine("==============================");
        Console.WriteLine($"TOTAL REMOVED: {totalRemoved}");
        Console.WriteLine("==============================");
    }
}

class Program
{
    static void Main()
    {
        // Manual DI container (no HostBuilder required)
        var services = new ServiceCollection();

        string filePath = Path.Combine(AppContext.BaseDirectory, "input.txt");

        services.AddSingleton<IGridRepository>(sp => new FileGridRepository(filePath));
        services.AddSingleton<IAccessibilityService, AccessibilityService>();
        services.AddSingleton<IRemovalService, RemovalService>();
        services.AddSingleton<IPaperRollUseCase, PaperRollUseCase>();

        var provider = services.BuildServiceProvider();

        var useCase = provider.GetRequiredService<IPaperRollUseCase>();
        useCase.Execute();
    }
}
