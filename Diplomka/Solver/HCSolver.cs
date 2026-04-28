using Diplomka.Entity;
using Diplomka.Solver;

namespace Diplomka.Solver
{
    public class HCSolver : ISolver
    {
        private readonly List<Referee> _referees;
        private readonly Random _random = new Random();

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SortedCandidateTable _candidateTable;

        private State? _bestState;
        private double _bestCost;
        public double BestCost => _bestCost;

        public int MaxAttempts { get; set; } = 10;
        public int MaxIterations { get; set; } = 1000;
        public int MaxMoves { get; set; } = 20;

        public HCSolver(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            SortedCandidateTable candidateTable
            )
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _candidateTable = candidateTable;
        }

        // Validní počáteční stav — greedy, ne náhodný
        private State InitialState(List<Slot> slots)
        {
            return new GreedySolver(_referees, _conflictChecker, _costCalculator, _candidateTable).Solve(slots);
        }

        // Tah respektující omezení — vymění rozhodčího za způsobilého náhradníka
        private void ApplyRandomMove(State state)
        {
            var slots = state.GetSlots();
            // Vyber náhodný slot a zkus najít jiného způsobilého rozhodčího
            var slot = slots[_random.Next(slots.Count)];
            state.ClearSlot(slot); // uvolni slot, aby byl znovu přiřaditelný

            var eligible = _conflictChecker.GetEligibleReferees(state, slot, _referees);
            if (eligible.Count > 0)
            {
                state.SetReferee(slot, eligible[_random.Next(eligible.Count)]);
            }
        }

        public State Solve(State state)
        {
            _bestState = (State)state.Clone();
            _bestCost = _costCalculator.TotalCost(_bestState);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                // Restart z nejlepšího stavu + perturbace (Iterated Local Search)
                var currentState = (State)_bestState!.Clone();
                for (int p = 0; p < 5; p++) ApplyRandomMove(currentState); // perturbace
                var currentCost = _costCalculator.TotalCost(currentState);

                for (int iteration = 0; iteration < MaxIterations; iteration++)
                {
                    bool improved = false;

                    for (int move = 0; move < MaxMoves; move++)
                    {
                        var nextState = (State)currentState.Clone();
                        ApplyRandomMove(nextState);
                        var nextCost = _costCalculator.TotalCost(nextState);

                        if (nextCost < currentCost)
                        {
                            currentState = nextState;
                            currentCost = nextCost;
                            improved = true;
                            break;
                        }
                    }

                    if (!improved) break; // lokální optimum — ukonči iteraci
                }

                if (currentCost < _bestCost)
                {
                    _bestState = (State)currentState.Clone();
                    _bestCost = currentCost;
                    Console.WriteLine($"[HC] Nalezena nová nejlepší cena: {_bestCost:F2}");
                }
            }

            Console.WriteLine($"[HC] Hotovo. Nejlepší cena: {_bestCost:F2}");
            return _bestState!;
        }

        public State Solve(IEnumerable<Slot> slots)
        {
            var initialState = InitialState(slots.ToList());
            return Solve(initialState);
        }
    }
}
