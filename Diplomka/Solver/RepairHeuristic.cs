using Diplomka.Model;

namespace Diplomka.Solver
{
    /// <summary>
    /// Opravná heuristika – zajišťuje, že žádný slot nezůstane bez rozhodčího.
    ///
    /// Používá se po greedy fázi, pokud greedy nestihlo obsadit všechny sloty.
    ///
    /// Strategie:
    ///   Pro každý prázdný slot:
    ///     1. Zkus najít způsobilého rozhodčího bez konfliktu (standardní cesta).
    ///     2. Pokud nikdo není volný: najdi rozhodčího, jehož přesunutím
    ///        konfliktnaje přiřazení uvolníme – tzv. chain-repair.
    ///     3. Pokud ani to nepomůže: přiřaď nejlepšího způsobilého rozhodčího
    ///        s ignorováním časové kolize a zaloguj varování
    ///        (v praxi by to signalizovalo nedostatek rozhodčích v daném časovém okně).
    /// </summary>
    public class RepairHeuristic
    {
        private readonly List<Referee> _referees;

        public RepairHeuristic(IEnumerable<Referee> referees)
        {
            _referees = referees.ToList();
        }

        public State Repair(State state)
        {
            var current = (State)state.Clone();
            var emptySlots = current.GetEmptySlots()
                .OrderByDescending(s => s.RequiredRank)
                .ThenBy(s => s.Start)
                .ToList();

            foreach (var slot in emptySlots)
            {
                // Pokus 1: standardní přiřazení
                var eligible = ConflictChecker.GetEligibleReferees(current, slot, _referees);
                if (eligible.Count > 0)
                {
                    var best = eligible.OrderBy(r => CostCalculator.AssignmentCost(slot, r)).First();
                    current.SetReferee(slot, best);
                    continue;
                }

                // Pokus 2: chain-repair – uvolni méně důležitý přiřazený slot
                bool repaired = TryChainRepair(current, slot);
                if (repaired) continue;

                // Pokus 3: nouzové přiřazení (porušuje časovou kolizi – logujeme)
                var fallback = _referees
                    .Where(r => r.Rank >= slot.RequiredRank)
                    .OrderBy(r => CostCalculator.AssignmentCost(slot, r))
                    .FirstOrDefault();

                if (fallback != null)
                {
                    Console.WriteLine($"[WARN] Nouzové přiřazení (kolize) pro slot {slot}: {fallback.Name}");
                    current.SetReferee(slot, fallback);
                }
                else
                {
                    Console.WriteLine($"[ERROR] Nelze přiřadit žádného rozhodčího ke slotu {slot}");
                }
            }

            return current;
        }

        /// <summary>
        /// Pokusí se uvolnit kolizní slot s nižší prioritou, aby se uvolnil rozhodčí.
        /// Vrátí true, pokud se oprava povedla.
        /// </summary>
        private bool TryChainRepair(State state, Slot targetSlot)
        {
            // Najdi všechny přiřazení, která kolidují s targetSlot
            // a jejichž rozhodčí má hodnost >= RequiredRank targetSlotu
            var conflictingAssignments = state
                .Where(p => p.Value != null
                            && p.Value.Rank >= targetSlot.RequiredRank
                            && ConflictChecker.Overlaps(p.Key, targetSlot)
                            && p.Key.RequiredRank <= targetSlot.RequiredRank) // nižší priorita
                .OrderBy(p => p.Key.RequiredRank) // nejdříve nejméně náročné
                .ToList();

            foreach (var conflict in conflictingAssignments)
            {
                var referee = conflict.Value!;
                // Zkus přiřadit uvolněného rozhodčího k targetSlotu
                // Dočasně odeber konfliktní přiřazení
                state.ClearSlot(conflict.Key);

                if (ConflictChecker.CanAssign(state, targetSlot, referee))
                {
                    // Zkontroluj, zda lze obsadit uvolněný slot jiným rozhodčím
                    var replacements = ConflictChecker.GetEligibleReferees(state, conflict.Key, 
                        _referees.Where(r => r.Id != referee.Id).ToList());

                    state.SetReferee(targetSlot, referee);

                    if (replacements.Count > 0)
                    {
                        var replacement = replacements
                            .OrderBy(r => CostCalculator.AssignmentCost(conflict.Key, r))
                            .First();
                        state.SetReferee(conflict.Key, replacement);
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
