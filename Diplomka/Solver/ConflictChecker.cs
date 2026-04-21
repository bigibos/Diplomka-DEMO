using Diplomka.Model;
using Diplomka.Routing;

namespace Diplomka.Solver
{
    /// <summary>
    /// Pomocné metody pro detekci časových konfliktů.
    /// </summary>
    public class ConflictChecker
    {

        private readonly DistanceTable _distanceTable;
        private readonly SolverConfiguration _config;

        public ConflictChecker(DistanceTable distanceTable, SolverConfiguration config)
        {
            _distanceTable = distanceTable;
            _config = config;
        }


        /// <summary>
        /// Vrátí true, pokud se dva sloty časově překrývají.
        /// </summary>
        public bool Overlaps(Slot first, Slot second)
        {
            // Pokud se prekrivaji uz ted, tak vracime
            if (first.End > second.Start)
                return false;

            // Ziskani doby potrebne pro presun mezi sloty
            var route = _distanceTable.GetRouteInfo(first.Location, second.Location);
            TimeSpan travelTime = TimeSpan.FromMinutes(route.DurationMinutes);

            // Zjistime, kdy je dostupny po skonceni prvniho slotu
            DateTime availableTime = first.End
                .Add(_config.RefereePostpTime)
                .Add(travelTime)
                .Add(_config.RefereePrepTime);

            // Rozhodci to nestihne
            if (availableTime > second.Start)
                return false;

            return true;
        }

        /// <summary>
        /// Vrátí true, pokud lze rozhodčímu přiřadit daný slot,
        /// aniž by došlo ke kolizi s již přiřazenými sloty v daném stavu.
        /// Zároveň ověřuje, že hodnost rozhodčího splňuje minimální požadavek slotu.
        /// </summary>
        public bool CanAssign(State state, Slot slot, Referee referee)
        {
            // Kontrola hodnosti
            if (referee.Rank < slot.RequiredRank)
                return false;

            // Kontrola časových kolizí
            foreach (var (assignedSlot, assignedReferee) in state)
            {
                // Pouze stejny rozhodci
                if (assignedReferee?.Id != referee.Id) 
                    continue;

                // Ktery slot zacina driv
                var (first, second) = slot.Start < assignedSlot.Start
                    ? (slot, assignedSlot)
                    : (assignedSlot, slot);

                if (Overlaps(first, second))
                    return false;

            }

            return true;
        }

        /// <summary>
        /// Vrátí seznam rozhodčích, kteří mohou být přiřazeni k danému slotu
        /// (splňují hodnost a nemají kolizi).
        /// </summary>
        public List<Referee> GetEligibleReferees(State state, Slot slot, IReadOnlyList<Referee> referees)
        {
            return referees.Where(r => CanAssign(state, slot, r)).ToList();
        }
    }
}
