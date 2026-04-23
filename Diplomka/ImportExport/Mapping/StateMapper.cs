using Diplomka.ImportExport.Dto;
using Diplomka.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Mapping
{
    public static class StateCsvMapper
    {
        public static List<StateCsvDto> ToDtoList(State state, RouteSolver routeSolver)
        {
            var result = new List<StateCsvDto>();


            foreach (var (slot, referee) in state)
            {
                if (referee == null)
                    continue;

                var route = routeSolver.ComputeOptimalRoute(state, slot, referee);

                result.Add(new StateCsvDto
                {
                    Referee = referee.Name,
                    From = slot.Start.ToString("dd.MM.yyyy HH:mm"),
                    To = slot.End.ToString("dd.MM.yyyy HH:mm"),
                    Rank = referee.Rank,
                    RequiredRank = slot.RequiredRank,
                    RankDiff = Math.Abs(referee.Rank - slot.RequiredRank), 
                    TravelDistance = route.DistanceKm,
                    TravelTime = route.Duration.TotalHours,
                    TravelFrom = route.From.ToReadableDecimal(),
                    TravelTo = route.To.ToReadableDecimal(),
                    TravelFromHome = route.From.Equals(referee.Location),
                    // TODO: Tady musi byt realna vypocitana trasa, jako je v assignment cost (asi brat ze stavu nevim)
                    // Distance = Math.Round(referee.Location.DistanceTo(slot.Location), 2) 
                });
            }

            return result;
        }
    }
}
