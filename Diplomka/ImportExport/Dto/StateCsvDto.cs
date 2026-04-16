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

        public double Distance { get; set; }
    }
}
