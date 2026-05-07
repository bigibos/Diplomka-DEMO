using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    /// <summary>
    /// Data Transfer Object (DTO) pro načítání dat rozhodčích z CSV
    /// </summary>
    public record RefereeCsvDto
    {
        [Range(0, int.MaxValue)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Rank { get; set; }

        [Range(-90, 90)]
        public double Lat { get; set; }

        [Range(-180, 180)]
        public double Lon { get; set; }

        [Range(0, 1)]
        public int HasCar { get; set; } = 1;
    }
}
