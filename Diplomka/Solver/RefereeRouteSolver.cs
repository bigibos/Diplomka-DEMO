using Diplomka.Entity;
using Diplomka.Routing;

namespace Diplomka.Solver
{
    /// <summary>
    /// Rozhoduje, kudy se rozhodčí mezi sloty přesune —
    /// přímo (prev → next) nebo přes zázemí (prev → home → next).
    /// 
    /// Logika rozhodnutí (od nejsilnějšího pravidla):
    ///   1. Absolutní mezera ≥ HomeReturnMaxGap        → vždy domů
    ///   2. Skóre (mezera v min / přímá vzdálenost+1) ≥ HomeReturnScoreThreshold → domů
    ///   3. Výchozí: kratší trasa vyhrává
    /// </summary>
    public class RefereeRouteSolver
    {
        private readonly DistanceTable _distanceTable;
        private readonly SolverConfiguration _config;

        public RefereeRouteSolver(DistanceTable distanceTable, SolverConfiguration config)
        {
            _distanceTable = distanceTable;
            _config = config;
        }

        // ── Veřejné API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Vrátí délku (km) jednoho úseku cesty, přičemž rozhodne
        /// zda jet přímo nebo přes zázemí.
        /// </summary>
        public double ComputeLegDistance(
            Geo from,
            Geo to,
            DateTime? departureTime,
            DateTime? arrivalTime,
            Geo home)
        {
            double directKm = _distanceTable.GetRouteInfo(from, to).DistanceKm;
            double viaHomeKm = _distanceTable.GetRouteInfo(from, home).DistanceKm
                              + _distanceTable.GetRouteInfo(home, to).DistanceKm;

            if (directKm == 0 && viaHomeKm == 0)
                return 0;

            if (ShouldReturnHome(departureTime, arrivalTime, directKm))
                return viaHomeKm;

            return Math.Min(directKm, viaHomeKm);
        }

        /// <summary>
        /// Vrátí délku příjezdového úseku pro přiřazovaný slot v kontextu
        /// aktuálního stavu (hledá předchozí slot rozhodčího).
        /// Vhodné pro použití v CostCalculator.
        /// </summary>
        public double GetIncomingLegDistance(State state, Slot slot, Referee referee)
        {
            var prev = state.GetSlotsByReferee(referee)
                            .OrderBy(s => s.Start)
                            .LastOrDefault(s => s.End <= slot.Start);

            return ComputeLegDistance(
                from: prev?.Location ?? referee.Location,
                to: slot.Location,
                departureTime: prev?.End,
                arrivalTime: slot.Start,
                home: referee.Location);
        }

        /// <summary>
        /// Sestaví úplnou trasu rozhodčího pro finální stav:
        ///   zázemí → slot[0] → slot[1] → ... → slot[n] → zázemí
        /// 
        /// Vrátí seznam <see cref="RouteLeg"/> kde každý leg popisuje
        /// jeden úsek včetně vzdálenosti a příznaku průjezdu přes zázemí.
        /// Vhodné pro export.
        /// </summary>
        public List<RouteLeg> GetFullRoute(Referee referee, IEnumerable<Slot> slots)
        {
            var ordered = slots.OrderBy(s => s.Start).ToList();
            var legs = new List<RouteLeg>(ordered.Count + 1);

            // Sestavíme sekvenci zastávek: home + sloty + home
            var stops = new List<(Geo Location, DateTime? Time)>
            {
                (referee.Location, null)
            };

            foreach (var s in ordered)
            {
                stops.Add((s.Location, s.Start));
            }

            stops.Add((referee.Location, null));

            // Pro každý úsek rozhodneme přímá vs. přes zázemí
            for (int i = 0; i < stops.Count - 1; i++)
            {
                var from = stops[i];
                var to = stops[i + 1];

                // departureTime = konec předchozí zastávky (pro sloty: s.End)
                DateTime? departure = i == 0
                    ? null
                    : ordered[i - 1].End;           // ordered je o 1 kratší než stops

                bool viaHome = i > 0 && i < stops.Count - 1
                    && ShouldReturnHome(departure, to.Time,
                           _distanceTable.GetRouteInfo(from.Location, to.Location).DistanceKm);

                double km = viaHome
                    ? _distanceTable.GetRouteInfo(from.Location, referee.Location).DistanceKm
                      + _distanceTable.GetRouteInfo(referee.Location, to.Location).DistanceKm
                    : _distanceTable.GetRouteInfo(from.Location, to.Location).DistanceKm;

                legs.Add(new RouteLeg(
                    From: from.Location,
                    To: to.Location,
                    DistanceKm: km,
                    ViaHome: viaHome));
            }

            return legs;
        }

        // ── Privátní pomocné metody ────────────────────────────────────────────────

        private bool ShouldReturnHome(DateTime? departure, DateTime? arrival, double directKm)
        {
            if (!departure.HasValue || !arrival.HasValue)
                return false;

            var gap = arrival.Value - departure.Value;

            // Pravidlo 1: absolutní časová mezera
            if (gap >= _config.HomeReturnMaxGap)
                return true;

            // Pravidlo 2: poměrové skóre (mezera vs. vzdálenost)
            double score = gap.TotalMinutes / (directKm + 1);
            return score >= _config.HomeReturnScoreThreshold;
        }
    }

    /// <summary>Jeden úsek trasy rozhodčího.</summary>
    public record RouteLeg(
        Geo From,
        Geo To,
        double DistanceKm,
        bool ViaHome);
}