using Diplomka;
using Diplomka.Model;
using Diplomka.Files;
using Diplomka.Solver;
using System.Diagnostics;


string rootDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
Team team1 = new Team { Name = "Team A" };
Team team2 = new Team { Name = "Team B" };

List<Team> teams = new List<Team> { team1, team2 }; 


List<Match> matches = new List<Match> {
    new Match
    {
        Location = new Geo(50.0878, 14.4205),
        Home = team1,
        Away = team2,
        Start = new DateTime(2024, 6, 1, 15, 0, 0),
        End = new DateTime(2024, 6, 1, 17, 0, 0)
    },
    new Match
    {
        Location = new Geo(49.1951, 16.6068),
        Home = team2,
        Away = team1,
        Start = new DateTime(2024, 6, 2, 15, 0, 0),
        End = new DateTime(2024, 6, 2, 17, 0, 0)
    }
};



AppData appData = new AppData
{
    Teams = teams,
    Matches = matches,
    Referees = new List<Referee>(),
    Slots = new List<Slot>()
};

var fs = new FileStorage();

// fs.Save($"{rootDirectory}\\data.json", appData, new JsonSerializer<AppData>());

var data = fs.Load($"{rootDirectory}\\data.json", new JsonSerializer<AppData>());

Console.WriteLine(data);
data.Teams[0].Name = "Dynamo Pardubice";
Console.WriteLine(data);

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
