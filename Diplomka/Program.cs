using Diplomka;
using Diplomka.Model;
using Diplomka.Files;
using Diplomka.Solver;
using System.Diagnostics;
using Diplomka.ImportExport;


string rootDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;

List<Slot> slots = new List<Slot>();
List<Referee> referees = new List<Referee>();


var fs = new FileStorage();


// CsvExporter.SaveSlots($"{rootDirectory}\\slots.csv", slots);

slots = CsvImporter.LoadSlots($"{rootDirectory}\\slots.csv");
referees = CsvImporter.LoadReferees($"{rootDirectory}\\referees.csv");

Console.WriteLine($"Načteno {referees.Count} rozhodčích a {slots.Count} slotů.");

var solver = new BranchAndBoundSolver(
    referees,
    timeLimit: TimeSpan.FromSeconds(1800)   // zvyš pro lepší optimum, sniž pro rychlost
);

HCSolver hc = new HCSolver(referees);

State result = solver.Solve(slots);

Console.WriteLine();
Console.WriteLine("Rešení pomocí Branch & Bound:");
Console.WriteLine($"Celková cena:       {CostCalculator.TotalCost(result):F2}");
Console.WriteLine($"Prázdné sloty:      {result.GetEmptySlots().Count}");
Console.WriteLine($"Prozkoumáno uzlů:   {solver.NodesExplored}");


CsvExporter.SaveState($"{rootDirectory}\\result.csv", result);


Stopwatch swHC = Stopwatch.StartNew();
Console.WriteLine("Zpracovávám přes Hill Climbing...");
State resultHC = hc.Solve(slots);
swHC.Stop();

Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {CostCalculator.TotalCost(resultHC)}");
Console.WriteLine($"Hotovo za: {swHC.ElapsedMilliseconds} ms");

CsvExporter.SaveState($"{rootDirectory}\\resultHC.csv", resultHC);



Console.WriteLine("Hotovo, stiskněte Enter pro ukončení.");
Console.ReadKey();

