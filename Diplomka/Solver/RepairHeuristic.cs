using Diplomka.Entity;

namespace Diplomka.Solver
{
    public class RepairHeuristic
    {
        private readonly List<Referee> _referees;

        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SolverConfiguration _config;

        public RepairHeuristic(
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

        public State Repair(State state)
        {
            var current = (State)state.Clone();

            for (int pass = 0; pass < _config.MaxRepairPasses; pass++)
            {
                Console.WriteLine($"[Repair] Oprava {pass}");
                var emptySlots = GetEmptySlotsOrdered(current);
                if (emptySlots.Count == 0)
                {
                    Console.WriteLine($"[Repair] Oprava se podařila");
                    break;
                }

                foreach (var slot in emptySlots)
                    TryRepairSlot(current, slot);
            }

            return current;
        }

        private void TryRepairSlot(State state, Slot slot)
        {
            if (TryStandardAssign(state, slot)) return;
            if (TryChainRepair(state, slot)) return;
            TryFallbackAssign(state, slot);
        }

        private bool TryStandardAssign(State state, Slot slot)
        {
            var eligible = _conflictChecker.GetEligibleReferees(state, slot, _referees);
            if (eligible.Count == 0) return false;

            var best = eligible.OrderBy(r => _costCalculator.AssignmentCost(slot, r)).First();
            state.SetReferee(slot, best);
            Console.WriteLine($"[Repair] Standard {best.Name} -> {slot.Name}, {slot.Start}");
            return true;
        }

        // TODO: Dokumentace
        private static List<Referee> Relax(List<Referee> candidates, Func<Referee, bool> filter)
        {
            var filtered = candidates.Where(filter).ToList();
            return filtered.Count > 0 ? filtered : candidates;
        }

        // Fallback prirazeni ktere relaxuje nektera omezeni - casy ale musi byt dodrzeny!
        // TODO: Dokumentace
        private bool TryFallbackAssign(State state, Slot slot)
        {
            var candidates = _referees
                .Where(r => !_conflictChecker.Overlaps(state, slot, r))
                .ToList();

            if (candidates.Count() == 0) 
                return false;

            // Každá úroveň relaxace – pokud filtr vrátí neprázdný seznam, použijeme ho
            candidates = Relax(candidates, r => !_conflictChecker.Banned(slot, r));
            candidates = Relax(candidates, r => !_conflictChecker.MaxSlots(state, r));
            candidates = Relax(candidates, r => !_conflictChecker.UnderRanked(slot, r));
            candidates = Relax(candidates, r => !_conflictChecker.Incompatible(state, slot, r));

            var fallback = candidates
                .OrderBy(r => _costCalculator.AssignmentCost(slot, r))
                .FirstOrDefault();

            if (fallback == null)
                return false;

            Console.WriteLine($"[Repair] Fallback {fallback.Name} -> {slot.Name}, {slot.Start}");
            state.SetReferee(slot, fallback);
            return true;
        }

        private List<Slot> GetEmptySlotsOrdered(State state) =>
            state.GetEmptySlots()
                 .OrderBy(s => s.RequiredRank)
                 .ThenBy(s => s.Start)
                 .ToList();

        /// <summary>
        /// Pokusí se uvolnit kolizní slot s nižší prioritou, aby se uvolnil rozhodčí.
        /// Vrátí true, pokud se oprava povedla.
        /// </summary>
        private bool TryChainRepair(State state, Slot targetSlot)
        {
            var conflictingAssignments = state
                .Where(p => p.Value != null
                            && p.Value.Rank >= targetSlot.RequiredRank
                            && _conflictChecker.Overlaps(p.Key, targetSlot)
                            && p.Key.RequiredRank <= targetSlot.RequiredRank)
                .OrderBy(p => p.Key.RequiredRank)
                .ToList();

            foreach (var conflict in conflictingAssignments)
            {
                var referee = conflict.Value!;
                state.ClearSlot(conflict.Key);

                if (_conflictChecker.CanAssign(state, targetSlot, referee))
                {
                    // Hledej náhradu za uvolněný slot — GetEligibleReferees volá CanAssign,
                    // takže MaxRefereSlots je automaticky respektováno
                    var replacements = _conflictChecker.GetEligibleReferees(
                        state,
                        conflict.Key,
                        _referees.Where(r => !r.Equals(referee)).ToList());

                    state.SetReferee(targetSlot, referee);

                    if (replacements.Count > 0)
                    {
                        var replacement = replacements
                            .OrderBy(r => _costCalculator.AssignmentCost(conflict.Key, r))
                            .First();
                        state.SetReferee(conflict.Key, replacement);
                        Console.WriteLine($"[Repair] Chain {replacement.Name} -> {conflict.Key.Name}, {conflict.Key.Start}");
                    }
                    // Uvolněný slot zůstane prázdný – zpracuje se v další iteraci
                    return true;
                }
                else
                {
                    // Obnov původní přiřazení a zkus další konfliktní slot
                    state.SetReferee(conflict.Key, referee);
                }
            }

            return false;
        }
    }
}