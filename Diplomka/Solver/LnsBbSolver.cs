using Diplomka.Entity;
using Diplomka.Solver;

namespace Diplomka.Solver
{
    /*
     * Large Neighborhood Search (LNS)
     * Komplexnejsi nez klasicky Hill Climbing (Iterated Local Search - ILS)
     * Umoznuje unikat z lokalniho optima pomoci reseni skupin stavoveho prostoru, misto prostoru celeho
     * 
     * V kazde iteraci jsou tyto faze:
     *  - Destroy - vybere se urcity pocet slotu, ktere se uvolni pro nova prirazeni pomoci strategie:
     *      - Random - vyber slotu je nahodny
     *      - CostWeighted - preferuji se sloty s drazsim prirazenim (nejvetsi potencialni zlepseni)
     *      - Clustered - vyber jednoho kotevniho slotu a pak jeho casove sousedicich slotu
     *  - Restric - vyberou se vhodni kandidati pro prirazeni do skupiny slotu
     *  - Repair - pomoci B&B se provedou prirazeni na vybrane skupine slotu s vybranymi rozhodcimi
     *  - Merge - skupina prirazenych slotu se slouci se zbytkem stavu
     *  - Accept - pri zlepseni se akceptuje nove reseni, jinak se zkousi az do vycerpani limitu (pri prekroci se vybere nejlepsi znamy stav)
     */
    public class LnsBbSolver : ISolver
    {
        private readonly List<Referee> _referees;
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SolverConfiguration _config;
        private readonly Random _random = new();


        public int NeighborhoodSize { get; set; } = 10;

        public int MaxIterations { get; set; } = 150;

        public int MaxAttempts { get; set; } = 30;

        public TimeSpan IterationTimeLimit { get; set; } = TimeSpan.FromSeconds(3);

        public NeighborhoodStrategy Strategy { get; set; } = NeighborhoodStrategy.CostWeighted;

        public enum NeighborhoodStrategy
        {
            Random,
            CostWeighted,
            Clustered
        }

        public int TotalIterations { get; private set; }
        public int ImprovingIterations { get; private set; }
        public double BestCost { get; private set; }

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

        // Vytvoreni pocatecniho reseni pro warm start
        public State Solve(IEnumerable<Slot> slots)
        {
            Console.WriteLine("[LNS] Spouštím greedy warm start...");
            var initial = new GreedySolver(_referees, _conflictChecker, _costCalculator).Solve(slots.ToList());
            initial = new RepairHeuristic(_referees, _conflictChecker, _costCalculator, _config).Repair(initial);
            return Solve(initial);
        }

        // Jadro algoritmu, pouziva stav pro warm start
        public State Solve(State initialState)
        {
            TotalIterations = 0;
            ImprovingIterations = 0;

            var best = (State)initialState.Clone();
            var current = best;
            BestCost = _costCalculator.TotalCost(best);
            int noImprovementCount = 0;

            Console.WriteLine($"[LNS] Start, cena: {BestCost:F2}, iterací: {MaxIterations}");

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                TotalIterations++;

                var neighborhood = SelectNeighborhood(current);
                var available = GetAvailableReferees(current, neighborhood);

                if (available.Count == 0) { noImprovementCount++; continue; }

                // var repaired = Repair(current, neighborhood, available);
                // var merged = MergeStates(current, neighborhood, repaired);
                var merged = Repair(current, neighborhood, available);
                double mergedCost = _costCalculator.TotalCost(merged);

                if (mergedCost < BestCost)
                {
                    BestCost = mergedCost;
                    best = (State)merged.Clone();
                    current = best;
                    noImprovementCount = 0;
                    ImprovingIterations++;
                }
                else
                {
                    noImprovementCount++;
                    if (noImprovementCount >= MaxAttempts)
                    {
                        current = (State)best.Clone();
                        noImprovementCount = 0;
                    }
                }
            }

            return best;
        }

        // Repair faze vyuzivajici B&B
        private State Repair(State current, List<Slot> neighborhood, List<Referee> available)
        {
            try
            {
                var solver = new BBSolver(available, _conflictChecker, _costCalculator,
                                          _config, timeLimit: IterationTimeLimit);
                return solver.Solve(current, neighborhood);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LNS] Repair selhal: {ex.Message}");
                return current; // fallback
            }
        }

        // Destroy faze s vyberem rezimu
        private List<Slot> SelectNeighborhood(State state)
        {
            var slots = state.GetSlots();
            int groupSize = Math.Min(NeighborhoodSize, slots.Count);

            return Strategy switch
            {
                NeighborhoodStrategy.Random => SelectRandom(slots, groupSize),
                NeighborhoodStrategy.CostWeighted => SelectCostWeighted(state, slots, groupSize),
                NeighborhoodStrategy.Clustered => SelectClustered(slots, groupSize),
                _ => SelectRandom(slots, groupSize)
            };
        }

        // Random strategie pro destroy fazi
        private List<Slot> SelectRandom(List<Slot> slots, int groupSize)
        {
            return slots.OrderBy(s => _random.Next()).Take(groupSize).ToList();
        }

        // CostWeighted strategie pro destroy fazi
        private List<Slot> SelectCostWeighted(State state, List<Slot> slots, int groupSize)
        {
            // Kdyby byla vaha 0, tak by dalsi faze nepracoval spravne. Proto se pouziva offset
            const double weightOffset = 1.0;

            // Vyber prirazene sloty a jajich cenu
            var candidatesWithCost = slots.Select(s => {
                var referee = state.GetRefereeForSlot(s);
                double cost = referee != null
                    ? _costCalculator.AssignmentCost(s, referee)
                    : _config.UnassignedCost;
                return (slot: s, weight: cost + weightOffset);
            }).ToList();

            var group = new List<Slot>(groupSize);
            double total = candidatesWithCost.Sum(x => x.weight);

            // Vytvoreni listu pro vybranou skupinu
            while (group.Count < groupSize && candidatesWithCost.Count > 0)
            {
                double randomTreshold = _random.NextDouble() * total;
                int cId = candidatesWithCost.Count - 1;
                double cost = 0;
                for (int j = 0; j < candidatesWithCost.Count - 1; j++)
                {
                    cost += candidatesWithCost[j].weight;
                    if (randomTreshold <= cost)
                    {
                        cId = j; 
                        break;
                    }
                }
                group.Add(candidatesWithCost[cId].slot);
                total -= candidatesWithCost[cId].weight; // Asi lepsi nez pocitat vse pres sum pokazde?
                candidatesWithCost.RemoveAt(cId);
            }
            return group;
        }

        // Clustered strategie pro destroy fazi
        private List<Slot> SelectClustered(List<Slot> slots, int groupSize)
        {
            // Vybere se zakladni/kotevni slot
            var baseSlot = slots[_random.Next(slots.Count)];

            // Sloty se odfiltruji podle spolecne casove oblasti - jsou v casove clusteru
            var group = slots
                .OrderBy(s => Math.Abs((s.Start - baseSlot.Start).TotalMinutes))
                .Take(groupSize)
                .ToList();

            return group;
        }


        // Restrict faze
        private List<Referee> GetAvailableReferees(State state, List<Slot> relaxed)
        {
            var relaxedSet = new HashSet<Slot>(relaxed);
            var blocked = new HashSet<Referee>();

            // Puvodni rozhodci z uvolnenych slotu
            var relaxedReferees = relaxed
                .Select(s => state.GetRefereeForSlot(s))
                .Where(r => r != null)
                .ToHashSet();


            foreach (var s in state.GetSlots())
            {
                if (relaxedSet.Contains(s)) 
                    continue; // preskoceni uvolnenych slotu

                var referee = state.GetRefereeForSlot(s);
                if (referee == null) 
                    continue; // prazdny slot nikoho neblokuje

                if (relaxedReferees.Contains(referee))
                    continue;

                if (blocked.Contains(referee)) 
                    continue; // rozhodci je uz zablokovany pro prirazeni

                foreach (var r in relaxed)
                {
                    // prirazeni by vytvorilo casovy prekryv
                    if (_conflictChecker.Overlaps(s, r))
                    {
                        blocked.Add(referee); // zablokuj tedy rozhodciho
                        break;
                    }
                }
            }

            // rozhodci, kteri nejsou v blokaci pro uvolnene sloty
            return _referees.Where(r => !blocked.Contains(r)).ToList();
        }

        // Merge faze
        private State MergeStates(State state, List<Slot> relaxed, State groupState)
        {
            var relaxedSet = new HashSet<Slot>(relaxed);
            var merged = new State();

            // Pridani vsech slotu z puvodniho stavu
            foreach (var slot in state.GetSlots())
                merged.AddSlot(slot);

            // Pridani do merged prirazeni mimo opravenou skupinu
            foreach (var slot in state.GetSlots())
            {
                if (relaxedSet.Contains(slot)) 
                    continue;

                var referee = state.GetRefereeForSlot(slot);
                if (referee != null)
                    merged.SetReferee(slot, referee);
            }

            // Pridani do merged prirazeni z opravene skupiny 
            foreach (var slot in relaxed)
            {
                var referee = groupState.GetRefereeForSlot(slot);
                if (referee != null)
                    merged.SetReferee(slot, referee);
            }

            return merged;
        }
    }
}