using Diplomka;
using Diplomka.Entity;
using Diplomka.Files;
using Diplomka.Solver;
using System.Diagnostics;
using Diplomka.ImportExport;
using System.Runtime.ExceptionServices;
using Diplomka.Routing;
using System.Net.Http.Headers;


string rootDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;

List<Slot> slots = new List<Slot>();
List<Referee> referees = new List<Referee>();

slots = CsvImporter.LoadSlots($"{rootDirectory}\\slots.csv");
referees = CsvImporter.LoadReferees($"{rootDirectory}\\referees.csv");


var distanceTable = new DistanceTable();
var config = new SolverConfiguration()
{
    MaxWasteTime = TimeSpan.FromHours(4),
    RefereePostTime = TimeSpan.FromMinutes(120),
    RefereePrepTime = TimeSpan.FromMinutes(90),
    DistanceFactor = 1.0,
    RankFactor = 1.0,
    OverRankFactor = 1.0,
    UnderRankFactor = 1.0
};

var conflictChecker = new ConflictChecker(distanceTable, config);
var costCalculator = new CostCalculator(distanceTable, config);

var fs = new FileStorage();


var slotLocations = slots.Select(s => s.Location).Distinct().ToList();
var refereeLocations = referees.Select(r => r.Location).Distinct().ToList();

var allLocations = slotLocations.Union(refereeLocations).Distinct().ToList();

var routeSolver = new RouteSolver(distanceTable, config);

Console.WriteLine($"Budování matice vzdáleností přes OSRM - toto může chvíli trvat...");
await distanceTable.Initialize(allLocations);


Console.WriteLine("Matice vzdáleností hotová.");

Console.WriteLine($"Načteno {referees.Count} rozhodčích a {slots.Count} slotů.");


var solver = new BBSolver(
    referees,
    conflictChecker,
    costCalculator,
    timeLimit: TimeSpan.FromSeconds(60) // omezeni casu behu B&B
);

HCSolver hc = new HCSolver(
    referees,
    conflictChecker,
    costCalculator
);
Stopwatch sw = new Stopwatch();

sw.Restart();
State result = solver.Solve(slots);
sw.Stop(); 


Console.WriteLine();
Console.WriteLine("Rešení pomocí Branch & Bound:");
Console.WriteLine($"Celková cena:       {costCalculator.TotalCost(result):F2}");
Console.WriteLine($"Prázdné sloty:      {result.GetEmptySlots().ToList().Count}");
Console.WriteLine($"Prozkoumáno uzlů:   {solver.NodesExplored}");
Console.WriteLine($"Hotovo za: {sw.ElapsedMilliseconds} ms");

CsvExporter.SaveState($"{rootDirectory}\\result.csv", result, routeSolver);
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

// Paralelní


Console.WriteLine("##############################################");

sw.Restart();
State resultHC = hc.Solve(slots);
sw.Stop();

Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {costCalculator.TotalCost(resultHC)}");
Console.WriteLine($"Hotovo za: {sw.ElapsedMilliseconds} ms");

CsvExporter.SaveState($"{rootDirectory}\\resultHC.csv", resultHC, routeSolver);



Console.WriteLine("Hotovo, stiskněte Enter pro ukončení.");
Console.ReadKey();

