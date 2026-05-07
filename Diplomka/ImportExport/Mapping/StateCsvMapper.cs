using Diplomka.ImportExport.Dto;
using Diplomka.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Mapping
{
    /// <summary>
    /// Pravidla mapování stavu mezi entitou a DTO
    /// </summary>
    public static class StateCsvMapper
    {
        public static List<StateCsvDto> ToDtoList(State state, RouteSolver routeSolver)
        {
            var result = new List<StateCsvDto>();

            foreach (var (slot, referee) in state)
            {
                // Namapovani slotu
                var dto = new StateCsvDto
                {
                    Slot = slot.Name,
                    From = slot.Start.ToString("dd.MM.yyyy HH:mm"),
                    To = slot.End.ToString("dd.MM.yyyy HH:mm"),
                    RequiredRank = slot.RequiredRank,
                };

                var route = routeSolver.ComputeOptimalRoute(state, slot, referee);
                
                // Mapovani rozodciho a trasy, pokud existuji
                if (referee != null && route != null)
                {
                    dto = dto with {
                        Referee = referee.Name,
                        Rank = referee.Rank,
                        RankDiff = Math.Abs(referee.Rank - slot.RequiredRank),
                        TravelDistance = Math.Round(route.DistanceKm, 2),
                        TravelTime = Math.Round(route.Duration.TotalHours, 2),
                        TravelFrom = route.From.ToReadableDecimal(),
                        TravelTo = route.To.ToReadableDecimal(),
                        TravelFromHome = route.From.Equals(referee.Location)
                    };
                }

                result.Add(dto);
            }

            return result;
        }
    }
}
