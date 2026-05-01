using Diplomka.Entity;

namespace Diplomka.Solver
{
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
        public State Solve(State warmStart)
        {
            _startTime = DateTime.UtcNow;
            _nodesExplored = 0;
            _timeLimitExceeded = false;

            _bestState = (State)warmStart.Clone();
            _bestCost = _costCalculator.TotalCost(warmStart);

            Console.WriteLine($"[B&B] Warm start cena: {_bestCost:F2}");

            var initialState = new State();
            var emptySlots = warmStart.GetSlots(); // všechny sloty, žádné přiřazení
            foreach (var slot in emptySlots)
                initialState.AddSlot(slot);

            Dfs(initialState, 0.0, emptySlots);

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
            _bestCost = _costCalculator.TotalCost(greedyState);
            Console.WriteLine($"[B&B] Počáteční cena (greedy): {_bestCost:F2}");

            // Spousteni B&B pro zlepseni reseni z greedy faze
            Console.WriteLine($"[B&B] Spouštím B&B (limit: {_timeLimit.TotalSeconds} s)...");

            // Prázdný výchozí stav pro B&B (přidáme sloty bez přiřazení)
            var initialState = new State();
            foreach (var slot in slotList)
                initialState.AddSlot(slot);

            Dfs(initialState, 0.0, slotList);

            Console.WriteLine($"[B&B] Hotovo. Prozkoumáno uzlů: {_nodesExplored}, nejlepší cena: {_bestCost:F2}");
            return _bestState!;
        }

        /*
         * Hlavni jadro B&B
         * - Rekurzivni Depth-First Search prohledava stavovy prostor
         * - V kazdem kroku se vybere slot s nejmensim poctem zpusobilych rozhodcich (MRV - Minimum Remaining Values)
         * - Kandidati pro tento slot jsou serazeni podle ceny (best-first), aby se rychleji dosahovalo lepsich reseni
         */
        private void Dfs(State state, double costSoFar, List<Slot> emptySlots)
        {
            _nodesExplored++;

            // Kontrola pro vymereny cas
            if (_nodesExplored % 500 == 0)
            {
                if (DateTime.UtcNow - _startTime > _timeLimit)
                {
                    _timeLimitExceeded = true;
                    return;
                }
            }

            // Prubezny vypis
            if (_nodesExplored % 5_000 == 0)
                Console.WriteLine($"[B&B] Prozkoumáno uzlů: {_nodesExplored}");

            if (_timeLimitExceeded)
                return;

            // Kontrola otima pri zaplneni vsech slotu
            if (emptySlots.Count == 0)
            {
                if (costSoFar < _bestCost)
                {
                    _bestCost = costSoFar;
                    _bestState = (State)state.Clone();
                    Console.WriteLine($"[B&B] Nové optimum: {_bestCost:F2}");
                }
                return;
            }

            // MRV - slot s nejmensim poctem zpusobilych rozhodcich
            var (mrvSlot, candidates, lbAll) = SelectSlotMRV(state, emptySlots);

            // Pruning - neni zadny vhodny rozhodci (kontrola PRED vypoctem cen)
            if (candidates.Count == 0)
                return;

            // Serazeni kandidatu podle ceny (best-first)
            var sorted = new (Referee r, double cost)[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                sorted[i] = (candidates[i], _costCalculator.AssignmentCost(mrvSlot, candidates[i]));
            Array.Sort(sorted, (a, b) => a.cost.CompareTo(b.cost));

            var remaining = new List<Slot>(emptySlots);
            remaining.Remove(mrvSlot);

            foreach (var (referee, assignCost) in sorted)
            {
                double newCost = costSoFar + assignCost;

                // LB = newCost + (lbAll - minCostForMrvSlot)
                // lbAll obsahuje minimum pro mrvSlot — odečteme ho, přičteme assignCost
                double lb = newCost + lbAll - sorted[0].cost;
                if (lb >= _bestCost) continue; // prune

                state.SetReferee(mrvSlot, referee);
                Dfs(state, newCost, remaining);
                state.ClearSlot(mrvSlot);

                if (_timeLimitExceeded) return;
            }
        }

        /*
         * MRV - Minimum Remaining Values
         * - Vyber slotu s nejmensim poctem zpusobilych rozhodcich
         */
        private (Slot slot, List<Referee> candidates, double lowerBound) SelectSlotMRV(
            State state, List<Slot> emptySlots)
        {
            Slot? best = null;
            List<Referee> bestCandidates = new();
            int bestCount = int.MaxValue;
            double lbSum = 0.0;  // <-- přidáme součet minim

            foreach (var slot in emptySlots)
            {
                var candidates = _conflictChecker.GetEligibleReferees(state, slot, _referees);

                if (candidates.Count == 0)
                    return (slot, candidates, double.MaxValue); // mrtvá větev

                // Minimum pro tento slot = nejlevnější dostupné přiřazení
                double minCost = candidates
                    .Min(r => _costCalculator.AssignmentCost(slot, r));
                lbSum += minCost;

                if (candidates.Count < bestCount ||
                    (candidates.Count == bestCount &&
                     slot.RequiredRank > (best?.RequiredRank ?? 0)))
                {
                    bestCount = candidates.Count;
                    best = slot;
                    bestCandidates = candidates;
                }
            }

            return (best!, bestCandidates, lbSum);
        }
    }
}