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

foreach (var slot in slots)
{
    Console.WriteLine(slot);   
}

foreach (var referee in referees)
{
    Console.WriteLine(referee);
}


State state = new State();

foreach (var slot in slots)
    state.AddSlot(slot);

Random r = new Random();


State initial = new State();
foreach (var slot in slots)
{
    initial.AddSlot(slot);
}


BBSolver bb = new BBSolver();
Stopwatch swBB = Stopwatch.StartNew();
int maxBranchingFactor = int.MaxValue;
int maxSeconds = 1200;
Console.WriteLine($"Zpracovávám přes B&B (Max {maxSeconds} sekund)...");

// Můžeš si pohrát s parametry:
// maxSeconds: Kdy se má prohledávání natvrdo utnout
// maxBranchingFactor: 3-5 je rychlé, 10+ je pomalejší ale přesnější
State resultBB = bb.Solve(initial, referees, maxSeconds: maxSeconds, maxBranchingFactor: maxBranchingFactor);

swBB.Stop();
Console.WriteLine("Řešení pomocí Branch and Bound:");
Console.WriteLine($"Cena: {bb.StateCost(resultBB)}");
Console.WriteLine($"Hotovo za: {swBB.ElapsedMilliseconds} ms");
// Console.WriteLine(resultBB);

CsvExporter.SaveState($"{rootDirectory}\\resultBB.csv", resultBB);



HCSolver hc = new HCSolver();
Stopwatch swHC = Stopwatch.StartNew();
Console.WriteLine("Zpracovávám přes Hill Climbing...");
State resultHC = hc.Solve(slots, referees);
swHC.Stop();

Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {hc.StateCost(resultHC)}");
Console.WriteLine($"Hotovo za: {swHC.ElapsedMilliseconds} ms");
// Console.WriteLine(resultHC);


CsvExporter.SaveState($"{rootDirectory}\\resultHC.csv", resultHC);


Console.WriteLine("Hotovo, stiskněte Enter pro ukončení.");
Console.ReadKey();




/*
Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {hc.EvaluateCost(solution1)}");
Console.WriteLine(solution1);
Console.WriteLine("------------------------------------------");

Console.WriteLine("Řešení pomocí Branch and Bound:");
Console.WriteLine($"Cena: {bb.EvaluateCost(solution2)}");
Console.WriteLine(solution2);
*/
