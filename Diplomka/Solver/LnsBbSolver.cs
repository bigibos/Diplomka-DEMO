using Diplomka.Entity;
using Diplomka.Solver;

namespace Diplomka.Solver
{
    /// <summary>
    /// Large Neighbourhood Search solver s B&amp;B jako lokálním optimalizátorem.
    ///
    /// ── Motivace ──────────────────────────────────────────────────────────────────
    ///
    ///   Standalone B&amp;B selhává na středních a velkých instancích, protože prohledává
    ///   exponenciální prostor od nuly. HC naopak uvízne v lokálních optimech.
    ///
    ///   LNS-B&amp;B kombinuje výhody obou:
    ///     • HC/Greedy poskytuje počáteční feasible řešení
    ///     • V každé iteraci se uvolní malá skupina k slotů (destroy)
    ///     • B&amp;B optimálně přiřadí těchto k slotů (repair) – hloubka k místo S
    ///     • Opakováním přes různé neighbourhoods se pokryje celý prostor řešení
    ///
    /// ── Architektura ──────────────────────────────────────────────────────────────
    ///
    ///   1. GREEDY FÁZE  – počáteční přiřazení (warm start)
    ///   2. ITERACE:
    ///      a) Destroy  – vyber k slotů strategicky (Random / CostWeighted / Clustered)
    ///      b) Restrict – urči dostupné rozhodčí (ti, kteří nejsou blokováni fixními přiřazeními)
    ///      c) Repair   – mini B&amp;B na k slotech s dostupnými rozhodčími
    ///      d) Merge    – zkombinuj fixní přiřazení + výsledek mini B&amp;B
    ///      e) Accept   – přijmi zlepšení; po sérii neúspěchů restart z nejlepšího
    ///
    /// ── Proč to funguje ───────────────────────────────────────────────────────────
    ///
    ///   Mini B&amp;B s k=10 prohledává stromy hloubky 10 (ne 60 nebo 200).
    ///   Pro k≤15 typicky dokončí v desetinách sekundy a zaručeně najde
    ///   lokální optimum v daném neighbourhoodu.
    ///
    /// </summary>
    public class LnsBbSolver : ISolver
    {
        // ─── Závislosti ───────────────────────────────────────────────────────────
        private readonly List<Referee> _referees;
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SolverConfiguration _config;
        private readonly Random _rng = new();

        // ─── Konfigurace ──────────────────────────────────────────────────────────

        /// <summary>Počet slotů uvolňovaných v každé iteraci (k).</summary>
        public int NeighborhoodSize { get; set; } = 10;

        /// <summary>Maximální počet iterací LNS.</summary>
        public int MaxIterations { get; set; } = 150;

        /// <summary>Restart z nejlepšího řešení po tomto počtu neúspěšných iterací.</summary>
        public int NoImprovementLimit { get; set; } = 30;

        /// <summary>Časový limit mini B&amp;B na jednu iteraci.</summary>
        public TimeSpan IterationTimeLimit { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>Strategie výběru neighbourhoodu.</summary>
        public NeighborhoodStrategy Strategy { get; set; } = NeighborhoodStrategy.CostWeighted;

        public enum NeighborhoodStrategy
        {
            /// <summary>Náhodný výběr k slotů.</summary>
            Random,

            /// <summary>
            /// Bias k drahým přiřazením – sloty s vyšší AssignmentCost
            /// mají vyšší pravděpodobnost výběru.
            /// Soustředí LNS na nejproblematičtější přiřazení.
            /// </summary>
            CostWeighted,

            /// <summary>
            /// Vyber kotevní slot náhodně, pak k-1 časově nejbližších.
            /// Cluster temporálně příbuzných slotů → B&amp;B lépe přeorganizuje trasy.
            /// </summary>
            Clustered
        }

        // ─── Statistiky ───────────────────────────────────────────────────────────
        public int TotalIterations { get; private set; }
        public int ImprovingIterations { get; private set; }
        public double BestCost { get; private set; }

        // ─── Konstruktor ──────────────────────────────────────────────────────────
        public LnsBbSolver(
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

        /// <summary>Solve od nuly: greedy warm start → LNS.</summary>
        public State Solve(IEnumerable<Slot> slots)
        {
            Console.WriteLine("[LNS] Spouštím greedy warm start...");
            var initial = new GreedySolver(_referees, _conflictChecker, _costCalculator)
                .Solve(slots.ToList());
            return Solve(initial);
        }

        /// <summary>Solve s externím warm startem.</summary>
        public State Solve(State initialState)
        {
            TotalIterations = 0;
            ImprovingIterations = 0;

            var best = (State)initialState.Clone();
            BestCost = _costCalculator.TotalCost(best);
            var current = best;

            int emptyStart = best.GetEmptySlots().Count();
            Console.WriteLine($"[LNS] Počáteční cena: {BestCost:F2} | Prázdné: {emptyStart}");
            Console.WriteLine($"[LNS] k={NeighborhoodSize}, max iterací={MaxIterations}, " +
                              $"limit/iter={IterationTimeLimit.TotalSeconds}s, " +
                              $"strategie={Strategy}");

            int noImprovementCount = 0;

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                TotalIterations++;

                // ── Destroy: výběr k slotů k uvolnění ─────────────────────────────
                var relaxed = SelectNeighborhood(current);

                // ── Restrict: rozhodčí dostupní pro tento neighbourhood ────────────
                // Rozhodčí blokovaný fixním přiřazením kolidujícím s některým z k slotů
                // nesmí být nabídnut mini B&B (předejdeme dvojímu přiřazení ve stejný čas).
                var available = GetAvailableReferees(current, relaxed);

                if (available.Count == 0)
                {
                    // Extrémní případ: všichni rozhodčí jsou blokováni fixními přiřazeními.
                    // Zkus jiný neighbourhood příští iteraci.
                    noImprovementCount++;
                    continue;
                }

                // ── Repair: mini B&B optimalizuje k slotů ─────────────────────────
                var miniSolver = new BBSolver(
                    available,
                    _conflictChecker,
                    _costCalculator,
                    _config,
                    timeLimit: IterationTimeLimit
                );

                State localResult = miniSolver.Solve(relaxed);

                // ── Merge: fixní přiřazení + výsledek mini B&B ────────────────────
                State merged = MergeStates(current, relaxed, localResult);
                double mergedCost = _costCalculator.TotalCost(merged);

                // ── Accept: přijmi zlepšení ────────────────────────────────────────
                if (mergedCost < BestCost)
                {
                    double delta = BestCost - mergedCost;
                    BestCost = mergedCost;
                    best = (State)merged.Clone();
                    current = best;
                    noImprovementCount = 0;
                    ImprovingIterations++;

                    int emptyNow = best.GetEmptySlots().Count();
                    Console.WriteLine($"#### [LNS] Iter {iter + 1,4}: ✓  cena {BestCost:F2}  " +
                                      $"(−{delta:F2})  prázdné: {emptyNow}  " +
                                      $"[uzlů B&B: {miniSolver.NodesExplored:N0}]");
                }
                else
                {
                    noImprovementCount++;

                    // Restart po sérii neúspěchů – escape z oblasti lokálního optima
                    if (noImprovementCount >= NoImprovementLimit)
                    {
                        Console.WriteLine($"#### [LNS] Iter {iter + 1,4}:    restart " +
                                          $"(po {noImprovementCount} neúspěších, " +
                                          $"nejlepší: {BestCost:F2})");
                        current = (State)best.Clone();
                        noImprovementCount = 0;
                    }
                }
            }

            int emptyFinal = best.GetEmptySlots().Count();
            Console.WriteLine($"#### [LNS] Hotovo. Nejlepší cena: {BestCost:F2} | " +
                              $"Prázdné: {emptyFinal} | " +
                              $"Zlepšující iterace: {ImprovingIterations}/{TotalIterations}");

            return best;
        }

        // ─── Destroy fáze: strategie výběru neighbourhoodu ───────────────────────

        private List<Slot> SelectNeighborhood(State current)
        {
            var all = current.GetSlots();
            int k = Math.Min(NeighborhoodSize, all.Count);

            return Strategy switch
            {
                NeighborhoodStrategy.Random => SelectRandom(all, k),
                NeighborhoodStrategy.CostWeighted => SelectCostWeighted(current, all, k),
                NeighborhoodStrategy.Clustered => SelectClustered(all, k),
                _ => SelectRandom(all, k)
            };
        }

        /// <summary>Uniformně náhodný výběr k slotů.</summary>
        private List<Slot> SelectRandom(List<Slot> all, int k)
        {
            return all.OrderBy(_ => _rng.Next()).Take(k).ToList();
        }

        /// <summary>
        /// Vážený výběr: dražší přiřazení jsou preferována.
        /// Sloty s cenou přiřazení c mají váhu (c + ε), takže pravděpodobnost výběru
        /// roste s cenou přiřazení → LNS se soustředí tam, kde je největší potenciál.
        /// </summary>
        private List<Slot> SelectCostWeighted(State current, List<Slot> all, int k)
        {
            var weighted = all.Select(s =>
            {
                var referee = current.GetRefereeForSlot(s);
                double cost = referee != null
                    ? _costCalculator.AssignmentCost(s, referee)
                    : _config.UnassignedCost; // prázdné sloty mají vysokou prioritu
                return (slot: s, weight: cost + 1.0); // +1 aby nikdy nebyla váha 0
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

        /// <summary>
        /// Cluster kolem náhodné kotvy: vyber slot náhodně, pak k-1 časově nejbližších.
        /// Temporálně příbuzné sloty sdílejí rozhodčí → B&B může lépe optimalizovat trasy.
        /// </summary>
        private List<Slot> SelectClustered(List<Slot> all, int k)
        {
            var anchor = all[_rng.Next(all.Count)];
            return all
                .OrderBy(s => Math.Abs((s.Start - anchor.Start).TotalMinutes))
                .Take(k)
                .ToList();
        }

        // ─── Restrict fáze: dostupní rozhodčí ────────────────────────────────────

        /// <summary>
        /// Vrátí rozhodčí, kteří jsou k dispozici pro optimalizaci uvolněného neighbourhoodu.
        ///
        /// Rozhodčí je BLOKOVÁN pokud má fixní přiřazení (mimo neighbourhood), které
        /// se časově překrývá s některým ze k uvolněných slotů. Přiřadili bychom ho
        /// dvakrát ve stejný čas, což by způsobilo konflikt.
        ///
        /// Rozhodčí je DOSTUPNÝ pokud žádné takové fixní přiřazení nemá – může být
        /// volně přiřazen mini B&B k libovolnému z k slotů.
        /// </summary>
        private List<Referee> GetAvailableReferees(State current, List<Slot> relaxed)
        {
            var relaxedSet = new HashSet<Slot>(relaxed);
            var blocked = new HashSet<Referee>();

            foreach (var slot in current.GetSlots())
            {
                if (relaxedSet.Contains(slot)) continue; // přeskočíme uvolněné sloty
                var referee = current.GetRefereeForSlot(slot);
                if (referee == null) continue;
                if (blocked.Contains(referee)) continue; // already blocked

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

        /// <summary>
        /// Zkombinuje:
        ///   • fixní přiřazení z <paramref name="current"/> (sloty mimo neighbourhood)
        ///   • výsledek mini B&B z <paramref name="localResult"/> (k uvolněných slotů)
        /// Výsledkem je kompletní State se všemi sloty.
        /// </summary>
        private State MergeStates(State current, List<Slot> relaxed, State localResult)
        {
            var merged = new State();
            var relaxedSet = new HashSet<Slot>(relaxed);

            // Přidáme všechny sloty z původního řešení
            foreach (var slot in current.GetSlots())
                merged.AddSlot(slot);

            // Fixní přiřazení (sloty mimo neighbourhood)
            foreach (var slot in current.GetSlots())
            {
                if (relaxedSet.Contains(slot)) continue;
                var referee = current.GetRefereeForSlot(slot);
                if (referee != null)
                    merged.SetReferee(slot, referee);
            }

            // Výsledek mini B&B pro k uvolněných slotů
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