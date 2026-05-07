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
    /// <summary>
    /// Logika pro mapování DTO do entit resp. entit do DTO
    /// </summary>
    public static class CsvMapper
    {
        /// <summary>
        /// Mapuje DTO slotu na datovou entitu použivanou v business logice
        /// </summary>
        /// <param name="dto">DTO slotu mapování</param>
        /// <returns>Vytvořená datová entita</returns>
        public static Slot ToEntity(SlotCsvDto dto)
        {
            return new Slot
            {
                Id = dto.Id,
                Name = dto.Name,
                RequiredRank = dto.RequiredRank,
                Location = new Geo(dto.Lat, dto.Lon),
                Start = new DateTime(dto.StartYear, dto.StartMonth, dto.StartDay, dto.StartHour, dto.StartMinute, 0),
                End = new DateTime(dto.EndYear, dto.EndMonth, dto.EndDay, dto.EndHour, dto.EndMinute, 0)
            };
        }

        /// <summary>
        /// Mapuje DTO rozhodčího na datovou entitu použivanou v business logice
        /// </summary>
        /// <param name="dto">DTO rozhodčího pro namapování</param>
        /// <returns>Namapovaná datová entita</returns>
        public static Referee ToEntity(RefereeCsvDto dto)
        {
            return new Referee
            {
                Id = dto.Id,
                Name = dto.Name,
                Rank = dto.Rank,
                Location = new Geo(dto.Lat, dto.Lon)
            };
        }

        /// <summary>
        /// Mapuje datovou entitu slotu na DTO pro uložení do CSV
        /// </summary>
        /// <param name="entity">Datová entita slotu pro namapování</param>
        /// <returns>Namapované DTO</returns>
        public static SlotCsvDto ToDto(Slot entity)
        {
            return new SlotCsvDto
            {
                Id = entity.Id,
                Name = entity.Name,
                RequiredRank = entity.RequiredRank,
                Lat = entity.Location.Lat,
                Lon = entity.Location.Lon,

                StartDay = entity.Start.Day,
                StartMonth = entity.Start.Month,
                StartYear = entity.Start.Year,
                StartHour = entity.Start.Hour,
                StartMinute = entity.Start.Minute,

                EndDay = entity.End.Day,
                EndMonth = entity.End.Month,
                EndYear = entity.End.Year,
                EndHour = entity.End.Hour,
                EndMinute = entity.End.Minute
            };
        }

        /// <summary>
        /// Mapuje datovou entitu rozhodčího na DTO pro uložení do CSV
        /// </summary>
        /// <param name="entity">Datová entita rozhodčího pro namapování</param>
        /// <returns>Namapované DTO</returns>
        public static RefereeCsvDto ToDto(Referee entity)
        {
            return new RefereeCsvDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Rank = entity.Rank,
                Lat = entity.Location.Lat,
                Lon = entity.Location.Lon
            };
        }
    }
}
