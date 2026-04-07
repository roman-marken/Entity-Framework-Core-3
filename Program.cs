using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string City { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int GoalsScored { get; set; }
    public int GoalsConceded { get; set; }
    public List<Player> Players { get; set; }
    public List<Match> HomeMatches { get; set; }
    public List<Match> AwayMatches { get; set; }
}

public class Player
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Country { get; set; }
    public int Number { get; set; }
    public string Position { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; }
    public List<Goal> Goals { get; set; }
}

public class Match
{
    public int Id { get; set; }
    public int Team1Id { get; set; }
    public int Team2Id { get; set; }
    public int Team1Goals { get; set; }
    public int Team2Goals { get; set; }
    public DateTime MatchDate { get; set; }
    public Team Team1 { get; set; }
    public Team Team2 { get; set; }
    public List<Goal> Goals { get; set; }
}

public class Goal
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int PlayerId { get; set; }
    public int Minute { get; set; }
    public Match Match { get; set; }
    public Player Player { get; set; }
}

public class ChampionshipContext : DbContext
{
    public DbSet<Team> Teams { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<Goal> Goals { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SpanishChampionshipDB;Trusted_Connection=True;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Match>()
            .HasOne(m => m.Team1)
            .WithMany(t => t.HomeMatches)
            .HasForeignKey(m => m.Team1Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.Team2)
            .WithMany(t => t.AwayMatches)
            .HasForeignKey(m => m.Team2Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChampionshipService
{
    private readonly ChampionshipContext _context;
    public ChampionshipService(ChampionshipContext context) { _context = context; }

    public async Task<List<object>> GetGoalDifferenceForEachTeamAsync()
    {
        var teams = await _context.Teams.ToListAsync();
        return teams.Select(t => new { Team = t.Name, GoalDifference = t.GoalsScored - t.GoalsConceded }).ToList<object>();
    }

    public async Task<Match> GetFullMatchInfoAsync(int matchId)
    {
        return await _context.Matches
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .Include(m => m.Goals)
            .ThenInclude(g => g.Player)
            .FirstOrDefaultAsync(m => m.Id == matchId);
    }

    public async Task<List<Match>> GetMatchesByDateAsync(DateTime date)
    {
        return await _context.Matches
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .Where(m => m.MatchDate.Date == date.Date)
            .ToListAsync();
    }

    public async Task<List<Match>> GetAllMatchesForTeamAsync(string teamName)
    {
        return await _context.Matches
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .Where(m => m.Team1.Name == teamName || m.Team2.Name == teamName)
            .ToListAsync();
    }

    public async Task<List<Player>> GetGoalscorersByDateAsync(DateTime date)
    {
        var matchesOnDate = await _context.Matches
            .Where(m => m.MatchDate.Date == date.Date)
            .Select(m => m.Id)
            .ToListAsync();

        var goals = await _context.Goals
            .Include(g => g.Player)
            .Where(g => matchesOnDate.Contains(g.MatchId))
            .Select(g => g.Player)
            .Distinct()
            .ToListAsync();

        return goals;
    }
}

public class MatchCrudService
{
    private readonly ChampionshipContext _context;
    public MatchCrudService(ChampionshipContext context) { _context = context; }

    public async Task<bool> AddMatchAsync(Match newMatch)
    {
        bool exists = await _context.Matches.AnyAsync(m =>
            m.Team1Id == newMatch.Team1Id &&
            m.Team2Id == newMatch.Team2Id &&
            m.MatchDate.Date == newMatch.MatchDate.Date);

        if (exists)
        {
            Console.WriteLine($"Матч мiж командами вже iснує на цю дату!");
            return false;
        }

        await _context.Matches.AddAsync(newMatch);
        await _context.SaveChangesAsync();
        Console.WriteLine($"Матч додано успiшно!");
        return true;
    }

    public async Task<bool> UpdateMatchAsync(string team1Name, string team2Name, DateTime matchDate, Action<Match> updateAction)
    {
        var match = await _context.Matches
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .FirstOrDefaultAsync(m => m.Team1.Name == team1Name && m.Team2.Name == team2Name && m.MatchDate.Date == matchDate.Date);

        if (match == null)
        {
            Console.WriteLine($"Матч мiж {team1Name} та {team2Name} на дату {matchDate.ToShortDateString()} не знайдено.");
            return false;
        }

        updateAction(match);
        await _context.SaveChangesAsync();
        Console.WriteLine($"Данi матчу оновлено.");
        return true;
    }

    public async Task<bool> DeleteMatchAsync(string team1Name, string team2Name, DateTime matchDate)
    {
        var match = await _context.Matches
            .Include(m => m.Team1)
            .Include(m => m.Team2)
            .FirstOrDefaultAsync(m => m.Team1.Name == team1Name && m.Team2.Name == team2Name && m.MatchDate.Date == matchDate.Date);

        if (match == null)
        {
            Console.WriteLine($"Матч мiж {team1Name} та {team2Name} на дату {matchDate.ToShortDateString()} не знайдено.");
            return false;
        }

        Console.Write($"Ви дiйсно хочете видалити матч {team1Name} vs {team2Name} вiд {matchDate.ToShortDateString()}? (так/нi): ");
        var answer = Console.ReadLine();

        if (answer?.ToLower() == "так")
        {
            _context.Matches.Remove(match);
            await _context.SaveChangesAsync();
            Console.WriteLine($"Матч видалено.");
            return true;
        }

        Console.WriteLine("Видалення скасовано.");
        return false;
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ChampionshipContext>();
        services.AddScoped<ChampionshipService>();
        services.AddScoped<MatchCrudService>();
        var provider = services.BuildServiceProvider();

        var championshipService = provider.GetRequiredService<ChampionshipService>();
        var matchCrudService = provider.GetRequiredService<MatchCrudService>();

        Console.WriteLine("=== ЗАВДАННЯ 2: ФУНКЦІОНАЛЬНІСТЬ ===");
        var goalDifferences = await championshipService.GetGoalDifferenceForEachTeamAsync();
        Console.WriteLine("Рiзниця забитих та пропущених голiв:");
        foreach (var item in goalDifferences) Console.WriteLine(item);

        var fullMatch = await championshipService.GetFullMatchInfoAsync(1);
        if (fullMatch != null) Console.WriteLine($"Повна iнформацiя про матч: {fullMatch.Team1.Name} {fullMatch.Team1Goals} - {fullMatch.Team2Goals} {fullMatch.Team2.Name}");

        var matchesByDate = await championshipService.GetMatchesByDateAsync(DateTime.Now);
        Console.WriteLine($"Матчiв на сьогоднi: {matchesByDate.Count}");

        var teamMatches = await championshipService.GetAllMatchesForTeamAsync("Реал Мадрид");
        Console.WriteLine($"Матчiв Реал Мадрид: {teamMatches.Count}");

        var goalscorers = await championshipService.GetGoalscorersByDateAsync(DateTime.Now);
        Console.WriteLine($"Гравцiв якi забили сьогоднi: {goalscorers.Count}");

        Console.WriteLine("\n=== ЗАВДАННЯ 3: CRUD ДЛЯ МАТЧIВ ===");
        var newMatch = new Match
        {
            Team1Id = 1,
            Team2Id = 2,
            Team1Goals = 0,
            Team2Goals = 0,
            MatchDate = DateTime.Now.AddDays(7)
        };
        await matchCrudService.AddMatchAsync(newMatch);

        await matchCrudService.UpdateMatchAsync("Реал Мадрид", "Барселона", DateTime.Now.AddDays(7), m =>
        {
            m.Team1Goals = 2;
            m.Team2Goals = 1;
        });

        await matchCrudService.DeleteMatchAsync("Реал Мадрид", "Барселона", DateTime.Now.AddDays(7));
    }
}