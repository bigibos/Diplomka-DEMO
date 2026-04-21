using Diplomka.Model;
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

        /// <summary>
        /// Cena jednoho přiřazení (slot → rozhodčí).
        /// </summary>
        public double AssignmentCost(Slot slot, Referee referee)
        {
            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);
            // double distance = referee.Location.DistanceTo(slot.Location

            double distance = _distanceTable.GetRouteInfo(referee.Location, slot.Location!).DistanceKm;
            return _config.RankWeight * rankDiff + _config.DistanceWeight * distance;
        }

        /// <summary>
        /// Celková cena celého stavu (pouze přiřazené sloty).
        /// </summary>
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

        /// <summary>
        /// Dolní mez ceny pro množinu neohodnocených slotů.
        /// Pro každý slot bereme minimum přes VŠECHNY rozhodčí (bez ohledu na konflikty).
        /// Tato mez je přípustná (nikdy nepřeceňuje) → lze použít v B&B.
        /// </summary>
        public double LowerBoundForSlots(IEnumerable<Slot> slots, IReadOnlyList<Referee> referees)
        {
            double lb = 0;
            foreach (var slot in slots)
            {
                double minCost = double.MaxValue;
                foreach (var referee in referees)
                {
                    if (referee.Rank >= slot.RequiredRank)           // pouze způsobilí rozhodčí
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
