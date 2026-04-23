using Diplomka.Entity;

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
    public class BBSolver : ISolver
    {
        private readonly List<Referee> _referees;
        private readonly TimeSpan _timeLimit;

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;

        private State? _bestState;
        private double _bestCost;
        private long _nodesExplored;
        private DateTime _startTime;

        private bool _timeLimitExceeded;

        public long NodesExplored => _nodesExplored;
        public double BestCost => _bestCost;


        public BBSolver(
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
            _startTime = DateTime.UtcNow;
            _nodesExplored = 0;
            _timeLimitExceeded = false;

            var slotList = state.GetSlots();

            // Warm start = jen lepší horní mez, DFS začíná od nuly
            _bestState = (State)state.Clone();
            _bestCost = _costCalculator.TotalCost(state);

            Console.WriteLine($"[B&B] Warm start cena: {_bestCost:F2}");

            var emptyInitial = new State();
            foreach (var slot in slotList)
                emptyInitial.AddSlot(slot);

            Dfs(emptyInitial, 0.0);

            Console.WriteLine($"[B&B] Hotovo. Uzlů: {_nodesExplored}, cena: {_bestCost:F2}");
            return _bestState!;
        }

        /*
         * Hlavni metoda pro spusteni B&B solveru
         */
        public State Solve(IEnumerable<Slot> slots)
        {
            _startTime = DateTime.UtcNow;
            _nodesExplored = 0;
            _timeLimitExceeded = false;

            var slotList = slots.ToList();

            // Jednoduche (greedy) pocatecni reseni pro upper bound
            Console.WriteLine("[B&B] Spouštím greedy heuristiku...");
            var greedyState = new GreedySolver(_referees, _conflictChecker, _costCalculator).Solve(slotList);

            // Pripadna oprava greedy reseni
            var emptyAfterGreedy = greedyState.GetEmptySlots().ToList();
            if (emptyAfterGreedy.Count > 0)
            {
                Console.WriteLine($"[B&B] Greedy nezaplnil {emptyAfterGreedy.Count} slotů – spouštím repair...");
                greedyState = new RepairHeuristic(_referees, _conflictChecker, _costCalculator).Repair(greedyState);
            }

            _bestState = greedyState;
            _bestCost  = _costCalculator.TotalCost(greedyState);
            Console.WriteLine($"[B&B] Počáteční cena (greedy): {_bestCost:F2}");

            // Spousteni B&B pro zlepseni reseni z greedy faze
            Console.WriteLine($"[B&B] Spouštím B&B (limit: {_timeLimit.TotalSeconds} s)...");

            // Prázdný výchozí stav pro B&B (přidáme sloty bez přiřazení)
            var initialState = new State();
            foreach (var slot in slotList)
                initialState.AddSlot(slot);

            Dfs(initialState, 0.0);

            Console.WriteLine($"[B&B] Hotovo. Prozkoumáno uzlů: {_nodesExplored}, nejlepší cena: {_bestCost:F2}");
            return _bestState!;
        }

        /*
         * Hlavni jadro B&B
         * - Rekurzivni Depth-First Search prohledava stavovy prostor
         * - V kazdem kroku se vybere slot s nejmensim poctem zpusobilych rozhodcich (MRV - Minimum Remaining Values)
         * - Kandidati pro tento slot jsou serazeni podle ceny (best-first), aby se rychleji dosahovalo lepsich reseni
         */
        private void Dfs(State state, double totalCost)
        {
            _nodesExplored++;

            if (_nodesExplored % 500 == 0)
            {
                // Kontrolujeme casove omezeni
                if (DateTime.UtcNow - _startTime > _timeLimit)
                    _timeLimitExceeded = true;
            }

            if (_timeLimitExceeded)
                return;

            // Vyber prazdnych slotu
            var emptySlots = state.GetEmptySlots().ToList();

            if (emptySlots.Count == 0)
            {
                // Nalezeni lepsiho reseni
                if (totalCost < _bestCost)
                {
                    _bestCost  = totalCost;
                    _bestState = (State)state.Clone();
                    Console.WriteLine($"[B&B] Nové optimum: {_bestCost:F2} (uzlů: {_nodesExplored})");
                }
                return;
            }

            // MRV - slot s nejmensim poctem zpusobilych rozhodcich
            var (mrvSlot, candidateRefs) = SelectSlotMRV(state, emptySlots);

            // Serazeni kandidatu podle ceny (best-first)
            candidateRefs = candidateRefs
                .OrderBy(r => _costCalculator.AssignmentCost(state, mrvSlot, r))
                .ToList();

            // Pruning - neni zadny vhodny rozhodci
            if (candidateRefs.Count == 0)
                return;

            foreach (var referee in candidateRefs)
            {
                double assignmentCost = _costCalculator.AssignmentCost(state, mrvSlot, referee);
                double newTotalCost = totalCost + assignmentCost;

                // Nastaveni rozhodciho ke slotu
                state.SetReferee(mrvSlot, referee);

                // Vypocet dolni meze (lower bound) pro aktualni stav
                var remainingSlots = state.GetEmptySlots();
                double lowerBound = newTotalCost + _costCalculator.LowerBoundForSlots(state, remainingSlots, _referees, _conflictChecker);

                // Pokud dolni mez je horsi nez aktualni nejlepsi reseni, prohledavame tuto vetvu
                if (lowerBound < _bestCost)
                {
                    Dfs(state, newTotalCost);
                }

                // Backtrack - odstraneni rozhodciho ze slotu pro prohledani dalsich kandidatu
                state.ClearSlot(mrvSlot);

                // Kontrola casu
                if (_timeLimitExceeded)
                    return;
            }
        }

        /*
         * MRV - Minimum Remaining Values
         * - Vyber slotu s nejmensim poctem zpusobilych rozhodcich
         */
        private (Slot slot, List<Referee> candidates) SelectSlotMRV(State state, List<Slot> emptySlots)
        {
            Slot? best = null;
            List<Referee> bestCandidates = new List<Referee>();
            int bestCount = int.MaxValue;

            foreach (var slot in emptySlots)
            {
                var candidates = _conflictChecker.GetEligibleReferees(state, slot, _referees);
                int count = candidates.Count;

                // Nejprve sloty s nejmensim poctem vhodnych rozhodcich, pak sloty s nevyssim pozadovanym rankem
                if (count < bestCount || (count == bestCount && slot.RequiredRank > (best?.RequiredRank ?? 0)))
                {
                    bestCount = count;
                    best = slot;
                    bestCandidates = candidates;
                }
            }

            return (best!, bestCandidates);
        }
    }
}
