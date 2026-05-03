using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    public class StateCsvDto
    {
        public string Slot { get; set; } = String.Empty;
        public string Referee { get; set; } = String.Empty;
        public string From { get; set; } = String.Empty;
        public string To { get; set; } = String.Empty;

        public int Rank { get; set; }
        public int RequiredRank { get; set; }
        public int RankDiff {  get; set; }

        public double TravelDistance { get; set; }
        public double TravelTime {  get; set; }
        public string TravelFrom { get; set; } = String.Empty;
        public string TravelTo { get; set; } = String.Empty;
        public bool TravelFromHome { get; set; }
    }
}
