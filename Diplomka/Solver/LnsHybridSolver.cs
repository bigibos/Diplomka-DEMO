using Diplomka.Entity;
using Diplomka.Solver;

namespace Diplomka.Solver
{
    /// <summary>
    /// LNS hybrid kombinující Hill Climbing a B&amp;B jako repair operátory.
    ///
    /// ── Myšlenka ──────────────────────────────────────────────────────────────────
    ///
    ///   Každý repair operátor má jiné vlastnosti:
    ///
    ///   HC repair  – rychlý (ms), heuristický, může uvíznout v lokálním optimu,
    ///               ale díky rychlosti pokryje hodně neighbourhoodů
    ///
    ///   B&amp;B repair – pomalý (stovky ms), zaručeně optimální pro daný neighbourhood,
    ///               ale méně iterací za stejný čas
    ///
    ///   Adaptivní strategie střídá oba operátory podle výkonnosti:
    ///   pokud HC opakovaně nevylepšuje, přepne na B&amp;B (a obráceně).
    ///   Tím získáme rychlost HC + záruky B&amp;B tam kde je potřeba.
    ///
    /// ── Architektura ──────────────────────────────────────────────────────────────
    ///
    ///   1. Greedy warm start
    ///   2. Iterace:
    ///      a) Destroy  – uvolni k slotů (CostWeighted / Clustered)
    ///      b) Repair   – HC nebo B&amp;B podle aktuálního módu
    ///      c) Merge    – zkombinuj fixní přiřazení + výsledek repair
    ///      d) Accept   – přijmi zlepšení; sleduj výkonnost obou operátorů
    ///      e) Switch   – přepni operátor pokud aktuální selhává
    ///
    /// </summary>
    public class LnsHybridSolver : ISolver
    {
        // ─── Závislosti ───────────────────────────────────────────────────────────
        private readonly List<Referee> _referees;
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SolverConfiguration _config;
        private readonly Random _rng = new();

        // ─── Konfigurace ──────────────────────────────────────────────────────────

        /// Pocet slotu uvolnovanych v iteraci
        public int NeighborhoodSize { get; set; } = 10;

        /// <summary>Maximální počet iterací LNS.</summary>
        public int MaxIterations { get; set; } = 200;

        /// <summary>Přepni operátor po tomto počtu neúspěšných iterací za sebou.</summary>
        public int SwitchAfterNoImprovement { get; set; } = 20;

        /// <summary>Časový limit B&amp;B repair na jednu iteraci.</summary>
        public TimeSpan BbIterationTimeLimit { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>Konfigurace HC repair (počet pokusů a kroků).</summary>
        public int HcAttempts { get; set; } = 5;
        public int HcIterations { get; set; } = 200;
        public int HcMoves { get; set; } = 30;

        // ─── Statistiky ───────────────────────────────────────────────────────────
        public double BestCost { get; private set; }
        public int TotalIterations { get; private set; }
        public int HcImprovements { get; private set; }
        public int BbImprovements { get; private set; }

        private enum RepairOperator { HC, BB }

        // ─── Konstruktor ──────────────────────────────────────────────────────────
        public LnsHybridSolver(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            SolverConfiguration config)
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _config = config;
        }

        // ─── Veřejné vstupní body ─────────────────────────────────────────────────

        public State Solve(IEnumerable<Slot> slots)
        {
            Console.WriteLine("[Hybrid] Spouštím greedy warm start...");
            var initial = new GreedySolver(_referees, _conflictChecker, _costCalculator)
                .Solve(slots.ToList());
            return Solve(initial);
        }

        public State Solve(State initialState)
        {
            TotalIterations = 0;
            HcImprovements = 0;
            BbImprovements = 0;

            var best = (State)initialState.Clone();
            BestCost = _costCalculator.TotalCost(best);
            var current = best;

            var activeOperator = RepairOperator.HC;
            int noImprovementStreak = 0;

            Console.WriteLine($"[Hybrid] Počáteční cena: {BestCost:F2} | Prázdné: {best.GetEmptySlots().Count()}");
            Console.WriteLine($"[Hybrid] k={NeighborhoodSize}, iterace={MaxIterations}, přepnutí po {SwitchAfterNoImprovement} neúspěších");

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                TotalIterations++;

                // ── Destroy: uvolni k slotů ────────────────────────────────────────
                var relaxed = SelectNeighborhood(current);
                var available = GetAvailableReferees(current, relaxed);

                if (available.Count == 0)
                {
                    noImprovementStreak++;
                    continue;
                }

                // ── Repair: HC nebo B&B ────────────────────────────────────────────
                State localResult = activeOperator == RepairOperator.HC
                    ? RepairWithHC(relaxed, available)
                    : RepairWithBB(relaxed, available);

                // ── Merge ─────────────────────────────────────────────────────────
                State merged = MergeStates(current, relaxed, localResult);
                double mergedCost = _costCalculator.TotalCost(merged);

                // ── Accept ────────────────────────────────────────────────────────
                string operatorLabel = activeOperator == RepairOperator.HC ? "HC" : "B&B";

                if (mergedCost < BestCost)
                {
                    double delta = BestCost - mergedCost;
                    BestCost = mergedCost;
                    best = (State)merged.Clone();
                    current = best;
                    noImprovementStreak = 0;

                    if (activeOperator == RepairOperator.HC) HcImprovements++;
                    else BbImprovements++;

                    Console.WriteLine($"[Hybrid] Iter {iter + 1,4} [{operatorLabel}]: ✓  " +
                                      $"cena {BestCost:F2}  (−{delta:F2})  " +
                                      $"prázdné: {best.GetEmptySlots().Count()}");
                }
                else
                {
                    noImprovementStreak++;

                    // ── Adaptivní přepnutí operátoru ──────────────────────────────
                    if (noImprovementStreak >= SwitchAfterNoImprovement)
                    {
                        var next = activeOperator == RepairOperator.HC
                            ? RepairOperator.BB
                            : RepairOperator.HC;

                        Console.WriteLine($"[Hybrid] Iter {iter + 1,4}: přepnutí {operatorLabel} → " +
                                          $"{(next == RepairOperator.HC ? "HC" : "B&B")} " +
                                          $"(po {noImprovementStreak} neúspěších, nejlepší: {BestCost:F2})");

                        activeOperator = next;
                        noImprovementStreak = 0;
                        current = (State)best.Clone(); // restart z nejlepšího
                    }
                }
            }

            Console.WriteLine($"[Hybrid] Hotovo. Nejlepší cena: {BestCost:F2} | " +
                              $"HC zlepšení: {HcImprovements} | B&B zlepšení: {BbImprovements} | " +
                              $"Iterace: {TotalIterations}");

            return best;
        }

        // ─── Repair: HC ───────────────────────────────────────────────────────────

        /// <summary>
        /// Opraví uvolněné sloty pomocí omezeného HC.
        /// HC operuje pouze nad k uvolněnými sloty a dostupnými rozhodčími —
        /// díky tomu je rychlý a nevyžaduje žádný restart od nuly.
        /// </summary>
        private State RepairWithHC(List<Slot> relaxed, List<Referee> available)
        {
            // Greedy přiřazení jako počáteční bod pro HC
            var hcInitial = new GreedySolver(available, _conflictChecker, _costCalculator)
                .Solve(relaxed);

            var hcSolver = new HCSolver(available, _conflictChecker, _costCalculator)
            {
                MaxAttempts = HcAttempts,
                MaxIterations = HcIterations,
                MaxMoves = HcMoves
            };

            return hcSolver.Solve(hcInitial);
        }

        // ─── Repair: B&B ─────────────────────────────────────────────────────────

        /// <summary>
        /// Opraví uvolněné sloty pomocí mini B&amp;B.
        /// Díky malému k (≤15) B&amp;B dokončí v desetinách sekundy a zaručeně
        /// najde lokální optimum pro daný neighbourhood.
        /// </summary>
        private State RepairWithBB(List<Slot> relaxed, List<Referee> available)
        {
            var bbSolver = new BBSolver(
                available,
                _conflictChecker,
                _costCalculator,
                timeLimit: BbIterationTimeLimit
            );

            return bbSolver.Solve(relaxed);
        }

        // ─── Destroy fáze ─────────────────────────────────────────────────────────

        private List<Slot> SelectNeighborhood(State current)
        {
            var all = current.GetSlots();
            int k = Math.Min(NeighborhoodSize, all.Count);

            // Střídáme CostWeighted a Clustered pro diverzitu
            return _rng.Next(2) == 0
                ? SelectCostWeighted(current, all, k)
                : SelectClustered(all, k);
        }

        private List<Slot> SelectCostWeighted(State current, List<Slot> all, int k)
        {
            var weighted = all.Select(s =>
            {
                var referee = current.GetRefereeForSlot(s);
                double cost = referee != null
                    ? _costCalculator.AssignmentCost(s, referee)
                    : _config.UnassignedCost;
                return (slot: s, weight: cost + 1.0);
            }).ToList();

            var selected = new List<Slot>(k);
            var pool = weighted.ToList();

            for (int i = 0; i < k && pool.Count > 0; i++)
            {
                double total = pool.Sum(x => x.weight);
                double roll = _rng.NextDouble() * total;
                double cumulative = 0;
                int idx = 0;

                for (int j = 0; j < pool.Count; j++)
                {
                    cumulative += pool[j].weight;
                    if (roll <= cumulative) { idx = j; break; }
                    idx = j;
                }

                selected.Add(pool[idx].slot);
                pool.RemoveAt(idx);
            }

            return selected;
        }

        private List<Slot> SelectClustered(List<Slot> all, int k)
        {
            var anchor = all[_rng.Next(all.Count)];
            return all
                .OrderBy(s => Math.Abs((s.Start - anchor.Start).TotalMinutes))
                .Take(k)
                .ToList();
        }

        // ─── Restrict fáze ────────────────────────────────────────────────────────

        private List<Referee> GetAvailableReferees(State current, List<Slot> relaxed)
        {
            var relaxedSet = new HashSet<Slot>(relaxed);
            var blocked = new HashSet<Referee>();

            foreach (var slot in current.GetSlots())
            {
                if (relaxedSet.Contains(slot)) continue;
                var referee = current.GetRefereeForSlot(slot);
                if (referee == null || blocked.Contains(referee)) continue;

                foreach (var r in relaxed)
                {
                    if (_conflictChecker.Overlaps(slot, r))
                    {
                        blocked.Add(referee);
                        break;
                    }
                }
            }

            return _referees.Where(r => !blocked.Contains(r)).ToList();
        }

        // ─── Merge fáze ───────────────────────────────────────────────────────────

        private State MergeStates(State current, List<Slot> relaxed, State localResult)
        {
            var merged = new State();
            var relaxedSet = new HashSet<Slot>(relaxed);

            foreach (var slot in current.GetSlots())
                merged.AddSlot(slot);

            foreach (var slot in current.GetSlots())
            {
                if (relaxedSet.Contains(slot)) continue;
                var referee = current.GetRefereeForSlot(slot);
                if (referee != null)
                    merged.SetReferee(slot, referee);
            }

            foreach (var slot in relaxed)
            {
                var referee = localResult.GetRefereeForSlot(slot);
                if (referee != null)
                    merged.SetReferee(slot, referee);
            }

            return merged;
        }
    }
}