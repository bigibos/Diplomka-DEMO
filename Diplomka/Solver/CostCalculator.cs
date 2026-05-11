using Diplomka.Entity;
using Diplomka.Routing;
using System.Collections.Generic;

namespace Diplomka.Solver
{
    /// <summary>
    /// Slouží pro výpočet cen přiřazní a cen celých stavů řešení.
    /// </summary>
    public class CostCalculator
    {
       
        private readonly SolverConfiguration _config;
        private readonly RouteTable _distanceTable;
        private readonly RouteSolver _routeSolver;

        public CostCalculator(RouteTable distanceTable, SolverConfiguration config)
        {
            _config = config;   
            _distanceTable = distanceTable;
            _routeSolver = new RouteSolver(_distanceTable, _config);
        }

        /// <summary>
        /// Prostá varianta pro vápočet ceny přiřazení rozhodčího ke slotu.
        /// Využívá se základní výpočet vzdálenosti, vždy ze zázemí rozhodčího ke slotu
        /// </summary>
        /// <param name="slot">Slot pro výpočet</param>
        /// <param name="referee">Rozhodčí pro výpočet</param>
        /// <returns>Jednoduchou cenu přiřazení</returns>
        public double AssignmentCost(Slot slot, Referee? referee)
        {

            /*
             * Nastaveni vahy ranku
             * Muze byt prekvalifikovany, nebo podkavlifikovany
             */
            double rankFactor = referee.Rank > slot.RequiredRank ? _config.OverRankFactor : _config.UnderRankFactor;
            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);

            double distance = _distanceTable.GetRouteInfo(referee.Location, slot.Location!).DistanceKm;
            return rankFactor * rankDiff + _config.DistanceFactor * distance;
        }

        /// <summary>
        /// Hlavní vylepšená varianta pro vápočet ceny přiřazení rozhodčího ke slotu.
        /// Využívá se intuitivní způsob pro vyhodnocení vzdálenosti s ohledem na časová okna a časově sousedící sloty
        /// </summary>
        /// <param name="state">Stav pro kontext přiřazení</param>
        /// <param name="slot">Slot pro výpočet</param>
        /// <param name="referee">Rozhodčí pro výpočet</param>
        /// <returns>Cenu přiřezení v kontextu daného stavu</returns>
        public double AssignmentCost(State state, Slot slot, Referee? referee)
        {
            if (referee == null)
                return _config.UnassignedCost;

            // Nastaveni vahy ranku - prekvalifikovanost, nebo podkvalifikovanost 
            double rankFactor = referee.Rank > slot.RequiredRank ? _config.OverRankFactor : _config.UnderRankFactor;
            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);

            var route = _routeSolver.ComputeOptimalRoute(state,  slot, referee);

            return rankFactor * rankDiff + _config.DistanceFactor * route.DistanceKm;
        }

        /// <summary>
        /// Kontextuální výpočet souhrnu cen přiřazení v celém stavu
        /// </summary>
        /// <param name="state">Stav pro výpočet a kontext</param>
        /// <returns>Souhrn cen přiřezní v celém stavu</returns>
        public double TotalCost(State state)
        {
            double totalCost = 0;

            foreach (var (slot, referee) in state)
                totalCost += AssignmentCost(state, slot, referee);

            return totalCost;
        }

        // TODO: Pochopit a dpolnit doc. komenty
        // Vypocet dolni meze pro neohodnocene sloty v danem stavu.
        public double LowerBoundForSlots(
            State state,
            IEnumerable<Slot> emptySlots,
            IReadOnlyList<Referee> referees,
            ConflictChecker conflictChecker)
        {
            double lowerBound = 0;
            foreach (var slot in emptySlots)
            {
                double minCost = double.MaxValue;

                foreach (var referee in referees)
                {
                    // Kontrola způsobilosti v aktuálním kontextu (rank + časové kolize)
                    if (conflictChecker.CanAssign(state, slot, referee))
                    {
                        double c = AssignmentCost(slot, referee);
                        if (c < minCost) minCost = c;
                    }
                }

                // Pokud pro prázdný slot neexistuje v aktuálním stavu žádný kandidát,
                // znamená to, že tato větev je "mrtvá" – vrátíme extrémní penalizaci.
                if (minCost == double.MaxValue)
                    return double.MaxValue; // Okamžitý pruning

                lowerBound += minCost;
            }
            return lowerBound;
        }

        // TODO: Pochopit a dpolnit doc. komenty
        // Vypocet dolni meze pro neohodnocene sloty bez ohledu na konflikt s ostatnimi sloty
        public double LowerBoundForSlotsSoft(IEnumerable<Slot> slots, IReadOnlyList<Referee> referees)
        {
            double lb = 0;
            foreach (var slot in slots)
            {
                double minCost = double.MaxValue;
                foreach (var referee in referees)
                {
                    if (referee.Rank >= slot.RequiredRank) // pouze způsobilí rozhodčí
                    {
                        double c = AssignmentCost(slot, referee);
                        if (c < minCost) minCost = c;
                    }
                }

                if (minCost == double.MaxValue)
                    return double.MaxValue;

                lb += minCost;
                /*
                // Pokud neexistuje žádný způsobilý rozhodčí, přidáme velkou penalizaci
                lb += minCost == double.MaxValue ? _config.UnassignedCost : minCost;
                */
            }
            return lb;
        }
    }
}
