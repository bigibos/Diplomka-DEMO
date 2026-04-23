using Diplomka.Entity;
using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class RouteSolver
    {
        private DistanceTable _distanceTable;
        private SolverConfiguration _config;

        public RouteSolver(DistanceTable distanceTable, SolverConfiguration config)
        {
            _distanceTable = distanceTable;
            _config = config;
        }

        /*
         * Vypocet vhodne trasy pro rozhodciho
         * State slouzi jako kontext (jake sloty predchazi aktualnimu a jak se vypocita vzdalenost)
         */
        public RouteInfo ComputeOptimalRoute(State state, Slot slot, Referee referee)
        {
            // Seřaď existující sloty rozhodčího chronologicky
            var existing = state.GetSlotsByReferee(referee)
                                .OrderBy(s => s.Start)
                                .ToList();

            // Najdi sousedy v časové sekvenci
            var prev = existing.LastOrDefault(s => s.End <= slot.Start);
            var next = existing.FirstOrDefault(s => s.Start >= slot.End);


            /*
             * Zjistim predchozi a nasledujici slot
             * Pokud neexistuje, tak misto jeho lokace zvolim lokaci zazemi
             * Budeme pracovat s pomerem promarneho casu a procestovaneho casu
             * - Zjistim casove okno mezi sloty (prirazovany slot a sousedni slot)
             * - Zjistim procestovany cas mezi sloty (prirazovany slot a sousedni slot)
             * - Zjistim procestovany cas mezi sloty pres zazemi (prirazovany slot a sousedni slot pres zazemi)
             * -- Cas ze slotu do zazemi + cas ze zazemi do sousedniho slotu
             * - Veberu tu moznost kde pomer promarneho casu a procestovaneho casu je vetsi
             * -- Pokud je promarneny cas vetsi nez konfiguracni mez, rozhodnu se pro presun domu
             */
            var timeWindowPrev = prev != null ? (slot.Start - prev.End) : TimeSpan.Zero;

            var routePrev = prev != null ? _distanceTable.GetRouteInfo(prev.Location, slot.Location) : null;
            var routeHomePrev = prev != null ? _distanceTable.GetRouteInfo(prev.Location, referee.Location) : null;
            var routeHome = _distanceTable.GetRouteInfo(referee.Location, slot.Location);

            if (prev == null)
                return routeHome;
            

            /*
             * Waste time oznacuje promarneny cas - cili cas ktery zbyde z casoveho okne po odecteni cestovniho casu
             * Vybirame vetsi waste time, protoze ten nam zajisti mensi cestovni cas
             * Pokud je waste time vetsi nez nastavena mez, rozhodneme se pro presun ze zazemi
             */
            var homeWasteTime = timeWindowPrev - (routeHomePrev.Duration + routeHome.Duration);
            var prevWasteTime = timeWindowPrev - routePrev.Duration;

            var wasteTime = homeWasteTime > prevWasteTime ? homeWasteTime : prevWasteTime;

            if (wasteTime > _config.MaxWasteTime)
                return routeHome;

            return routePrev;

        }
    }
}
