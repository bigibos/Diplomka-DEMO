using Diplomka.Entity;

namespace Diplomka.Solver
{
    /// <summary>
    /// Greedy heuristika pro rychlé sestavení počátečního řešení.
    ///
    /// Strategie:
    ///   1. Seřaď sloty sestupně podle RequiredRank (nejtěžší sloty obsazujeme první –
    ///      pro ně je nejméně způsobilých rozhodčích).
    ///   2. Pro každý slot vyber způsobilého rozhodčího s nejnižší cenou přiřazení.
    ///
    /// Složitost: O(S × R) kde S = počet slotů, R = počet rozhodčích.
    /// </summary>
    public class GreedySolver : ISolver
    {
        private readonly List<Referee> _referees;

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SortedCandidateTable _candidateTable;

        public GreedySolver(
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

        public State Solve(State state)
        {
            return Solve(state.GetSlots());
        }

        public State Solve(IEnumerable<Slot> slots)
        {
            var state = new State();

            // Seřaď sloty: nejprve nejnáročnější (vysoký required rank),
            // při shodě seřaď chronologicky
            var orderedSlots = slots
                .OrderByDescending(s => s.RequiredRank)
                .ThenBy(s => s.Start)
                .ToList();

            foreach (var slot in orderedSlots)
                state.AddSlot(slot);

            foreach (var slot in orderedSlots)
            {
                /*
                // Najdi způsobilé rozhodčí (hodnost + bez časové kolize)
                var eligible = _conflictChecker.GetEligibleReferees(state, slot, _referees);
                

                if (eligible.Count == 0)
                {
                    // Slot zůstane prázdný – opraví RepairHeuristic
                    continue;
                }

                // Vyber rozhodčího s nejnižší cenou přiřazení
                var best = eligible
                    .OrderBy(r => _costCalculator.AssignmentCost(slot, r))
                    .First();
                */
                var best = _candidateTable.GetBestCandidate(state, slot);

                if (best == null)
                    continue; // Slot zustane prazdny - opravi repair

                state.SetReferee(slot, best);
            }

            return state;
        }
    }
}
