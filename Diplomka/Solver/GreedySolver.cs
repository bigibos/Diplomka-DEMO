using Diplomka.Entity;

namespace Diplomka.Solver
{

    /*
     * Seradi sloty podle potrebne urovne a snazi se nejmin priradit sloty pro ktere je nejmin kandidatu
     */
    public class GreedySolver : ISolver
    {
        private readonly List<Referee> _referees;

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;

        public GreedySolver(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator
            )
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;   
        }

        public State Solve(State state)
        {
            return Solve(state.GetSlots());
        }

        public State Solve(IEnumerable<Slot> slots)
        {
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

            return state;
        }
    }
}
