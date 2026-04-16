using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    public class SlotCsvDto
    {
        public int RequiredRank { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }

        public int StartDay { get; set; }
        public int StartMonth { get; set; }
        public int StartYear { get; set; }
        public int StartHour { get; set; }
        public int StartMinute { get; set; }

        public int EndDay { get; set; }
        public int EndMonth { get; set; }
        public int EndYear { get; set; }
        public int EndHour { get; set; }
        public int EndMinute { get; set; }
    }
}
