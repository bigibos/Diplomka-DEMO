using Diplomka.Entity;
using System.Globalization;

namespace Diplomka.Solver
{
    /// <summary>
    /// Rozdělí sloty do týdenních bloků a každý blok řeší samostatným BBSolverem.
    ///
    /// ── Proč to funguje ───────────────────────────────────────────────────────────
    ///   Sloty v různých týdnech nemohou způsobit časový konflikt, takže ConflictChecker
    ///   každý blok řeší nezávisle. Výsledek je identický s řešením celého problému najednou.
    ///
    /// ── Kontext mezi týdny ────────────────────────────────────────────────────────
    ///   RouteSolver se při výpočtu ceny dívá na předchozí slot rozhodčího.
    ///   Aby slot v pondělí správně zohlednil páteční slot z předchozího týdne,
    ///   každý blok dostane "kontextový stav" s posledním slotem každého rozhodčího
    ///   z předchozích bloků. Tyto kontextové sloty vstupují pouze do výpočtu ceny,
    ///   nikoliv do výsledného přiřazení.
    ///
    /// ── Paralelismus ──────────────────────────────────────────────────────────────
    ///   Každý týden je řešen v samostatném Task (thread pool).
    ///   DistanceTable je sdílená read-only, State a BBSolver jsou per-vlákno.
    ///   Kontext předchozího týdne je předán jako immutable snapshot.
    ///
    /// ── Omezení ───────────────────────────────────────────────────────────────────
    ///   Při plně paralelním běhu kontext předchozího týdne není k dispozici
    ///   (týdny běží současně). Pro přesný kontext použijte RunSequentialAsync,
    ///   pro maximální rychlost RunParallelAsync.
    /// </summary>
    public class WeeklyDecompositionSolver : ISolver
    {
        private readonly List<Referee> _referees;
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SortedCandidateTable _candidateTable;
        private readonly TimeSpan _timeLimitPerWeek;

        public WeeklyDecompositionSolver(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            SortedCandidateTable candidateTable,
            TimeSpan? timeLimitPerWeek = null)
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _candidateTable = candidateTable;
            _timeLimitPerWeek = timeLimitPerWeek ?? TimeSpan.FromSeconds(30);
        }

        // ── ISolver ───────────────────────────────────────────────────────────────

        public State Solve(State state) => Solve(state.GetSlots());

        /// <summary>
        /// Sekvenční běh – každý týden dostane kontext z předchozího.
        /// Lepší kvalita výpočtu ceny na přelomu týdnů.
        /// </summary>
        public State Solve(IEnumerable<Slot> slots)
        {
            var weeks = GroupByWeek(slots);
            var merged = new State();
            State? previousWeekState = null;

            foreach (var (weekKey, weekSlots) in weeks.OrderBy(w => w.Key))
            {
                Console.WriteLine($"[Weekly] Řeším týden {weekKey} ({weekSlots.Count} slotů)...");

                var contextState = BuildContextState(weekSlots, previousWeekState);
                var solver = CreateSolver();
                var weekResult = solver.Solve(contextState);

                // Do výsledku přidáme pouze přiřazení pro sloty tohoto týdne
                MergeWeekResult(merged, weekResult, weekSlots);
                previousWeekState = weekResult;

                Console.WriteLine($"[Weekly] Týden {weekKey} hotov. Cena: {_costCalculator.TotalCost(weekResult):F2}");
            }

            return merged;
        }

        /// <summary>
        /// Paralelní běh – všechny týdny běží současně bez křížového kontextu.
        /// Rychlejší, mírně horší cena pro sloty na začátku každého týdne.
        /// </summary>
        public async Task<State> RunParallelAsync(IEnumerable<Slot> slots)
        {
            var weeks = GroupByWeek(slots);

            Console.WriteLine($"[Weekly] Spouštím {weeks.Count} týdnů paralelně...");

            var tasks = weeks.Select(kvp => Task.Run(() =>
            {
                Console.WriteLine($"[Weekly] Start týdne {kvp.Key} ({kvp.Value.Count} slotů)...");

                // Každý Task dostane vlastní stav – bez sdíleného kontextu
                var initialState = new State();
                foreach (var slot in kvp.Value)
                    initialState.AddSlot(slot);

                var result = CreateSolver().Solve(initialState);

                Console.WriteLine($"[Weekly] Konec týdne {kvp.Key}. Cena: {_costCalculator.TotalCost(result):F2}");
                return (WeekKey: kvp.Key, Slots: kvp.Value, Result: result);
            })).ToList();

            var weekResults = await Task.WhenAll(tasks);

            // Sloučení všech výsledků
            var merged = new State();
            foreach (var (_, weekSlots, weekResult) in weekResults.OrderBy(w => w.WeekKey))
                MergeWeekResult(merged, weekResult, weekSlots);

            return merged;
        }

        // ── Pomocné metody ────────────────────────────────────────────────────────

        /// <summary>
        /// Sestaví počáteční stav pro daný týden.
        /// Pokud existuje kontext (předchozí týden), přidá poslední slot každého
        /// rozhodčího jako "phantom slot" – RouteSolver ho uvidí jako předchůdce,
        /// ale do výsledku se nepromítne.
        /// </summary>
        private State BuildContextState(List<Slot> weekSlots, State? previousWeekState)
        {
            var state = new State();

            // Přidáme phantom sloty z předchozího týdne jako kontext
            if (previousWeekState != null)
            {
                var lastSlotPerReferee = previousWeekState
                    .Where(kv => kv.Value != null)
                    .GroupBy(kv => kv.Value!)
                    .Select(g => (
                        Referee: g.Key,
                        LastSlot: g.OrderBy(kv => kv.Key.Start).Last().Key
                    ));

                foreach (var (referee, lastSlot) in lastSlotPerReferee)
                {
                    state.AddSlot(lastSlot);
                    state.SetReferee(lastSlot, referee);
                }
            }

            foreach (var slot in weekSlots)
                state.AddSlot(slot);

            return state;
        }

        /// <summary>
        /// Přenese přiřazení z týdenního výsledku do finálního stavu.
        /// Phantom (kontextové) sloty se přeskočí.
        /// </summary>
        private static void MergeWeekResult(State merged, State weekResult, List<Slot> weekSlots)
        {
            var weekSlotSet = new HashSet<Slot>(weekSlots);

            foreach (var (slot, referee) in weekResult)
            {
                if (!weekSlotSet.Contains(slot))
                    continue; // přeskočíme phantom sloty

                merged.AddSlot(slot);

                if (referee != null)
                    merged.SetReferee(slot, referee);
            }
        }

        private Dictionary<int, List<Slot>> GroupByWeek(IEnumerable<Slot> slots)
        {
            return slots
                .GroupBy(s => ISOWeek.GetWeekOfYear(s.Start))
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private BBSolverOLD CreateSolver() =>
            new BBSolverOLD(_referees, _conflictChecker, _costCalculator, _candidateTable, _timeLimitPerWeek);
    }
}