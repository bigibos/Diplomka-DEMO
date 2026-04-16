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


/*
BBSolver bb = new BBSolver();
Stopwatch swBB = Stopwatch.StartNew();
Console.WriteLine("Zrpacovavam pres BB...");
State resultBB = bb.Solve(initial, referees);
swBB.Stop();

Console.WriteLine("Řešení pomocí Branch & Bound:");
Console.WriteLine($"Cena: {bb.StateCost(resultBB)}");
Console.WriteLine($"Čas: {swBB.ElapsedMilliseconds} ms");
Console.WriteLine(resultBB);

CsvExporter.SaveState($"{rootDirectory}\\resultBB.csv", resultBB);
*/


HCSolver hc = new HCSolver();
Stopwatch swHC = Stopwatch.StartNew();
Console.WriteLine("Zrpacovavam pres HC...");
State resultHC = hc.Solve(slots, referees);
swHC.Stop();

Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {hc.StateCost(resultHC)}");
Console.WriteLine($"Čas: {swHC.ElapsedMilliseconds} ms");
Console.WriteLine(resultHC);


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
