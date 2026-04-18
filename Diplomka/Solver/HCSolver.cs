using Diplomka.Model;
using Diplomka.Solver;

public class HCSolver
{
    private readonly List<Referee> _referees;
    private readonly Random _random = new Random();  // sdílená instance

    private State? _bestState;
    public double BestCost { get; private set; }

    public int MaxAttempts { get; set; } = 10;
    public int MaxIterations { get; set; } = 1000;
    public int MaxMoves { get; set; } = 20;

    public HCSolver(IEnumerable<Referee> referees)
    {
        _referees = referees.ToList();
    }

    // Validní počáteční stav — greedy, ne náhodný
    private State InitialState(List<Slot> slots)
    {
        return new GreedySolver(_referees).Solve(slots);
    }

    // Tah respektující omezení — vymění rozhodčího za způsobilého náhradníka
    private void ApplyRandomMove(State state)
    {
        var slots = state.GetSlots();
        // Vyber náhodný slot a zkus najít jiného způsobilého rozhodčího
        var slot = slots[_random.Next(slots.Count)];
        var eligible = ConflictChecker.GetEligibleReferees(state, slot, _referees);
        if (eligible.Count > 0)
        {
            state.SetReferee(slot, eligible[_random.Next(eligible.Count)]);
        }
    }

    public State Solve(State state)
    {
        _bestState = (State)state.Clone();
        BestCost = CostCalculator.TotalCost(_bestState);

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // Restart z nejlepšího stavu + perturbace (Iterated Local Search)
            var currentState = (State)_bestState!.Clone();
            for (int p = 0; p < 5; p++) ApplyRandomMove(currentState); // perturbace
            var currentCost = CostCalculator.TotalCost(currentState);

            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                bool improved = false;

                for (int move = 0; move < MaxMoves; move++)
                {
                    var nextState = (State)currentState.Clone();
                    ApplyRandomMove(nextState);
                    var nextCost = CostCalculator.TotalCost(nextState);

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

            if (currentCost < BestCost)
            {
                _bestState = (State)currentState.Clone();
                BestCost = currentCost;
            }
        }

        Console.WriteLine($"[HC] Hotovo. Nejlepší cena: {BestCost:F2}");
        return _bestState!;
    }

    public State Solve(List<Slot> slots)
    {
        var initialState = InitialState(slots);
        return Solve(initialState);
    }
}