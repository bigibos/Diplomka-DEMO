using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    public record CarGroupCsvDto
    {
        public string Slot { get; set; } = String.Empty;
        public string From { get; set; } = String.Empty;
        public string To { get; set; } = String.Empty;

        public string TravelFrom { get; set; } = String.Empty;
        public string TravelTo { get; set; } = String.Empty;

        public string Driver { get; set; } = String.Empty;

        public string Passengers { get; set; } = String.Empty;
    }
}
