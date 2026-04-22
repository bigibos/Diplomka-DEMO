using Diplomka;
using Diplomka.Model;
using Diplomka.Files;
using Diplomka.Solver;
using System.Diagnostics;
using Diplomka.ImportExport;
using System.Runtime.ExceptionServices;
using Diplomka.Routing;


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

slots = CsvImporter.LoadSlots($"{rootDirectory}\\slots.csv");
referees = CsvImporter.LoadReferees($"{rootDirectory}\\referees.csv");


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

/*
var brno = new Geo(49.1951, 16.6068);
var praha = new Geo(50.0755, 14.4378);
var pardubice = new Geo(50.0343, 15.7812);
var hradec = new Geo(50.2092, 15.8328);

var a = slots[0];
var b = slots[1];


if (conflictChecker.Overlaps(b, a))
{
    Console.WriteLine("Slot A a B se překrývají.");
}
else
{
    Console.WriteLine("Slot A a B se nepřekrývají.");
}
*/


var solver = new BranchAndBoundSolver(
    referees,
    conflictChecker,
    costCalculator,
    timeLimit: TimeSpan.FromSeconds(60)   // zvyš pro lepší optimum, sniž pro rychlost
);

HCSolver hc = new HCSolver(
    referees,
    conflictChecker,
    costCalculator
);

State result = solver.Solve(slots);


Console.WriteLine();
Console.WriteLine("Rešení pomocí Branch & Bound:");
Console.WriteLine($"Celková cena:       {costCalculator.TotalCost(result):F2}");
Console.WriteLine($"Prázdné sloty:      {result.GetEmptySlots().ToList().Count}");
Console.WriteLine($"Prozkoumáno uzlů:   {solver.NodesExplored}");


CsvExporter.SaveState($"{rootDirectory}\\result.csv", result);


Console.WriteLine("##############################################");

Stopwatch swHC = Stopwatch.StartNew();
Console.WriteLine("Zpracovávám přes Hill Climbing...");
State resultHC = hc.Solve(slots);
swHC.Stop();

Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {costCalculator.TotalCost(resultHC)}");
Console.WriteLine($"Hotovo za: {swHC.ElapsedMilliseconds} ms");

CsvExporter.SaveState($"{rootDirectory}\\resultHC.csv", resultHC);



Console.WriteLine("Hotovo, stiskněte Enter pro ukončení.");
Console.ReadKey();

