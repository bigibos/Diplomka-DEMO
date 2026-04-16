using Diplomka;
using Diplomka.Model;
using Diplomka.Files;
using Diplomka.Solver;
using System.Diagnostics;
using Diplomka.ImportExport;


string rootDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;

List<Slot> slots = new List<Slot>();
List<Referee> referees = new List<Referee>();


slots.Add(new Slot { Id = 1, RequiredRank = 2, Location = new Geo(50.0878, 14.4205), Start = new DateTime(2024, 6, 1, 10, 0, 0), End = new DateTime(2024, 6, 1, 12, 0, 0) });
slots.Add(new Slot { Id = 2, RequiredRank = 1, Location = new Geo(49.1951, 16.6068), Start = new DateTime(2024, 6, 1, 11, 0, 0), End = new DateTime(2024, 6, 1, 13, 0, 0) });



var fs = new FileStorage();


// CsvExporter.SaveSlots($"{rootDirectory}\\slots.csv", slots);

slots = CsvImporter.LoadSlots($"{rootDirectory}\\slots.csv");

foreach (var s in slots)
{
    Console.WriteLine(s);   
}



/*
var t = fs.Load($"{rootDirectory}\\teams.json", new JsonSerializer<List<Team>>());
var m = fs.Load($"{rootDirectory}\\matches.json", sr);


foreach (var team in t)
{
    Console.WriteLine(team);
}
Console.WriteLine(m);
*/


/*
List<Slot> slots = FileManager.ReadSlots();
List<Referee> referees = FileManager.ReadReferees();    
State state = new State();

foreach (var slot in slots)
    state.AddSlot(slot);

Random r = new Random();


State initial = new State();
foreach (var slot in slots)
{
    initial.AddSlot(slot);
}

var praha = new Geo(50.0878, 14.4205);
var brno = new Geo(49.1951, 16.6068);

var pardubice = new Geo(50.0375792, 15.7774239);
var litomysl = new Geo(49.8720311, 16.3105192);

var info = await litomysl.GetRoadRouteToAsync(pardubice);

if (info != null)
{
    Console.WriteLine($"Vzdálenost po silnici: {info.DistanceKm:F2} km");
    Console.WriteLine($"Doba jízdy: {info.DurationMinutes:F0} min");
}


BBSolver bb = new BBSolver();
Stopwatch swBB = Stopwatch.StartNew();
State resultBB = bb.Solve(initial, referees);
swBB.Stop();

Console.WriteLine("Řešení pomocí Branch & Bound:");
Console.WriteLine($"Cena: {bb.StateCost(resultBB)}");
Console.WriteLine($"Čas: {swBB.ElapsedMilliseconds} ms");
Console.WriteLine(resultBB);
FileManager.WriteState(resultBB, "resultBB.csv");

Console.WriteLine();
Console.WriteLine("###########################################################");
Console.WriteLine();

HCSolver hc = new HCSolver();
Stopwatch swHC = Stopwatch.StartNew();
State resultHC = hc.Solve(slots, referees);
swHC.Stop();

Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {hc.StateCost(resultHC)}");
Console.WriteLine($"Čas: {swHC.ElapsedMilliseconds} ms");
Console.WriteLine(resultHC);
FileManager.WriteState(resultHC, "resultHC.csv");

Console.ReadKey();
*/



/*
Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {hc.EvaluateCost(solution1)}");
Console.WriteLine(solution1);
Console.WriteLine("------------------------------------------");

Console.WriteLine("Řešení pomocí Branch and Bound:");
Console.WriteLine($"Cena: {bb.EvaluateCost(solution2)}");
Console.WriteLine(solution2);
*/
