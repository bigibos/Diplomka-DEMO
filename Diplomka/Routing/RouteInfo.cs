using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Routing
{
    /// <summary>
    /// Záznam pro uchovávání trasových údajů.
    /// Je primárně využíván při výpočtech tras.
    /// </summary>
    public record RouteInfo
    {
        [Required]
        public Geo From { get; set; } = null!;

        [Required]
        public Geo To { get; set; } = null!;

        [Range(0, double.MaxValue)]
        public double DistanceKm { get; set; }

        [Required]
        public TimeSpan Duration { get; set; }
    }
}
