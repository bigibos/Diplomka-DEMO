using Diplomka.Entity;
using Diplomka.Routing;
using System.Collections.Generic;

namespace Diplomka.Solver
{
    /// <summary>
    /// Vypočítává cenu přiřazení rozhodčího ke slotu.
    /// Cena = váhovaný součet absolutního rozdílu hodnosti a vzdálenosti v km.
    /// </summary>
    public class CostCalculator
    {
       
        private readonly SolverConfiguration _config;
        private readonly DistanceTable _distanceTable;
        private readonly RouteSolver _routeSolver;

        public CostCalculator(DistanceTable distanceTable, SolverConfiguration config)
        {
            _config = config;   
            _distanceTable = distanceTable;
            _routeSolver = new RouteSolver(_distanceTable, _config);
        }

        /*
         * Vypocet ceny prirazeni rozhodciho ke slotu
         * Vyuziva se primitivni zpusob na vypocet vzdalenosti - vzdy ze zazemi rozhodciho
         */
        public double AssignmentCost(Slot slot, Referee referee)
        {
            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);

            double distance = _distanceTable.GetRouteInfo(referee.Location, slot.Location!).DistanceKm;
            return _config.RankWeight * rankDiff + _config.DistanceWeight * distance;
        }

        /*
         * Vypocet ceny prirazeni rozhodciho ke slotu
         * Vyuziva se intuitivnejsi zpusob pro vypocet vzdalenosti s ohledem na sousedni sloty
         */
        public double AssignmentCost(State state, Slot slot, Referee referee)
        {
            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);

            var route = _routeSolver.ComputeOptimalRoute(state,  slot, referee);

            return _config.RankWeight * rankDiff + _config.DistanceWeight * route.DistanceKm;
        }

        // TODO: Asi do budoucna nevyuzitelna vec - pozdeji smazat
        public double AssignmentCost1(State state, Slot slot, Referee referee)
        {
            var existing = state.GetSlotsByReferee(referee)
                                .OrderBy(s => s.Start)
                                .ToList();

            var prev = existing.LastOrDefault(s => s.End <= slot.Start);
            var next = existing.FirstOrDefault(s => s.Start >= slot.End);

            double distIn = ComputeLegDistance(prev?.Location ?? referee.Location,
                                                slot.Location,
                                                prev?.End,
                                                slot.Start,
                                                referee.Location);

            double distOut = ComputeLegDistance(slot.Location,
                                                next?.Location ?? referee.Location,
                                                slot.End,
                                                next?.Start,
                                                referee.Location);

            // Úspora oproti přímé trase prev→next (marginální přírůstek)
            double distSaved = _distanceTable.GetRouteInfo(
                prev?.Location ?? referee.Location,
                next?.Location ?? referee.Location).DistanceKm;

            double marginalDistance = distIn + distOut - distSaved;

            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);
            return _config.RankWeight * rankDiff + _config.DistanceWeight * marginalDistance;
        }


        // TODO: Taky do budoucna smazat - patri nepouzivane AssignmentCost1
        private double ComputeLegDistance(
            Geo from,
            Geo to,
            DateTime? departureTime,
            DateTime? arrivalTime,
            Geo home)
        {
            double directDistance = _distanceTable.GetRouteInfo(from, to).DistanceKm;
            double viaHomeDistance = _distanceTable.GetRouteInfo(from, home).DistanceKm
                                   + _distanceTable.GetRouteInfo(home, to).DistanceKm;

            if (directDistance == 0 && viaHomeDistance == 0) return 0;

            // Absolutní pravidlo: velká mezera → vždy domů
            bool absoluteReturn = departureTime.HasValue && arrivalTime.HasValue
                && (arrivalTime.Value - departureTime.Value) >= _config.HomeReturnMaxGap;

            if (absoluteReturn)
                return viaHomeDistance;

            // Poměrové rozhodnutí: mezera (min) / (přímá vzdálenost + 1)
            if (departureTime.HasValue && arrivalTime.HasValue)
            {
                double gapMinutes = (arrivalTime.Value - departureTime.Value).TotalMinutes;
                double score = gapMinutes / (directDistance + 1);

                if (score >= _config.HomeReturnScoreThreshold)
                    return viaHomeDistance;
            }

            // Výchozí: kratší trasa vyhrává
            return Math.Min(directDistance, viaHomeDistance);
        }

        /*
         * Vypocet ceny celeho stavu
         * TODO: Upravit s vyuzitim noveho RouteSolveru
         */
        public double TotalCost(State state)
        {
            double total = 0;

            var byReferee = state
                .Where(kv => kv.Value != null)
                .GroupBy(kv => kv.Value!);

            foreach (var group in byReferee)
            {
                var referee = group.Key;
                var slots = group
                    .Select(kv => kv.Key)
                    .OrderBy(s => s.Start)
                    .ToList();

                // Rank rozdíl
                foreach (var slot in slots)
                    total += _config.RankWeight * Math.Abs(slot.RequiredRank - referee.Rank);

                // Skutecna trasa
                var locs = new List<Geo> { referee.Location };
                locs.AddRange(slots.Select(s => s.Location));
                locs.Add(referee.Location);

                for (int i = 0; i < locs.Count - 1; i++)
                    total += _config.DistanceWeight
                             * _distanceTable.GetRouteInfo(locs[i], locs[i + 1]).DistanceKm;
            }

            return total;
        }

        // Vypocet dolni meze pro neohodnocene sloty v danem stavu.
        public double LowerBoundForSlots(
            State state,
            IEnumerable<Slot> emptySlots,
            IReadOnlyList<Referee> referees,
            ConflictChecker conflictChecker)
        {
            double lb = 0;
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
                    return 1_000_000; // Okamžitý pruning

                lb += minCost;
            }
            return lb;
        }

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
                // Pokud neexistuje žádný způsobilý rozhodčí, přidáme velkou penalizaci
                lb += minCost == double.MaxValue ? 1_000_000 : minCost;
            }
            return lb;
        }
    }
}
