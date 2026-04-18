using Diplomka.Model;

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
    public class GreedySolver
    {
        private readonly List<Referee> _referees;

        public GreedySolver(IEnumerable<Referee> referees)
        {
            _referees = referees.ToList();
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
                // Najdi způsobilé rozhodčí (hodnost + bez časové kolize)
                var eligible = ConflictChecker.GetEligibleReferees(state, slot, _referees);

                if (eligible.Count == 0)
                {
                    // Slot zůstane prázdný – opraví RepairHeuristic
                    continue;
                }

                // Vyber rozhodčího s nejnižší cenou přiřazení
                var best = eligible
                    .OrderBy(r => CostCalculator.AssignmentCost(slot, r))
                    .First();

                state.SetReferee(slot, best);
            }

            return state;
        }
    }
}
