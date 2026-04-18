using Diplomka.Model;

namespace Diplomka.Solver
{
    /// <summary>
    /// Branch &amp; Bound solver pro Referee Assignment Problem.
    ///
    /// ── Architektura ──────────────────────────────────────────────────────────────
    ///
    ///  1. GREEDY FÁZE
    ///     Sestaví počáteční přiřazení greedy heuristikou (seřazení slotů podle
    ///     obtížnosti, vždy přiřaď nejlevnějšího způsobilého rozhodčího).
    ///     Toto řešení slouží jako horní mez (UB) pro B&amp;B.
    ///
    ///  2. REPAIR FÁZE
    ///     Pokud greedy nestihne obsadit všechny sloty (nedostatek rozhodčích
    ///     v daném časovém okně), opravná heuristika se pokusí vyplnit prázdná
    ///     místa chain-repair technikou.
    ///
    ///  3. BRANCH &amp; BOUND FÁZE
    ///     Prohledává stavový prostor DFS s agresivním ořezáváním:
    ///
    ///     Větvení:
    ///       Vždy se vybere slot s nejmenším počtem způsobilých rozhodčích
    ///       (MRV – Minimum Remaining Values). Tím se odhalují mrtvé větve
    ///       co nejdříve a strom se prořezává efektivněji.
    ///
    ///     Dolní mez (Lower Bound):
    ///       LB = cena dosud přiřazených slotů
    ///            + pro každý zbývající slot: min(cena přes VŠECHNY způsobilé rozhodčí)
    ///       Tato mez je přípustná (admissible) – nikdy nepřeceňuje skutečné optimum.
    ///
    ///     Ořezávání:
    ///       Pokud LB ≥ UB (nejlepší dosud nalezené řešení), větev se opustí.
    ///
    ///     Časový limit:
    ///       Kvůli velikosti prostoru (≈ 300 slotů × 29 rozhodčích) je k dispozici
    ///       konfigurovatelný časový limit. Po jeho uplynutí se vrátí nejlepší
    ///       dosud nalezené řešení.
    ///
    /// ── Složitost ─────────────────────────────────────────────────────────────────
    ///   Nejhorší případ exponenciální, v praxi silně prořezáno UB z greedy fáze.
    /// </summary>
    public class BranchAndBoundSolver
    {
        private readonly List<Referee>  _referees;
        private readonly TimeSpan       _timeLimit;

        private State?   _bestState;
        private double   _bestCost;
        private long     _nodesExplored;
        private DateTime _startTime;

        public long   NodesExplored => _nodesExplored;
        public double BestCost      => _bestCost;

        /// <param name="referees">Všichni dostupní rozhodčí.</param>
        /// <param name="timeLimit">Maximální doba optimalizace (default: 30 s).</param>
        public BranchAndBoundSolver(IEnumerable<Referee> referees, TimeSpan? timeLimit = null)
        {
            _referees  = referees.ToList();
            _timeLimit = timeLimit ?? TimeSpan.FromSeconds(30);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Veřejné API
        // ─────────────────────────────────────────────────────────────────────────

        public State Solve(IEnumerable<Slot> slots)
        {
            _startTime    = DateTime.UtcNow;
            _nodesExplored = 0;

            var slotList = slots.ToList();

            // ── Krok 1: Greedy počáteční řešení ──────────────────────────────────
            Console.WriteLine("[B&B] Spouštím greedy heuristiku...");
            var greedyState = new GreedySolver(_referees).Solve(slotList);

            // ── Krok 2: Opravná heuristika ────────────────────────────────────────
            var emptyAfterGreedy = greedyState.GetEmptySlots();
            if (emptyAfterGreedy.Count > 0)
            {
                Console.WriteLine($"[B&B] Greedy neodsailo {emptyAfterGreedy.Count} slotů – spouštím repair...");
                greedyState = new RepairHeuristic(_referees).Repair(greedyState);
            }

            _bestState = greedyState;
            _bestCost  = CostCalculator.TotalCost(greedyState);
            Console.WriteLine($"[B&B] Počáteční cena (greedy): {_bestCost:F2}");

            // ── Krok 3: Branch & Bound ────────────────────────────────────────────
            Console.WriteLine($"[B&B] Spouštím B&B (limit: {_timeLimit.TotalSeconds} s)...");

            // Prázdný výchozí stav pro B&B (přidáme sloty bez přiřazení)
            var initialState = new State();
            foreach (var slot in slotList)
                initialState.AddSlot(slot);

            Branch(initialState, 0.0, slotList);

            Console.WriteLine($"[B&B] Hotovo. Prozkoumáno uzlů: {_nodesExplored}, nejlepší cena: {_bestCost:F2}");
            return _bestState!;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Jádro B&B – rekurzivní DFS
        // ─────────────────────────────────────────────────────────────────────────

        private void Branch(State state, double costSoFar, List<Slot> allSlots)
        {
            // ── Kontrola časového limitu ──────────────────────────────────────────
            if (DateTime.UtcNow - _startTime > _timeLimit)
                return;

            _nodesExplored++;

            // ── Výběr neohodnoceného slotu (MRV) ─────────────────────────────────
            var emptySlots = state.GetEmptySlots();

            if (emptySlots.Count == 0)
            {
                // Listový uzel – kompletní přiřazení
                if (costSoFar < _bestCost)
                {
                    _bestCost  = costSoFar;
                    _bestState = (State)state.Clone();
                    Console.WriteLine($"[B&B]   Nové optimum: {_bestCost:F2} (uzlů: {_nodesExplored})");
                }
                return;
            }

            // MRV: vyber slot s nejméně způsobilými rozhodčími
            var chosenSlot = SelectSlotMRV(state, emptySlots);

            // Způsobilí rozhodčí seřazení od nejlevnějšího (best-first větvení)
            var candidates = ConflictChecker
                .GetEligibleReferees(state, chosenSlot, _referees)
                .OrderBy(r => CostCalculator.AssignmentCost(chosenSlot, r))
                .ToList();

            // ── Ořezávání: žádný způsobilý rozhodčí → slepá ulička ───────────────
            if (candidates.Count == 0)
                return;

            foreach (var referee in candidates)
            {
                double assignCost = CostCalculator.AssignmentCost(chosenSlot, referee);
                double newCost    = costSoFar + assignCost;

                // Přiřaď rozhodčího
                state.SetReferee(chosenSlot, referee);

                // Dolní mez pro zbývající sloty
                var remaining = state.GetEmptySlots();
                double lb = newCost + CostCalculator.LowerBoundForSlots(remaining, _referees);

                // ── Ořezávání (bound) ─────────────────────────────────────────────
                if (lb < _bestCost)
                {
                    Branch(state, newCost, allSlots);
                }

                // Backtrack
                state.ClearSlot(chosenSlot);

                // Časový limit
                if (DateTime.UtcNow - _startTime > _timeLimit)
                    return;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // MRV: slot s nejmenším počtem způsobilých rozhodčích
        // ─────────────────────────────────────────────────────────────────────────

        private Slot SelectSlotMRV(State state, List<Slot> emptySlots)
        {
            Slot? best     = null;
            int   bestCount = int.MaxValue;

            foreach (var slot in emptySlots)
            {
                int count = ConflictChecker
                    .GetEligibleReferees(state, slot, _referees)
                    .Count;

                // Nejprve sloty s nejmenším výběrem; při shodě preferuj vyšší RequiredRank
                if (count < bestCount || (count == bestCount && slot.RequiredRank > (best?.RequiredRank ?? 0)))
                {
                    bestCount = count;
                    best      = slot;
                }
            }

            return best!;
        }
    }
}
