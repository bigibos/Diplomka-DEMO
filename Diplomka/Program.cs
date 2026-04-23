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
 * 
 * TODO: Opravit prekryvani casu
 * Momentalne by to melo kontrolovat dojezdy a cas na pripravu, ale nefunguje to.
 * Rozhodci jsou prirazeni do slotu kde nemaj absolutne sanci se umistit.
 * PRoste je to horsi nez pri jednoduche kontrole prakryvu v predchozi verzi.
 * 
 * NUTNO OPRAVIT
 * 
 * 
 */

string rootDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;

List<Slot> slots = new List<Slot>();
List<Referee> referees = new List<Referee>();

slots = CsvImporter.LoadSlots($"{rootDirectory}\\slots_test.csv");
referees = CsvImporter.LoadReferees($"{rootDirectory}\\referees_test.csv");


var distanceTable = new DistanceTable();
var config = new SolverConfiguration()
{
    DistanceWeight = 1.0,
    RankWeight = 1.0,
    RefereePostpTime = TimeSpan.FromMinutes(120),
    RefereePrepTime = TimeSpan.FromMinutes(90)
};

var conflictChecker = new ConflictChecker(distanceTable, config);
var costCalculator = new CostCalculator(distanceTable, config);

var fs = new FileStorage();


var slotLocations = slots.Select(s => s.Location).Distinct().ToList();
var refereeLocations = referees.Select(r => r.Location).Distinct().ToList();

var allLocations = slotLocations.Union(refereeLocations).Distinct().ToList();


Console.WriteLine($"Budování matice vzdáleností přes OSRM - toto může chvíli trvat...");
await distanceTable.Initialize(allLocations);


Console.WriteLine("Matice vzdáleností hotová.");
// Console.WriteLine(DistanceTable.GetInstance());

Console.WriteLine($"Načteno {referees.Count} rozhodčích a {slots.Count} slotů.");


var solver = new BBSolver(
    referees,
    conflictChecker,
    costCalculator,
    timeLimit: TimeSpan.FromSeconds(15)   // zvyš pro lepší optimum, sniž pro rychlost
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

CsvExporter.SaveState($"{rootDirectory}\\result.csv", result);


Console.WriteLine("##############################################");

sw.Restart();
State resultHC = hc.Solve(slots);
sw.Stop();

Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {costCalculator.TotalCost(resultHC)}");
Console.WriteLine($"Hotovo za: {sw.ElapsedMilliseconds} ms");

CsvExporter.SaveState($"{rootDirectory}\\resultHC.csv", resultHC);



Console.WriteLine("Hotovo, stiskněte Enter pro ukončení.");
Console.ReadKey();

