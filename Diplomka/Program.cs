using Diplomka;
using Diplomka.Data;
using Diplomka.Solver;
using System.Diagnostics;


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
/*
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
*/


BBSolver bb = new BBSolver();
Stopwatch swBB = Stopwatch.StartNew();
State resultBB = bb.BranchAndBound(initial, referees);
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




/*
Console.WriteLine("Řešení pomocí Hill Climbing:");
Console.WriteLine($"Cena: {hc.EvaluateCost(solution1)}");
Console.WriteLine(solution1);
Console.WriteLine("------------------------------------------");

Console.WriteLine("Řešení pomocí Branch and Bound:");
Console.WriteLine($"Cena: {bb.EvaluateCost(solution2)}");
Console.WriteLine(solution2);
*/
