using Diplomka.Model;

namespace Diplomka.Solver
{
    /// <summary>
    /// Pomocné metody pro detekci časových konfliktů.
    /// </summary>
    public static class ConflictChecker
    {
        /// <summary>
        /// Vrátí true, pokud se dva sloty časově překrývají.
        /// </summary>
        public static bool Overlaps(Slot a, Slot b)
        {
            // Překryv nastane, pokud jeden začíná dříve, než druhý končí
            return a.Start < b.End && b.Start < a.End;
        }

        /// <summary>
        /// Vrátí true, pokud lze rozhodčímu přiřadit daný slot,
        /// aniž by došlo ke kolizi s již přiřazenými sloty v daném stavu.
        /// Zároveň ověřuje, že hodnost rozhodčího splňuje minimální požadavek slotu.
        /// </summary>
        public static bool CanAssign(State state, Slot slot, Referee referee)
        {
            // Kontrola hodnosti
            if (referee.Rank < slot.RequiredRank)
                return false;

            // Kontrola časových kolizí
            foreach (var (assignedSlot, assignedReferee) in state)
            {
                if (assignedReferee?.Id == referee.Id && Overlaps(slot, assignedSlot))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Vrátí seznam rozhodčích, kteří mohou být přiřazeni k danému slotu
        /// (splňují hodnost a nemají kolizi).
        /// </summary>
        public static List<Referee> GetEligibleReferees(State state, Slot slot, IReadOnlyList<Referee> referees)
        {
            return referees.Where(r => CanAssign(state, slot, r)).ToList();
        }
    }
}
