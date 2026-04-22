using Diplomka.ImportExport.Dto;
using Diplomka.Entity;
using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Mapping
{
    public static class CsvMapper
    {
        public static Slot ToEntity(SlotCsvDto dto, int id)
        {
            return new Slot
            {
                Id = dto.Id,
                RequiredRank = dto.RequiredRank,
                Location = new Geo(dto.Lat, dto.Lon),
                Start = new DateTime(dto.StartYear, dto.StartMonth, dto.StartDay, dto.StartHour, dto.StartMinute, 0),
                End = new DateTime(dto.EndYear, dto.EndMonth, dto.EndDay, dto.EndHour, dto.EndMinute, 0)
            };
        }

        public static Referee ToEntity(RefereeCsvDto dto, int id)
        {
            return new Referee
            {
                Id = dto.Id,
                Name = dto.Name,
                Rank = dto.Rank,
                Location = new Geo(dto.Lat, dto.Lon)
            };
        }

        public static SlotCsvDto ToDto(Slot s)
        {
            return new SlotCsvDto
            {
                RequiredRank = s.RequiredRank,
                Lat = s.Location.Lat,
                Lon = s.Location.Lon,

                StartDay = s.Start.Day,
                StartMonth = s.Start.Month,
                StartYear = s.Start.Year,
                StartHour = s.Start.Hour,
                StartMinute = s.Start.Minute,

                EndDay = s.End.Day,
                EndMonth = s.End.Month,
                EndYear = s.End.Year,
                EndHour = s.End.Hour,
                EndMinute = s.End.Minute
            };
        }

        public static RefereeCsvDto ToDto(Referee r)
        {
            return new RefereeCsvDto
            {
                Name = r.Name,
                Rank = r.Rank,
                Lat = r.Location.Lat,
                Lon = r.Location.Lon
            };
        }
    }
}
