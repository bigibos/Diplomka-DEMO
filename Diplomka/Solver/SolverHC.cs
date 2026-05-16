using Diplomka.Entity;
using Diplomka.Solver;

namespace Diplomka.Solver
{
    /// <summary>
    /// Hlavní optimalizační algoritmus.
    ///     - Iterované Hill Climbing (horolezecký algoritmus)
    ///     - Stochastický
    ///     - Stavy řešení jsou nepředvídatelné (prvek náhody)
    /// </summary>
    public class SolverHC : SolverBase
    {


        private readonly List<Referee> _referees;
        private readonly Random _random = new Random();

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SolverConfiguration _config;

        private State? _bestState;
        private double _bestCost;

        public double BestCost => _bestCost;

        public int MaxAttempts { get; set; } = 100;
        public int MaxIterations { get; set; } = 2000;
        public int MaxMoves { get; set; } = 50;

        public SolverHC(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            SolverConfiguration config
            )
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _config = config;
        }

        /// <summary>
        /// Vytvoření počátečního startu pro warm start.
        /// Využívají se algortimy <see cref="SolverGreedy"/> a <see cref="SolverRepair"/> pro opravu.
        /// </summary>
        /// <param name="slots">Seznam slotů pro jejich naplnění</param>
        /// <returns>Hotový počáteční stav</returns>
        private State InitialState(IEnumerable<Slot> slots)
        {
            var greedy = new SolverGreedy(_referees, _conflictChecker, _costCalculator);
            greedy.OnEvent += Forward;

            var initState = greedy.Solve(slots);

            var emptyAfterGreedy = initState.GetEmptySlots().ToList();
            if (emptyAfterGreedy.Count > 0)
            {
                var repair = new SolverRepair(_referees, _conflictChecker, _costCalculator, _config);
                repair.OnEvent += Forward; 

                // Console.WriteLine($"[Greedy] Nezaplnil {emptyAfterGreedy.Count} slotů. Oprava...");
                // Emit(new SolverEvent.InfoEvent($"Nezaplněno {emptyAfterGreedy.Count} slotů."));
                initState = repair.Solve(initState);    
            }
            
            return initState;
        }

        /// <summary>
        /// Náhodná výměna/prohození rozhodčích mezi dvěma sloty s repsektováním časového omezení
        /// </summary>
        /// <param name="state">Stav ve kterém se má provést prohození</param>
        private void ApplyRandomMove(State state)
        {
            var slots = state.GetSlots();

            // Vybere se nahodny slot pro ktery provadime zmenu
            var slot = slots[_random.Next(slots.Count)];
            var originalReferee = state.GetReferee(slot);
            state.ClearSlot(slot);

            // Vybere se nahodny existujici kandidat pro dany slot
            var eligible = _conflictChecker.GetEligibleReferees(state, slot, _referees);
            if (eligible.Count > 0)
                state.SetReferee(slot, eligible[_random.Next(eligible.Count)]);
            else
                state.SetReferee(slot, originalReferee);
            
        }

        /// <summary>
        /// Hlavní výpočetní metoda pro algoritmus.
        ///     1) Provádí se několik zlepšujících pokusů
        ///     2) V každém pokusu se provede pertrubace a pak se iteruje
        ///     3) V každé iteraci se provede určitý počet náhodných výměn vedoucích k zlepšení
        /// </summary>
        /// <param name="state">Počáteční stav pro warm start</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        override public State Solve(State state)
        {
            _bestState = (State)state.Clone();
            _bestCost = _costCalculator.TotalCost(_bestState);

            Emit(new SolverEvent.StartEvent(_bestCost));

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                // Restart z nejlepšího stavu + perturbace (Iterated Local Search)
                var currentState = (State)_bestState!.Clone();
                for (int p = 0; p < 20; p++) ApplyRandomMove(currentState); // perturbace
                var currentCost = _costCalculator.TotalCost(currentState);

                // TODO: Predelat do dvou urovni smycek (podle navrhu v teorii)
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
                    Emit(new SolverEvent.ImprovementEvent(_bestCost, currentCost));
                    _bestState = (State)currentState.Clone();
                    _bestCost = currentCost;
                }
            }

            Emit(new SolverEvent.FinishEvent(_bestCost));
            return _bestState!;
        }

        /// <summary>
        /// Přetížení hlavní metody algoritmu <see cref="Solve(State)"/>, která místo stavu pracuje se seznamem slotů, které se mají zaplnit.
        /// </summary>
        /// <param name="slots">Seznam slotů k zaplnění.</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        override public State Solve(IEnumerable<Slot> slots)
        {
            var initialState = InitialState(slots.ToList());
            return Solve(initialState);
        }
    }
}
