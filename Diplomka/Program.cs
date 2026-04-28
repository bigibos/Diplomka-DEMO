using Diplomka;
using Diplomka.Entity;
using Diplomka.Files;
using Diplomka.Solver;
using System.Diagnostics;
using Diplomka.ImportExport;
using System.Runtime.ExceptionServices;
using Diplomka.Routing;
using System.Net.Http.Headers;

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

slots = CsvImporter.LoadSlots($"{rootDirectory}\\slots_comb.csv");
referees = CsvImporter.LoadReferees($"{rootDirectory}\\referees_comb.csv");


Console.WriteLine($"Načteno {referees.Count} rozhodčích a {slots.Count} slotů.");

/*
 * Hlavni konfigurace
 */
var config = new SolverConfiguration()
{
    MaxWasteTime = TimeSpan.FromHours(3),
    RefereePostTime = TimeSpan.FromMinutes(30),
    RefereePrepTime = TimeSpan.FromMinutes(30),
    DistanceFactor = 1.0,
    OverRankFactor = 1.0,
    UnderRankFactor = 1.0,
    UnassignedCost = 100_000.0
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
    timeLimit: TimeSpan.FromSeconds(20) // omezeni casu behu B&B
);

HCSolver hcSolver = new HCSolver(
    referees,
    conflictChecker,
    costCalculator
);

Stopwatch sw = new Stopwatch();

sw.Restart();
State resultBB = bbSolver.Solve(slots);
sw.Stop(); 

Console.WriteLine("Rešení pomocí Branch & Bound:");
Console.WriteLine($"Celková cena:       {costCalculator.TotalCost(resultBB):F2}");
Console.WriteLine($"Prázdné sloty:      {resultBB.GetEmptySlots().ToList().Count}");
Console.WriteLine($"Prozkoumáno uzlů:   {bbSolver.NodesExplored}");
Console.WriteLine($"Hotovo za:          {sw.ElapsedMilliseconds} ms");

CsvExporter.SaveState($"{rootDirectory}\\result.csv", resultBB, routeSolver);

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



