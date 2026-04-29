using Diplomka.Entity;

namespace Diplomka.Solver
{
    /// <summary>
    /// Branch &amp; Bound solver pro Referee Assignment Problem.
    ///
    /// ── Změny oproti původní verzi ─────────────────────────────────────────────
    ///
    ///  Nyní používá BbSearchIndex pro:
    ///
    ///  1. INKREMENTÁLNÍ MRV
    ///     eligibleCounts[] = pole udržované po celou dobu DFS.
    ///     Při assign: OnAssign() sníží počty pro kolidující sloty → O(|konflikty|).
    ///     Při unassign: OnUnassign() je vrátí zpět.
    ///     Výběr MRV slotu: O(S) sken pole místo O(S × R × k).
    ///
    ///  2. RYCHLÝ CANASSIGN
    ///     Místo ConflictChecker.Overlaps() (dict-lookup + DateTime aritmetika)
    ///     používáme BbSearchIndex.CanAssignFast() s předpočítanou bool[][] maticí.
    ///
    ///  3. RYCHLÝ LOWER BOUND
    ///     FastLowerBound() = O(S) sum přes _slotMinCost[].
    ///     TightLowerBound() = O(S × K_avg) s časnou terminací – volitelné.
    ///
    ///  4. KANDIDÁTI PRE-SORTED
    ///     Iterujeme přes _sortedCandidates[] z indexu – rankově způsobilí,
    ///     seřazeni ASC podle ceny, bez nutnosti LINQ a alokací.
    ///
    /// ── Výsledek ──────────────────────────────────────────────────────────────
    ///   Každý DFS uzel: z O(S × R × k) na O(S + |konflikty|).
    ///   Pro S=500, R=200, k=5 → 100× zrychlení per uzel.
    /// </summary>
    public class BBSolver : ISolver
    {
        private readonly List<Referee> _referees;
        private readonly TimeSpan _timeLimit;
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly BbSearchIndex _index;

        private State? _bestState;
        private double _bestCost;
        private long _nodesExplored;
        private DateTime _startTime;
        private bool _timeLimitExceeded;

        // Inkrementální MRV pole – alokujeme jednou, sdílíme přes celý DFS strom
        // (DFS je single-threaded, takže je to bezpečné)
        private int[] _eligibleCounts = Array.Empty<int>();

        public long NodesExplored => _nodesExplored;
        public double BestCost => _bestCost;

        public BBSolver(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            BbSearchIndex index,
            TimeSpan? timeLimit = null)
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _index = index;
            _timeLimit = timeLimit ?? TimeSpan.FromSeconds(60);
        }

        // ── Veřejné API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Vstupní bod: slot-state je již vytvořen (warm-start z greedy/HC).
        /// </summary>
        public State Solve(State state)
        {
            _startTime = DateTime.UtcNow;
            _nodesExplored = 0;
            _timeLimitExceeded = false;

            _bestState = (State)state.Clone();
            _bestCost = _costCalculator.TotalCost(state);
            Console.WriteLine($"[B&B] Warm start cena: {_bestCost:F2}");

            // Inicializace inkrementálních počítadel
            _eligibleCounts = _index.InitEligibleCounts();

            // Prázdný stav pro DFS (sloty bez přiřazení)
            var emptyState = new State();
            foreach (var slot in state.GetSlots())
                emptyState.AddSlot(slot);

            Dfs(emptyState, 0.0);

            Console.WriteLine($"[B&B] Hotovo. Uzlů: {_nodesExplored}, cena: {_bestCost:F2}");
            return _bestState!;
        }

        /// <summary>
        /// Vstupní bod: solve přímo ze seznamu slotů (greedy warm-start uvnitř).
        /// </summary>
        public State Solve(IEnumerable<Slot> slots)
        {
            _startTime = DateTime.UtcNow;
            _nodesExplored = 0;
            _timeLimitExceeded = false;

            var slotList = slots.ToList();

            // Greedy warm-start (horní mez)
            Console.WriteLine("[B&B] Spouštím greedy heuristiku...");
            var greedyState = new GreedySolver(_referees, _conflictChecker, _costCalculator,
                                               new SortedCandidateTable(_costCalculator, _conflictChecker))
                                  .Solve(slotList);

            var emptyAfterGreedy = greedyState.GetEmptySlots().ToList();
            if (emptyAfterGreedy.Count > 0)
            {
                Console.WriteLine($"[B&B] Greedy nezaplnil {emptyAfterGreedy.Count} slotů – repair...");
                greedyState = new RepairHeuristic(_referees, _conflictChecker, _costCalculator)
                                  .Repair(greedyState);
            }

            _bestState = greedyState;
            _bestCost = _costCalculator.TotalCost(greedyState);
            Console.WriteLine($"[B&B] Počáteční cena (greedy): {_bestCost:F2}");

            Console.WriteLine($"[B&B] Spouštím B&B (limit: {_timeLimit.TotalSeconds} s)...");

            // Inicializace inkrementálních počítadel
            _eligibleCounts = _index.InitEligibleCounts();

            var initialState = new State();
            foreach (var slot in slotList)
                initialState.AddSlot(slot);

            Dfs(initialState, 0.0);

            Console.WriteLine($"[B&B] Hotovo. Prozkoumáno uzlů: {_nodesExplored}, cena: {_bestCost:F2}");
            return _bestState!;
        }

        // ── Jádro DFS ─────────────────────────────────────────────────────────────

        private void Dfs(State state, double totalCost)
        {
            _nodesExplored++;
            var emptySlots = state.GetEmptySlots().ToList();

            // Kontrola časového limitu každých 500 uzlů
            if (_nodesExplored % 5000 == 0)
            {
                Console.WriteLine($"[B&B] Uzel: {_nodesExplored}, Cas {DateTime.UtcNow - _startTime}, Cena: {totalCost}, Nevyplneno: {emptySlots.Count}");
                if (DateTime.UtcNow - _startTime > _timeLimit)
                    _timeLimitExceeded = true;
            }

            if (_timeLimitExceeded) return;


            // ── Leaf: všechny sloty obsazeny ──────────────────────────────────────
            if (emptySlots.Count == 0)
            {
                if (totalCost < _bestCost)
                {
                    _bestCost = totalCost;
                    _bestState = (State)state.Clone();
                    Console.WriteLine($"[B&B] Nové optimum: {_bestCost:F2} (uzlů: {_nodesExplored})");
                }
                return;
            }

            // ── Fast admissible lower bound ────────────────────────────────────────
            // Rychlá kontrola: i kdyby každý prázdný slot dostal nejlevnějšího možného
            // rozhodčího (bez ohledu na kolize), jsme stále horší než best? → prořez.
            double fastLb = _index.FastLowerBound(emptySlots, totalCost);
            if (fastLb >= _bestCost) return;

            // ── MRV výběr slotu ───────────────────────────────────────────────────
            // O(S) díky inkrementálním eligibleCounts[]
            int mrvIdx = _index.SelectMrvSlotIndex(emptySlots, _eligibleCounts);
            var mrvSlot = _index.GetSlot(mrvIdx);

            // Pruning: žádný způsobilý rozhodčí pro MRV slot
            if (_eligibleCounts[mrvIdx] == 0) return;

            // ── Iterace přes kandidáty (předseřazeni ASC podle ceny) ──────────────
            var candidates = _index.GetSortedCandidates(mrvIdx);

            foreach (var (refIdx, assignmentCost) in candidates)
            {
                var referee = _index.GetReferee(refIdx);

                // Rychlá kontrola časových kolizí pomocí ConflictMatrix
                if (!_index.CanAssignFast(mrvIdx, state.GetSlotsByReferee(referee)))
                    continue;

                double newTotalCost = totalCost + assignmentCost;



                // Tighter pruning: s tímto přiřazením + optimistický LB pro zbytek
                // Používáme TightLowerBound jen pokud je strom stále velký
                double lb;

                if (emptySlots.Count > 20)
                {
                    // Pro velký strom: fast (O(S)), dostatečně tightní
                    lb = newTotalCost + _index.FastLowerBound(
                        emptySlots.Where(s => s != mrvSlot), 0);
                }
                else
                {
                    // Pro malý strom: tighter (O(S × K)), lepší pruning
                    state.SetReferee(mrvSlot, referee);
                    lb = _index.TightLowerBound(state.GetEmptySlots(), newTotalCost, state);
                    state.ClearSlot(mrvSlot);
                    if (lb >= _bestCost) continue;
                    // Přiřadíme znovu níže
                }

                if (lb >= _bestCost) continue;

                // ── Assign ────────────────────────────────────────────────────────
                state.SetReferee(mrvSlot, referee);
                _index.OnAssign(mrvIdx, refIdx, _eligibleCounts);

                Dfs(state, newTotalCost);

                // ── Backtrack ─────────────────────────────────────────────────────
                state.ClearSlot(mrvSlot);
                _index.OnUnassign(mrvIdx, refIdx, _eligibleCounts);

                if (_timeLimitExceeded) return;
            }
        }
    }
}