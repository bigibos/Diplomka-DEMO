using Diplomka.Entity;
using Diplomka.Routing;
using Diplomka.Solver.Config;

namespace Diplomka.Solver.Services
{
    /// <summary>
    /// Slouží pro kontrolu podmínek přiřazení a konfliktů
    /// </summary>
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
        /// <summary>
        /// Kontrola časového překrytí dvou slotu se započtenými časy na přípravu, dokončení a přesun
        /// </summary>
        /// <param name="a">Jeden slot</param>
        /// <param name="b">Druhý slot</param>
        /// <returns>True, když se překrývají, jinak false</returns>
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

        /// <summary>
        /// Kontroluje jestli se některý slot přiřazený k rozhodčímu časově překrývá s daným slotem.
        /// </summary>
        /// <param name="state">Stav pro získání přiřazení</param>
        /// <param name="slot">Slot ke kontrola</param>
        /// <param name="referee">Rozhdočí ke kontrole</param>
        /// <returns>True, když se alespoň jeden slot překrývá, jinak false</returns>
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

        // TODO: Zkusit pridat i kontrolu nazvu
        /// <summary>
        /// Kontroluje jestli jsou dva sloty vrámci stejného zápasu podle času a lokace
        /// </summary>
        /// <param name="a">Jeden slot</param>
        /// <param name="b">Druhý slot</param>
        /// <returns>True pokud jsou sloty součástí stejného zápasu, jinak false</returns>
        private bool SameMatchTime(Slot a, Slot b)
        {
            return a.Start == b.Start && a.End == b.End && a.Location.Equals(b.Location);
        }

        /// <summary>
        /// Kontroluje jestli nemá rozhodčí daný slot zakázaný
        /// </summary>
        /// <param name="slot">Slot ke kontrole</param>
        /// <param name="referee">Rozhodčí ke kontrole</param>
        /// <returns>True, když má slot zakázaný, jinak false</returns>
        public bool Banned(Slot slot, Referee referee)
        {
            if (referee.BannedSlotIds.Contains(slot.Id))
                return true;
            return false;
        }

        /// <summary>
        /// Kontroluje jestli není rozhodčí podkvalifikován i se započtenou vůlí
        /// </summary>
        /// <param name="slot">Slot ke kontrole</param>
        /// <param name="referee">Rozhodčí ke kontrole</param>
        /// <returns>True, když je rozhodčí podkvalifikován, jinak false</returns>
        public bool UnderRanked(Slot slot, Referee referee)
        {
            if (referee.Rank + _config.RankDiffMargin < slot.RequiredRank)
                return true;
            return false;
        }

        /// <summary>
        /// Kontroluje jestli rozhodčí nepřekročí maximální počet přiřazených slotů
        /// </summary>
        /// <param name="state">Stav pro kontrolu přiřazení</param>
        /// <param name="referee">Rozhodčí ke kontrole</param>
        /// <returns>True, když maximum přiřazení překročí, jinak false</returns>
        public bool MaxSlots(State state, Referee referee)
        {
            var assignedSlots = state.GetSlotsByReferee(referee);
            if (assignedSlots.Count >= _config.MaxRefereSlots)
                return true;
            return false;
        }

        /// <summary>
        /// Kontroluje jestli nedojde při přiřazení k danémus slotu k setkání s jiným nežádoucím rozhodčím v rámci stejného zápasu
        /// </summary>
        /// <param name="state">Stav pro kontrolu přiřazení</param>
        /// <param name="slot">Slot ke kontrole</param>
        /// <param name="referee">Rozhodčí ke kontrole</param>
        /// <returns>True, když k takovému setkání může dojít, jinak false</returns>
        public bool Incompatible(State state, Slot slot, Referee referee)
        {
            if (referee.IncompatibleRefereeIds.Count > 0)
            {
                foreach (var (assignedSlot, assignedReferee) in state)
                {
                    if (assignedReferee == null) continue;
                    if (!referee.IncompatibleRefereeIds.Contains(assignedReferee.Id)) continue;

                    // Konfliktni rozhodci je prirazen do stejneho zapasu jako je dany slot
                    if (SameMatchTime(slot, assignedSlot))
                        return true;
                }
            }
            return false;
        }


        /// <summary>
        /// Kontrola možného přiřazení, zdali dodržuje stanovené podmínky a pravidla
        /// </summary>
        /// <param name="state">Stav pro kontrolu přiřazení</param>
        /// <param name="slot">Slot ke kontrole</param>
        /// <param name="referee">Rozhodčí ke kontrole</param>
        /// <returns>True, když může k přiřazení dojít, jinak false</returns>
        public bool CanAssign(State state, Slot slot, Referee referee)
        {
            // Kontrola hodnosti
            // Kontrola časových kolizí
            if (Overlaps(state, slot, referee))
                return false;

            // Kontrola zakazanych slotu pro rozhodciho
            if (Banned(slot, referee))
                return false;

            // Rozhoci ma uz maximum prirazenych slotu
            if (MaxSlots(state, referee))
                return false;

            if (UnderRanked(slot, referee))
                return false;

            // Kontrola rozhodcich kteri nemohou byt spolu (at uz z jakehokoliv duvodu)
            if (Incompatible(state, slot, referee))
                return false;    

            return true;
        }

        /// <summary>
        /// Umožňuje získání vhodných kondidátních rozhodčích, kteří splňují podmínky pro daný slot
        /// </summary>
        /// <param name="state">Stav pro kontrolu přiřazení</param>
        /// <param name="slot">Slot ke kontrole</param>
        /// <param name="referees">Seznam rozhodčích, ze kterých se vyberou kandidáti</param>
        /// <returns>Seznam kandidátních rozhodčích</returns>
        public List<Referee> GetEligibleReferees(State state, Slot slot, IReadOnlyList<Referee> referees)
        {
            return referees.Where(r => CanAssign(state, slot, r)).ToList();
        }
    }
}
