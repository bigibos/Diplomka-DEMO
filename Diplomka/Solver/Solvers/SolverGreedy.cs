using Diplomka.Entity;
using Diplomka.Solver.Services;
using Diplomka.Solver.Solvers;

namespace Diplomka.Solver
{

    /// <summary>
    /// Hladový algoritmus pro pøímoèaré pøiøazení rozhodèích ke slotùm.
    /// 
    /// Jeho strategii je vybrat vždy to nejlepší možné pøiøazení pro postupné pøiøazování.
    /// Díky tomu je možné sestavovat rychlá poèáteèní øešení, která však pro složitìjší problémi mùžou zanechávat
    /// nektìré sloty prázdné z dùvodu "vyžrání" vhodných rozhodèích pøedèasnì (proto hladový)
    /// </summary>
    public class SolverGreedy : SolverBase
    {
        private readonly List<Referee> _referees;

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;

        public SolverGreedy(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator
            )
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;   
        }

        /// <summary>
        /// Pøetížení hlavní metody algoritmu <see cref="SolverGreedy.Solve(IEnumerable{Slot})"/>.
        /// Místo seznamu rozhodèích využívá stav.
        /// </summary>
        /// <param name="state">Stav pro vybrání seznamu slotù pro jejich zaplnìní</param>
        /// <returns>Nový sestavný stav øešení</returns>
        override public State Solve(State state)
        {
            return Solve(state.GetSlots());
        }

        /// <summary>
        /// Hlavní metoda pro bìh algoritmu.
        ///     1) Seøadí sloty podle úrovní a èasù
        ///     2) Pro každý slot najde vhodného kandidáty
        ///     3) Do slotu pøiøadí kandidáta s nejnižší cenou pøiøazení
        /// </summary>
        /// <param name="slots">Seznam slotù pro jejich zaplnìní</param>
        /// <returns>Nový sestavený stav øešení</returns>
        override public State Solve(IEnumerable<Slot> slots)
        {
            Emit(new SolverEvent.StartEvent());
            var state = new State();

            // Serazeni slotu podle potrebne urovne
            var orderedSlots = slots
                .OrderByDescending(s => s.RequiredRank)
                .ThenBy(s => _conflictChecker.GetEligibleReferees(state, s, _referees).Count)
                .ThenBy(s => s.Start)
                .ToList();

            foreach (var slot in orderedSlots)
                state.AddSlot(slot);

            foreach (var slot in orderedSlots)
            {
                // Ziskani kandidatu - bez kolize
                var eligible = _conflictChecker.GetEligibleReferees(state, slot, _referees);


                if (eligible.Count == 0)
                    continue;

                // Vyber rozhodciho s nejnizsi cenou prirazeni
                var best = eligible
                    .OrderBy(r => _costCalculator.AssignmentCost(slot, r))
                    .First();

                state.SetReferee(slot, best);
            }

            Emit(new SolverEvent.FinishEvent(_costCalculator.TotalCost(state)));
            return state;
        }
    }
}
