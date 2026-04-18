using Diplomka.Model;

namespace Diplomka.Solver
{
    /// <summary>
    /// Vypočítává cenu přiřazení rozhodčího ke slotu.
    /// Cena = váhovaný součet absolutního rozdílu hodnosti a vzdálenosti v km.
    /// </summary>
    public static class CostCalculator
    {
        // Váhy jednotlivých složek ceny – lze ladit
        public const double RankWeight     = 1.0;
        public const double DistanceWeight = 1.0;

        /// <summary>
        /// Cena jednoho přiřazení (slot → rozhodčí).
        /// </summary>
        public static double AssignmentCost(Slot slot, Referee referee)
        {
            double rankDiff = Math.Abs(slot.RequiredRank - referee.Rank);
            double distance = referee.Location.DistanceTo(slot.Location!);
            return RankWeight * rankDiff + DistanceWeight * distance;
        }

        /// <summary>
        /// Celková cena celého stavu (pouze přiřazené sloty).
        /// </summary>
        public static double TotalCost(State state)
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
        public static double LowerBoundForSlots(IEnumerable<Slot> slots, IReadOnlyList<Referee> referees)
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
