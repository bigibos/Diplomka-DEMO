using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    public class RefereeCsvDto
    {
        public string Name { get; set; } = "";
        public int Rank { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
