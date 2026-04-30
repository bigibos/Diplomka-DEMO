using Diplomka;
using Diplomka.Entity;
using Diplomka.Files;
using Diplomka.ImportExport;
using Diplomka.Routing;
using Diplomka.Solver;
using Diplomka.Utils;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using static Diplomka.Utils.ScenerioGenerator;

/*
 * TODO: Opravit
 * 
 * Pro vstupni data slots_comb.csv a referees_comb.csv
 * Pro dleis casy na pripravu a uklid
 * Vraci unfeasable reseni (sloty zustavaji neobsazeny) bez ohledu na naroky na cenu
 * Zaroven je s takovym objemem dat B&B pomerne pomaly - mala sance nalezeni lepsiho reseni
 * 
 * Pokud je to mozne (coz by u techto dat a parametru snad melo) musi byt vraceno minimalne feasable reseni - bez cas. kolizi
 * Mozne nedostatky greedy alg. nebo opravneho alg.
 * Bylo by mozna dobre analyticky orezat vstupni mnozinu rozhodcich, aby byla co nejmensi ale zaroven pouzitelna (MOZNA)
 * 
 */



string rootDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;

List<Slot> slots = new List<Slot>();
List<Referee> referees = new List<Referee>();

// slots = CsvImporter.LoadSlots($"{rootDirectory}\\slots_comb_2.csv");
// referees = CsvImporter.LoadReferees($"{rootDirectory}\\referees_comb_2.csv");

ScenerioGenerator gen = new ScenerioGenerator
{
    SlotsNumber = 60,
    RefereeNumber = 35,
    DayClustering = 0.4,
    LocationClustering = 0.5,
    OverlapProbability = 0.05,
    EliteRefereeProbability = 0.2,
    RefereeRankDistribution = new()
    {
        new RankBucket { Min = 80, Max = 100, Weight = 0.15 },
        new RankBucket { Min = 40, Max = 80, Weight = 0.55 },
        new RankBucket { Min = 10, Max = 40, Weight = 0.30 }
    },
    SlotRankDistribution = new()
    {
        new RankBucket { Min = 75, Max = 100, Weight = 0.40 }, // víc náročných slotů
        new RankBucket { Min = 40, Max = 75, Weight = 0.45 },
        new RankBucket { Min = 10, Max = 40, Weight = 0.15 }
    }
};

var dateFrom = new DateTime(2025, 3, 1, 8, 0, 0);
var dateTo = new DateTime(2025, 3, 7, 20, 0, 0);

slots = gen.GenerateSlots(dateFrom, dateTo);
referees = gen.GenerateReferess();

/*
foreach(var s in slots)
    Console.WriteLine(s);

foreach (var r in referees)
    Console.WriteLine(r);
*/

Console.WriteLine($"Načteno {referees.Count} rozhodčích a {slots.Count} slotů.");

/*
 * Hlavni konfigurace
 */
var config = new SolverConfiguration()
{
    MaxWasteTime = TimeSpan.FromHours(6),
    RefereePostTime = TimeSpan.FromMinutes(60),
    RefereePrepTime = TimeSpan.FromMinutes(90),
    DistanceFactor = 1.0,
    OverRankFactor = 1.0,
    UnderRankFactor = 1.0,  
    UnassignedCost = 1_000_000.0,
    RelativeGap = 0.01
};

/*
 * Geolokace a budovani tabulky tras
 */
var slotLocations = slots.Select(s => s.Location).Distinct().ToList();
var refereeLocations = referees.Select(r => r.Location).Distinct().ToList();
var allLocations = slotLocations.Union(refereeLocations).Distinct().ToList();
var distanceTable = new DistanceTable();

Console.WriteLine($"Budování matice vzdáleností přes OSRM - toto může chvíli trvat...");
await distanceTable.Initialize(allLocations);

var routeSolver = new RouteSolver(distanceTable, config);


var conflictChecker = new ConflictChecker(distanceTable, config);
var costCalculator = new CostCalculator(distanceTable, config);


BBSolver bbSolver = new BBSolver(
    referees,
    conflictChecker,
    costCalculator,
    config,
    timeLimit: TimeSpan.FromSeconds(60*5) // omezeni casu behu B&B
);


HCSolver hcSolver = new HCSolver(
    referees,
    conflictChecker,
    costCalculator
)
{
    MaxAttempts = 150,
    MaxIterations = slots.Count * 2,
    MaxMoves = 60
};

Stopwatch sw = new Stopwatch();

sw.Restart();
State resultBB = bbSolver.Solve(slots);
sw.Stop();

Console.WriteLine("Rešení pomocí Branch & Bound:");
Console.WriteLine($"Celková cena:       {costCalculator.TotalCost(resultBB):F2}");
Console.WriteLine($"Prázdné sloty:      {resultBB.GetEmptySlots().ToList().Count}");
Console.WriteLine($"Prozkoumáno uzlů:   {bbSolver.NodesExplored}");
Console.WriteLine($"Hotovo za:          {sw.ElapsedMilliseconds} ms");

CsvExporter.SaveState($"{rootDirectory}\\resultBB.csv", resultBB, routeSolver);

/*
Console.WriteLine("Spoustim week solver");
var weekSolver = new WeeklyDecompositionSolver(referees, conflictChecker, costCalculator);
sw.Restart();
State result = weekSolver.Solve(slots);
sw.Stop();
// State result = await weekSolver.RunParallelAsync(slots);
CsvExporter.SaveState($"{rootDirectory}\\result.csv", result, routeSolver);

Console.WriteLine("Řešení pomocí Weekly B&B:");
Console.WriteLine($"Cena: {costCalculator.TotalCost(result)}");
*/



Console.WriteLine("\n--------------------------------------------------\n");

sw.Restart();
State resultHC = hcSolver.Solve(slots);
sw.Stop();

Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Celková cena:       {costCalculator.TotalCost(resultHC):F2}");
Console.WriteLine($"Prázdné sloty:      {resultHC.GetEmptySlots().ToList().Count}");
Console.WriteLine($"Hotovo za:          {sw.ElapsedMilliseconds} ms");

CsvExporter.SaveState($"{rootDirectory}\\resultHC.csv", resultHC, routeSolver);



