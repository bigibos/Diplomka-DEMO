using Diplomka.Entity;
using Diplomka.Routing;

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

        // Vypocet ceny celehoho stavu
        public double TotalCost(State state)
        {
            double total = 0;
            foreach (var (slot, referee) in state)
            {
                if (referee != null)
                    total += AssignmentCost(slot, referee);
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
