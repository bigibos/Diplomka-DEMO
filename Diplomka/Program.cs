using Diplomka;
using Diplomka.Entity;
using Diplomka.Files;
using Diplomka.ImportExport;
using Diplomka.Routing;
using Diplomka.Solver;
using Diplomka.Utils;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using static Diplomka.Utils.ScenerioGenerator;

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

/*
 * ---------------------------------------
 * Import dat - pokud data nejsou generovana
 * ---------------------------------------
 */
slots = CsvImporter.LoadSlots($"{rootDirectory}\\slots_comb_2.csv");
referees = CsvImporter.LoadReferees($"{rootDirectory}\\referees_comb_2.csv");


/*
 * ---------------------------------------
 * Inicializace generatoru scenaru
 * ---------------------------------------
 */
ScenerioGenerator gen = new ScenerioGenerator
{
    SlotsNumber = 300,
    RefereeNumber = 150,
    DayClustering = 0.4,
    LocationClustering = 0.5,
    OverlapProbability = 0.05,
    EliteRefereeProbability = 0.2,
    RefereeRankDistribution = new()
    {
        new RankBucket { Min = 80, Max = 100, Weight = 0.15 },
        new RankBucket { Min = 40, Max = 80, Weight = 0.55 },
        new RankBucket { Min = 10, Max = 40, Weight = 0.30 }
    },
    SlotRankDistribution = new()
    {
        new RankBucket { Min = 75, Max = 100, Weight = 0.40 }, // víc náročných slotů
        new RankBucket { Min = 40, Max = 75, Weight = 0.45 },
        new RankBucket { Min = 10, Max = 40, Weight = 0.15 }
    }
};

var dateFrom = new DateTime(2025, 3, 1, 8, 0, 0);
var dateTo = new DateTime(2025, 3, 7, 20, 0, 0);

// slots = gen.GenerateSlots(dateFrom, dateTo);
// referees = gen.GenerateReferess();


Console.WriteLine($"Načteno {referees.Count} rozhodčích a {slots.Count} slotů.");

/*
 * ---------------------------------------
 * Inicializace hlavni konfigurace
 * ---------------------------------------
 */
var config = new SolverConfiguration()
{
    MaxWasteTime = TimeSpan.FromHours(6),
    RefereePostTime = TimeSpan.FromMinutes(60),
    RefereePrepTime = TimeSpan.FromMinutes(90),
    DistanceFactor = 1.0,
    OverRankFactor = 1.0,
    UnderRankFactor = 1.0,  
    UnassignedCost = 1_000_000.0,
    RelativeGap = 0.01
};

/*
 * ---------------------------------------
 * Geolokace a inicializace tabulky tras
 * ---------------------------------------
 */
var slotLocations = slots.Select(s => s.Location).Distinct().ToList();
var refereeLocations = referees.Select(r => r.Location).Distinct().ToList();
var allLocations = slotLocations.Union(refereeLocations).Distinct().ToList();
var distanceTable = new DistanceTable();

Console.WriteLine($"Budování matice vzdáleností přes OSRM - toto může chvíli trvat...");
await distanceTable.Initialize(allLocations);


/*
 * ---------------------------------------
 * Inicializace pomocnych trid a solveru
 * ---------------------------------------
 */
var routeSolver = new RouteSolver(distanceTable, config);
var conflictChecker = new ConflictChecker(distanceTable, config);
var costCalculator = new CostCalculator(distanceTable, config);

/*
 * ---------------------------------------
 * Inicializace Hybrid
 * ---------------------------------------
 */
var hybridSolver = new LnsHybridSolver(
    referees, conflictChecker, costCalculator, config)
{
    NeighborhoodSize = 20,
    MaxIterations = 300,
    SwitchAfterNoImprovement = 20,
    BbIterationTimeLimit = TimeSpan.FromSeconds(2),
    HcAttempts = 5,
    HcIterations = 200,
    HcMoves = 30
};

/*
 * ---------------------------------------
 * Inicializace LNS
 * ---------------------------------------
 */
var lnsSolver = new LnsBbSolver(
    referees,
    conflictChecker,
    costCalculator,
    config
)
{
    NeighborhoodSize = 20,     // počet slotů k uvolnění per iteraci
    MaxIterations = 300,    // celkový počet iterací
    NoImprovementLimit = 30,     // restart po X neúspěších
    IterationTimeLimit = TimeSpan.FromSeconds(2), // limit mini B&B
    Strategy = LnsBbSolver.NeighborhoodStrategy.CostWeighted
};


/*
 * ---------------------------------------
 * Inicializace Branch & Bound (mega optimalizace)
 * ---------------------------------------
 */
var bbSolverOpt = new BBSolver(
    referees,
    conflictChecker,
    costCalculator,
    config,
    timeLimit: TimeSpan.FromSeconds(10) // omezeni casu behu B&B
);


/*
 * ---------------------------------------
 * Inicializace Branch & Bound (puvodni verze)
 * ---------------------------------------
 */
var bbSolver = new BBSolverOLD(
    referees,
    conflictChecker,
    costCalculator,
    timeLimit: TimeSpan.FromSeconds(10) // omezeni casu behu B&B
);

/*
 * ---------------------------------------
 * Inicializace Hill Climbing
 * ---------------------------------------
 */
var hcSolver = new HCSolver(
    referees,
    conflictChecker,
    costCalculator
)
{
    MaxAttempts = 150,
    MaxIterations = slots.Count * 2,
    MaxMoves = 60
};

Stopwatch sw = new Stopwatch();

Console.WriteLine("==============================================================");
Console.WriteLine();
Console.WriteLine("                  Spoustim Hybrid (LNS + BB + HC)...");
Console.WriteLine();
Console.WriteLine("==============================================================");

sw.Restart();
State resultHybrid = hybridSolver.Solve(slots);
sw.Stop();
var hybridTime = sw.ElapsedMilliseconds;
CsvExporter.SaveState($"{rootDirectory}\\resultHYB.csv", resultHybrid, routeSolver);

Console.WriteLine("==============================================================");
Console.WriteLine();
Console.WriteLine("                  Spoustim Branch & Bound (LNS)...");
Console.WriteLine();
Console.WriteLine("==============================================================");

sw.Restart();
State resultLNS = lnsSolver.Solve(slots);
sw.Stop();
var lnsTime = sw.ElapsedMilliseconds;
CsvExporter.SaveState($"{rootDirectory}\\resultLNS.csv", resultLNS, routeSolver);


Console.WriteLine("==============================================================");
Console.WriteLine();
Console.WriteLine("                  Spoustim Branch & Bound (zakladni)...");
Console.WriteLine();
Console.WriteLine("==============================================================");

sw.Restart();
State resultBB = bbSolver.Solve(slots);
sw.Stop();
var bbTime = sw.ElapsedMilliseconds;
CsvExporter.SaveState($"{rootDirectory}\\resultBB.csv", resultBB, routeSolver);

Console.WriteLine("==============================================================");
Console.WriteLine();
Console.WriteLine("                  Spoustim Branch & Bound (optim)...");
Console.WriteLine();
Console.WriteLine("==============================================================");

sw.Restart();
State resultBBOpt = bbSolverOpt.Solve(slots);
sw.Stop();
var bbOptTime = sw.ElapsedMilliseconds;
CsvExporter.SaveState($"{rootDirectory}\\resultBB.csv", resultBBOpt, routeSolver);

Console.WriteLine("==============================================================");
Console.WriteLine();
Console.WriteLine("                  Spoustim Hill Climbing...");
Console.WriteLine();
Console.WriteLine("==============================================================");

sw.Restart();
State resultHC = hcSolver.Solve(slots);
sw.Stop();
CsvExporter.SaveState($"{rootDirectory}\\resultHC.csv", resultHC, routeSolver);
var hcTime = sw.ElapsedMilliseconds;

Console.WriteLine("==============================================================");
Console.WriteLine();
Console.WriteLine("                  VÝSLEDKY ALGORITMŮ");
Console.WriteLine();
Console.WriteLine("==============================================================");

Console.WriteLine();
Console.WriteLine("Hybrid (LNS + BB + HC)");
Console.WriteLine("+----------------------+------------------------------+");
Console.WriteLine($"| Celková cena         | {costCalculator.TotalCost(resultHybrid),-28:F2} |");
Console.WriteLine($"| Prázdné sloty        | {resultHybrid.GetEmptySlots().Count(),-28} |");
Console.WriteLine($"| Zlepšující iterace BB| {($"{hybridSolver.BbImprovements}/{hybridSolver.TotalIterations}"),-28} |");
Console.WriteLine($"| Zlepšující iterace HC| {($"{hybridSolver.HcImprovements}/{hybridSolver.TotalIterations}"),-28} |");
Console.WriteLine($"| Čas                  | {($"{hybridTime} ms"),-28} |");
Console.WriteLine("+----------------------+------------------------------+");

Console.WriteLine();
Console.WriteLine("Branc & Bound (LNS)");
Console.WriteLine("+----------------------+------------------------------+");
Console.WriteLine($"| Celková cena         | {costCalculator.TotalCost(resultLNS),-28:F2} |");
Console.WriteLine($"| Prázdné sloty        | {resultLNS.GetEmptySlots().Count(),-28} |");
Console.WriteLine($"| Zlepšující iterace   | {($"{lnsSolver.ImprovingIterations}/{lnsSolver.TotalIterations}"),-28} |");
Console.WriteLine($"| Čas                  | {($"{lnsTime} ms"),-28} |");
Console.WriteLine("+----------------------+------------------------------+");

Console.WriteLine();
Console.WriteLine("Branch & Bound (optim)");
Console.WriteLine("+----------------------+------------------------------+");
Console.WriteLine($"| Celková cena         | {costCalculator.TotalCost(resultBBOpt),-28:F2} |");
Console.WriteLine($"| Prázdné sloty        | {resultBBOpt.GetEmptySlots().Count(),-28} |");
Console.WriteLine($"| Prozkoumáno uzlů     | {bbSolverOpt.NodesExplored,-28} |");
Console.WriteLine($"| Čas                  | {($"{bbOptTime} ms"),-28} |");
Console.WriteLine("+----------------------+------------------------------+");

Console.WriteLine();
Console.WriteLine("Branch & Bound (basic)");
Console.WriteLine("+----------------------+------------------------------+");
Console.WriteLine($"| Celková cena         | {costCalculator.TotalCost(resultBB),-28:F2} |");
Console.WriteLine($"| Prázdné sloty        | {resultBB.GetEmptySlots().Count(),-28} |");
Console.WriteLine($"| Prozkoumáno uzlů     | {bbSolver.NodesExplored,-28} |");
Console.WriteLine($"| Čas                  | {($"{bbTime} ms"),-28} |");
Console.WriteLine("+----------------------+------------------------------+");

Console.WriteLine();
Console.WriteLine("Hill Climbing");
Console.WriteLine("+----------------------+------------------------------+");
Console.WriteLine($"| Celková cena         | {costCalculator.TotalCost(resultHC),-28:F2} |");
Console.WriteLine($"| Prázdné sloty        | {resultHC.GetEmptySlots().Count(),-28} |");
Console.WriteLine($"| Čas                  | {($"{hcTime} ms"),-28} |");
Console.WriteLine("+----------------------+------------------------------+");




