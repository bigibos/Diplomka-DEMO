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
        public string Referee { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";

        public int Rank { get; set; }
        public int RequiredRank { get; set; }
        public int RankDiff {  get; set; }

        public double TravelDistance { get; set; }
        public double TravelTime {  get; set; }
        public string TravelFrom { get; set; } = "";
        public string TravelTo { get; set; } = "";
        public bool TravelFromHome { get; set; }
    }
}
