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
        private readonly List<Referee> _referees;
        private readonly TimeSpan _timeLimit;

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;

        private State? bestState;
        private double bestCost;
        private long nodesExplored;
        private DateTime startTime;

        public long NodesExplored => nodesExplored;
        public double BestCost => bestCost;


        public BranchAndBoundSolver(
            IEnumerable<Referee> referees, 
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            TimeSpan? timeLimit = null
            )
        {
            _referees = referees.ToList();
            
            _conflictChecker = conflictChecker; 
            _costCalculator = costCalculator;
            
            _timeLimit = timeLimit ?? TimeSpan.FromSeconds(30);
        }

        public State Solve(State state)
        {
            startTime = DateTime.UtcNow;
            nodesExplored = 0;

            var slotList = state.GetSlots();

            // Warm start = jen lepší horní mez, DFS začíná od nuly
            bestState = (State)state.Clone();
            bestCost = _costCalculator.TotalCost(state);

            Console.WriteLine($"[B&B] Warm start cena: {bestCost:F2}");

            var emptyInitial = new State();
            foreach (var slot in slotList)
                emptyInitial.AddSlot(slot);

            Dfs(emptyInitial, 0.0, slotList);

            Console.WriteLine($"[B&B] Hotovo. Uzlů: {nodesExplored}, cena: {bestCost:F2}");
            return bestState!;
        }

        /*
         * Hlavni metod pro spusteni B&B solveru
         */
        public State Solve(IEnumerable<Slot> slots)
        {
            startTime = DateTime.UtcNow;
            nodesExplored = 0;

            var slotList = slots.ToList();

            // Jednoduche (greedy) pocatecni reseni pro upper bound
            Console.WriteLine("[B&B] Spouštím greedy heuristiku...");
            var greedyState = new GreedySolver(_referees, _conflictChecker, _costCalculator).Solve(slotList);

            // Pripadna oprava greedy reseni
            var emptyAfterGreedy = greedyState.GetEmptySlots();
            if (emptyAfterGreedy.Count > 0)
            {
                Console.WriteLine($"[B&B] Greedy nezaplnil {emptyAfterGreedy.Count} slotů – spouštím repair...");
                greedyState = new RepairHeuristic(_referees, _conflictChecker, _costCalculator).Repair(greedyState);
            }

            bestState = greedyState;
            bestCost  = _costCalculator.TotalCost(greedyState);
            Console.WriteLine($"[B&B] Počáteční cena (greedy): {bestCost:F2}");

            // Spousteni B&B pro zlepseni reseni z greedy faze
            Console.WriteLine($"[B&B] Spouštím B&B (limit: {_timeLimit.TotalSeconds} s)...");

            // Prázdný výchozí stav pro B&B (přidáme sloty bez přiřazení)
            var initialState = new State();
            foreach (var slot in slotList)
                initialState.AddSlot(slot);

            Dfs(initialState, 0.0, slotList);

            Console.WriteLine($"[B&B] Hotovo. Prozkoumáno uzlů: {nodesExplored}, nejlepší cena: {bestCost:F2}");
            return bestState!;
        }

        /*
         * Hlavni jadro B&B
         * - Rekurzivni Depth-First Search prohledava stavovy prostor
         * - V kazdem kroku se vybere slot s nejmensim poctem zpusobilych rozhodcich (MRV - Minimum Remaining Values)
         * - Kandidati pro tento slot jsou serazeni podle ceny (best-first), aby se rychleji dosahovalo lepsich reseni
         */
        private void Dfs(State state, double totalCost, List<Slot> slots)
        {
            // Kontrolujeme casove omezeni
            if (DateTime.UtcNow - startTime > _timeLimit)
                return;

            nodesExplored++;

            // Vyber prazdnych slotu
            var emptySlots = state.GetEmptySlots();

            if (emptySlots.Count == 0)
            {
                // Nalezeni lepsiho reseni
                if (totalCost < bestCost)
                {
                    bestCost  = totalCost;
                    bestState = (State)state.Clone();
                    Console.WriteLine($"[B&B] Nové optimum: {bestCost:F2} (uzlů: {nodesExplored})");
                }
                return;
            }

            // MRV - slot s nejmensim poctem zpusobilych rozhodcich
            var mrvSlot = SelectSlotMRV(state, emptySlots);

            // Serazeni kandidatu podle ceny (best-first)
            var candidateRefs = _conflictChecker
                .GetEligibleReferees(state, mrvSlot, _referees)
                .OrderBy(r => _costCalculator.AssignmentCost(mrvSlot, r))
                .ToList();

            // Pruning - neni zadny vhodny rozhodci
            if (candidateRefs.Count == 0)
                return;

            foreach (var referee in candidateRefs)
            {
                double assignmentCost = _costCalculator.AssignmentCost(mrvSlot, referee);
                double newTotalCost = totalCost + assignmentCost;

                // Nastaveni rozhodciho ke slotu
                state.SetReferee(mrvSlot, referee);

                // Vypocet dolni meze (lower bound) pro aktualni stav
                var remainingSlots = state.GetEmptySlots();
                double lowerBound = newTotalCost + _costCalculator.LowerBoundForSlots(remainingSlots, _referees);

                // Pokud dolni mez je horsi nez aktualni nejlepsi reseni, prohledavame tuto vetvu
                if (lowerBound < bestCost)
                {
                    Dfs(state, newTotalCost, slots);
                }

                // Backtrack - odstraneni rozhodciho ze slotu pro prohledani dalsich kandidatu
                state.ClearSlot(mrvSlot);

                // Kontrola casu
                if (DateTime.UtcNow - startTime > _timeLimit)
                    return;
            }
        }

        /*
         * MRV - Minimum Remaining Values
         * - Vyber slotu s nejmensim poctem zpusobilych rozhodcich
         */
        private Slot SelectSlotMRV(State state, List<Slot> emptySlots)
        {
            Slot? best = null;
            int bestCount = int.MaxValue;

            foreach (var slot in emptySlots)
            {
                int count = _conflictChecker
                    .GetEligibleReferees(state, slot, _referees)
                    .Count;

                // Nejprve sloty s nejmensim poctem vhodnych rozhodcich, pak sloty s nevyssim pozadovanym rankem
                if (count < bestCount || (count == bestCount && slot.RequiredRank > (best?.RequiredRank ?? 0)))
                {
                    bestCount = count;
                    best = slot;
                }
            }

            return best!;
        }
    }
}
