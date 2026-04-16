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
        public static List<StateCsvDto> ToDtoList(State state)
        {
            var result = new List<StateCsvDto>();

            foreach (var (slot, referee) in state)
            {
                if (referee == null)
                    continue;

                result.Add(new StateCsvDto
                {
                    Referee = referee.Name,
                    From = slot.Start.ToString("dd.MM.yyyy HH:mm"),
                    To = slot.End.ToString("dd.MM.yyyy HH:mm"),
                    Rank = referee.Rank,
                    RequiredRank = slot.RequiredRank,
                    Distance = Math.Round(referee.Location.DistanceTo(slot.Location), 2)
                });
            }

            return result;
        }
    }
}
