using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    /// <summary>
    /// Data Transfer Object (DTO) pro načítání dat zápasových slotů z CSV
    /// </summary>
    public record SlotCsvDto
    {
        [Range(0, int.MaxValue)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int RequiredRank { get; set; }

        [Range(-90, 90)]
        public double Lat { get; set; }

        [Range(-180, 180)]
        public double Lon { get; set; }


        [Range(1, 31)]
        public int StartDay { get; set; }

        [Range(1, 12)]
        public int StartMonth { get; set; }

        [Range(1900, 3000)]
        public int StartYear { get; set; }

        [Range(0, 23)]
        public int StartHour { get; set; }

        [Range(0, 59)]
        public int StartMinute { get; set; }

        [Range(1, 31)]
        public int EndDay { get; set; }

        [Range(1, 12)]
        public int EndMonth { get; set; }

        [Range(1900, 3000)]
        public int EndYear { get; set; }

        [Range(0, 23)]
        public int EndHour { get; set; }

        [Range(0, 59)]
        public int EndMinute { get; set; }
    }
}
