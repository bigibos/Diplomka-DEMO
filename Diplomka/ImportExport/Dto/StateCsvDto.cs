using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    /// <summary>
    /// Data Transfer Object (DTO) pro ukládání stavu do CSV
    /// </summary>
    public record StateCsvDto
    {
        public string Slot { get; set; } = String.Empty;
        public string Referee { get; set; } = String.Empty;
        public string From { get; set; } = String.Empty;
        public string To { get; set; } = String.Empty;

        public int? Rank { get; set; } = null;
        public int? RequiredRank { get; set; } = null;
        public int? RankDiff {  get; set; } = null;

        public double? TravelDistance { get; set; } = null;
        public double? TravelTime { get; set; } = null;
        public string TravelFrom { get; set; } = String.Empty;
        public string TravelTo { get; set; } = String.Empty;
        public bool? TravelFromHome { get; set; } = null;
    }
}
