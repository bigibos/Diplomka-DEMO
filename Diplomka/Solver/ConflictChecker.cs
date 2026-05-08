using Diplomka.Entity;
using Diplomka.Routing;

namespace Diplomka.Solver
{
    public class ConflictChecker
    {

        private readonly RouteTable _distanceTable;
        private readonly SolverConfiguration _config;

        public SolverConfiguration Config => _config;

        public ConflictChecker(RouteTable distanceTable, SolverConfiguration config)
        {
            _distanceTable = distanceTable;
            _config = config;
        }


        /*
         * Vraci true kdyz se dva sloty casove prekryvaji
         * Bere v potaz i dobu cestovani a casove konstanty
         */
        public bool Overlaps(Slot a, Slot b)
        {
            // Ktery slot zacina driv
            var (first, second) = a.Start < b.Start
                ? (a, b)
                : (b, a);

            // Pokud se prekrivaji uz ted, tak vracime
            if (first.End > second.Start)
                return true;


            // Ziskani doby potrebne pro presun mezi sloty
            var route = _distanceTable.GetRouteInfo(first.Location, second.Location);
            TimeSpan travelTime = route.Duration;


            // Zjistime, kdy je dostupny po skonceni prvniho slotu
            DateTime availableTime = first.End
                .Add(_config.RefereePostTime)
                .Add(travelTime)
                .Add(_config.RefereePrepTime);

            // Rozhodci to nestihne
            if (availableTime > second.Start)
                return true;

            return false;
        }

        public bool Overlaps(State state, Slot slot, Referee referee)
        {
            var assignedSlots = state.GetSlotsByReferee(referee);
            foreach (var assignedSlot in assignedSlots)
            {
                if (Overlaps(slot, assignedSlot))
                    return true;
            }
            return false;
        }

        // TODO: Dopsat dokumentaci kometar
        // Kontrola jestli jestli dva sloty patri do stejneho zapasu
        private static bool SameMatchTime(Slot a, Slot b)
        {
            return a.Start == b.Start && a.End == b.End && a.Location.Equals(b.Location);
        }

        /*
         * Kontrola jestli deochazi u avizovaneho prirazeni ke konfliktu v ramci stavu
         * Pokud existuje konflikt vraci false
         */
        public bool CanAssign(State state, Slot slot, Referee referee)
        {
            // Kontrola hodnosti
            if (referee.Rank + _config.RankDiffMargin < slot.RequiredRank)
                return false;

            var assignedSlots = state.GetSlotsByReferee(referee);

            // Rozhoci ma uz maximum prirazenych slotu
            if (assignedSlots.Count >= _config.MaxRefereSlots)
                return false;

            // Kontrola zakazanych slotu pro rozhodciho
            if (referee.BannedSlotIds.Contains(slot.Id))
                return false;


            // Kontrola časových kolizí
            if (Overlaps(state, slot, referee))
                return false;


            // Kontrola rozhodcich kteri nemohou byt spolu (at uz z jakehokoliv duvodu)
            if (referee.IncompatibleRefereeIds.Count > 0)
            {
                foreach (var (assignedSlot, assignedReferee) in state)
                {
                    if (assignedReferee == null) continue;
                    if (!referee.IncompatibleRefereeIds.Contains(assignedReferee.Id)) continue;

                    // Konfliktni rozhodci je prirazen do stejneho zapasu jako je dany slot
                    if (SameMatchTime(slot, assignedSlot))
                        return false;
                }
            }

            return true;
        }

        /*
         * Vraci zpusobile rozhodci bez kolizi pro dany slot - kandidati
         */
        public List<Referee> GetEligibleReferees(State state, Slot slot, IReadOnlyList<Referee> referees)
        {
            return referees.Where(r => CanAssign(state, slot, r)).ToList();
        }
    }
}
