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

        public CostCalculator(DistanceTable distanceTable, SolverConfiguration config)
        {
            _config = config;   
            _distanceTable = distanceTable;
        }

        // Vypocet ceny prirazeni rozhodci ke slotu
        public double AssignmentCost(Slot slot, Referee referee)
        {
            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);

            double distance = _distanceTable.GetRouteInfo(referee.Location, slot.Location!).DistanceKm;
            return _config.RankWeight * rankDiff + _config.DistanceWeight * distance;
        }

        // Vypocet ceny prirazeni rozhodci ke slotu
        public double AssignmentCost(State state, Slot slot, Referee referee)
        {
            // Seřaď existující sloty rozhodčího chronologicky
            var existing = state.GetSlotsByReferee(referee)
                                .OrderBy(s => s.Start)
                                .ToList();

            // Najdi sousedy v časové sekvenci
            var prev = existing.LastOrDefault(s => s.End <= slot.Start);
            var next = existing.FirstOrDefault(s => s.Start >= slot.End);

            var fromLoc = prev?.Location ?? referee.Location;  // domov, pokud žádný předchůdce
            var toLoc = next?.Location ?? referee.Location;  // domov, pokud žádný následník
                                                             // (nebo null pokud návrat neuvažuješ)

            double distIn = _distanceTable.GetRouteInfo(fromLoc, slot.Location).DistanceKm;
            double distOut = _distanceTable.GetRouteInfo(slot.Location, toLoc).DistanceKm;
            double distSaved = _distanceTable.GetRouteInfo(fromLoc, toLoc).DistanceKm;

            double marginalDistance = distIn + distOut - distSaved;

            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);
            return _config.RankWeight * rankDiff + _config.DistanceWeight * marginalDistance;
        }

        // Vypocet ceny celehoho stavu
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

                // Skutečná trasa: domov → slot1 → slot2 → ... → domov
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
