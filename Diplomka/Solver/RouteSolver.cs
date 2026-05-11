using Diplomka.Entity;
using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    /// <summary>
    /// Třídá sloužící k zjištění optimální trasy pro rozhodčího.
    /// V zásadě se zde vyhodnocuje, jestli bude rozhodší v rámci přesunů mezi sloty cestovat přímo, nebo se bude vracet domů
    /// </summary>
    public class RouteSolver
    {
        private RouteTable _distanceTable;
        private SolverConfiguration _config;

        public RouteSolver(RouteTable distanceTable, SolverConfiguration config)
        {
            _distanceTable = distanceTable;
            _config = config;
        }


        // TODO: Null hodnoty
        /// <summary>
        /// Hlavní výpočetní metoda pro zjištění optimální trasy.
        ///     1) Seřazení slotů, která má rozhodčí přiřazený, chronolgicky podle času
        ///     2) Získání předcházejícího slotu vučí aktuálně zkoumanému
        ///     3) Zjištení časového okna mezi sloty a získání tras:
        ///         a) Z předchozího slotu do aktuálního slotu
        ///         b) Z předchozího slotu do zázemí
        ///         c) Z aktuálního slotu do zázemí
        ///     4) Výpočet promarněného času při cestě:
        ///         a) předchozí -> zázemí -> aktuální
        ///         b) předchozí -> aktuální
        ///     5) Promarněný čas implicitně ukazuje i čas potřebný na cestu - čím dělší promarněný čas, tím kratší čas na přesun
        ///     6) Vybere se větší promarněný čas z obou výpočtů a zkontroluje se vúči golbálně nastavenému maixmu
        ///     7) Nakonec je vybrána trassa s největším promarněným časem, který neporušuje maximum (jako fallback je vždy trasa přes domov)
        /// </summary>
        /// <param name="state">Stav pro kontext a hledání sousedů</param>
        /// <param name="slot">Slot k vyhodnocení</param>
        /// <param name="referee">Rozhodčí k vyhodnocení</param>
        /// <returns>Optimálně zvolenou trasu</returns>
        public RouteInfo? ComputeOptimalRoute(State state, Slot slot, Referee? referee)
        {
            if (referee == null)
                return null;

            // Seřaď existující sloty rozhodčího chronologicky
            var existing = state.GetSlotsByReferee(referee)
                                .OrderBy(s => s.Start)
                                .ToList();

            // Najdi sousedy v časové sekvenci
            var prev = existing.LastOrDefault(s => s.End <= slot.Start);

            var timeWindowPrev = prev != null ? (slot.Start - prev.End) : TimeSpan.Zero;

            var routePrev = prev != null ? _distanceTable.GetRouteInfo(prev.Location, slot.Location) : null;
            var routeHomePrev = prev != null ? _distanceTable.GetRouteInfo(prev.Location, referee.Location) : null;
            var routeHome = _distanceTable.GetRouteInfo(referee.Location, slot.Location);

            if (prev == null)
                return routeHome;
           
            // Vypocet promarneneho casu
            var homeWasteTime = timeWindowPrev - (routeHomePrev.Duration + routeHome.Duration);
            var prevWasteTime = timeWindowPrev - routePrev.Duration;

            var wasteTime = homeWasteTime > prevWasteTime ? homeWasteTime : prevWasteTime;

            if (wasteTime > _config.MaxWasteTime)
                return routeHome;

            return routePrev;

        }
    }
}
