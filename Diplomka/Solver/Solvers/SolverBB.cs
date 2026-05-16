using Diplomka.Entity;
using Diplomka.Solver.Config;
using Diplomka.Solver.Services;

namespace Diplomka.Solver.Solvers
{
    /// <summary>
    /// Hlavní optimalizační algoritmus.
    ///     - Branch & Bound s warm startem pro horní mez
    ///     - Deterministický (exaktní)
    ///     - V základu pro stejné vstupy vždy najde stejné řešení, ALE s použítím <see cref="SolverHC"/> pro warm start to neplatí
    /// </summary>
    public class SolverBB : SolverBase
    {
        private readonly List<Referee> _referees;
        private readonly TimeSpan _timeLimit;

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SolverConfiguration _config;

        private State? _bestState;
        private double _bestCost;
        private long _nodesExplored;
        private DateTime _startTime;

        private bool _timeLimitExceeded;

        public long NodesExplored => _nodesExplored;
        public double BestCost => _bestCost;


        public SolverBB(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            SolverConfiguration config,
            TimeSpan? timeLimit = null
            )
        {
            _referees = referees.ToList();

            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _config = config;

            _timeLimit = timeLimit ?? TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Přetížení hlavní metody algoritmu <see cref="Solve(IEnumerable{Slot})"/>
        /// Částečné řešení vybrané části stavu. Využívá se především v <see cref="SolverLNS"/>
        /// </summary>
        /// <param name="context">Stav pro kontext přiřazení</param>
        /// <param name="slotsToOptimize">Sloty ze kontextu, které se mají optimalizovat</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        public State Solve(State context, List<Slot>? slotsToOptimize = null)
        {
            _startTime = DateTime.UtcNow;
            _nodesExplored = 0;
            _timeLimitExceeded = false;

            var slots = slotsToOptimize ?? context.GetSlots();
            var relaxedSet = new HashSet<Slot>(slots);

            var initialState = (State)context.Clone();
            foreach (var slot in slots)
                initialState.ClearSlot(slot);

            // fixedCost = pouze cena fixních přiřazení, bez prázdných uvolněných slotů
            double fixedCost = context.GetSlots()
                .Where(s => !relaxedSet.Contains(s))
                .Sum(s => _costCalculator.AssignmentCost(s, context.GetReferee(s)));

            _bestState = (State)context.Clone();
            _bestCost = _costCalculator.TotalCost(context); // warm start = celá cena včetně neighbourhood

            Dfs(initialState, fixedCost, slots);

            return _bestState!;
        }

        /// <summary>
        /// Přetížení hlavní metody algoritmu <see cref="Solve(IEnumerable{Slot})"/>
        /// Principiálně podobné jako <see cref="Solve(State, List{Slot}?)"/> akorát bez přidaného seznamu slotů
        /// </summary>
        /// <param name="state">Stov s počátečním řešením</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        override public State Solve(State state)
        {
            return Solve(state, null);
        }

        /// <summary>
        /// Hlavní metoda algoritmu.
        ///     1) Vytvoří se stav pomocí <see cref="SolverGreedy"/>
        ///     2) Vytvoří se stav pomocí <see cref="SolverHC"/>
        ///     3) Vybere se lepší počáteční stav pro warm start a horní mez
        ///     4) Branch & Bound se rekurzivním voláním <see cref="Dfs(State, double, List{Slot})"/> pokusí počáteční stav zlepšit
        /// </summary>
        /// <param name="slots">Sloty k zaplnění</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        override public State Solve(IEnumerable<Slot> slots)
        {
            _startTime = DateTime.UtcNow;
            _nodesExplored = 0;
            _timeLimitExceeded = false;

            var slotList = slots.ToList();

            Emit(new SolverEvent.InfoEvent($"Vytáření počátečních stavů"));

            // Fáze 1: Greedy jako základ
            var greedy = new SolverGreedy(_referees, _conflictChecker, _costCalculator);
            greedy.OnEvent += Forward;
            var greedyState = greedy.Solve(slotList);
            var greedyCost = _costCalculator.TotalCost(greedyState);

            // Fáze 2: HC pro lepší upper bound
            var hc = new SolverHC(_referees, _conflictChecker, _costCalculator, _config);
            hc.OnEvent += Forward;
            var hcState = hc.Solve(slots);
            var hcCost = _costCalculator.TotalCost(hcState);

            // Vybereme lepší z greedy a HC jako warm start
            if (hcCost < greedyCost)
            {
                Emit(new SolverEvent.InfoEvent($"Zvolen HC"));
                _bestState = hcState;
                _bestCost = hcCost;
            }
            else
            {
                Emit(new SolverEvent.InfoEvent($"Zvolen Greedy"));
                _bestState = greedyState;
                _bestCost = greedyCost;
            }

            Emit(new SolverEvent.StartEvent(BestCost));

            // Fáze 3: B&B se pokusí zlepšit warm start
            var initialState = new State();
            foreach (var slot in slotList)
                initialState.AddSlot(slot);

            Dfs(initialState, 0.0, slotList);

            Emit(new SolverEvent.FinishEvent(BestCost));

            // Console.WriteLine($"[B&B] Hotovo. Prozkoumáno uzlů: {_nodesExplored}, nejlepší cena: {_bestCost:F2}");
            return _bestState!;
        }


        /// <summary>
        /// Jádro Branch & Bound.
        /// Jedná se o rekurzivní Depth-First Search algoritmus, pro prohledávání větví stavového prostoru
        ///     1) Kontrola jestli jsou všechny sloty zaplněny - pokud ano tak návrat z rekurze
        ///     2) Výběr MRV slotu pomocí <see cref="SelectSlotMRV(State, List{Slot})"/>
        ///     3) Odřezávání (pruning) pokud neexistují žádní vhodní kandidátí pro MRV slot
        ///     4) Seřazení kandidátů podle ceny přiřazení (best-first)
        ///     5) Odhad a kontrola spodní meze - pokud je spodní mez vyšší než nejlepší dosavadní cena tak odřezává větev (pruning)
        ///     6) Přiřazení rozhodčího ke slotu a další rekurzivní volání
        /// </summary>
        /// <param name="state">Stav pro postupné přiřazování v rámci rekurzí</param>
        /// <param name="costSoFar">Doposavaď nalezená cena stavu</param>
        /// <param name="emptySlots">Doposud prázdné sloty</param>
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
                Emit(new SolverEvent.TimeCheckEvent(DateTime.UtcNow - _startTime, _nodesExplored));

            if (_timeLimitExceeded)
                return;

            // Kontrola optima pri zaplneni vsech slotu
            if (emptySlots.Count == 0)
            {
                if (costSoFar < _bestCost)
                {
                    Emit(new SolverEvent.ImprovementEvent(_bestCost, costSoFar));
                    _bestCost = costSoFar;
                    _bestState = (State)state.Clone();
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


        /// <summary>
        /// MRV - Minimum Remaining Values
        /// Jedná se o výběr slotu s nejmenším počtem způsobilých kandidátu
        /// </summary>
        /// <param name="state"></param>
        /// <param name="emptySlots"></param>
        /// <returns>Trojice hodnot s MRV slotem, jeho kadidáty a odhadem spodní meze</returns>
        private (Slot slot, List<Referee> candidates, double lowerBound) SelectSlotMRV(
            State state, List<Slot> emptySlots)
        {
            Slot? best = null;
            List<Referee> bestCandidates = new();
            int bestCount = int.MaxValue;
            double lbSum = 0.0;

            foreach (var slot in emptySlots)
            {
                var candidates = _conflictChecker.GetEligibleReferees(state, slot, _referees);

                if (candidates.Count == 0)
                    return (slot, candidates, double.MaxValue); // mrtva vetev

                // Minimum pro tento slot = nejlevnější dostupné přiřazení
                double minCost = candidates
                    .Min(r => _costCalculator.AssignmentCost(slot, r));
                lbSum += minCost;

                if (candidates.Count < bestCount ||
                    candidates.Count == bestCount &&
                     slot.RequiredRank > (best?.RequiredRank ?? 0))
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