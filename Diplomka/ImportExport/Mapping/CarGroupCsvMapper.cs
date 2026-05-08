using Diplomka.ImportExport.Dto;
using Diplomka.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Mapping
{
    public class CarGroupCsvMapper
    {
        public static List<CarGroupCsvDto> ToDtoList(List<CarGroupOptimizer.CarGroup> groups)
        {
            var result = new List<CarGroupCsvDto>();

            foreach (var group in groups)
            {
                var dto = new CarGroupCsvDto
                {
                    Slot = group.Slot.Name,
                    From = group.Slot.Start.ToString("dd.MM.yyyy HH:mm"),
                    To = group.Slot.End.ToString("dd.MM.yyyy HH:mm"),
                    TravelFrom = group.Slot.Location?.ToReadableDecimal() ?? string.Empty,
                    TravelTo = group.Driver.Location?.ToReadableDecimal() ?? string.Empty,
                    Driver = group.Driver.Name,
                    Passengers = string.Join(", ", group.Passengers.Select(p => p.Name))
                };

                result.Add(dto);
            }

            return result;
        }
    }
}
