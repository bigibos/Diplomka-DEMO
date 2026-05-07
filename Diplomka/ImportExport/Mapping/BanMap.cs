using CsvHelper.Configuration;
using Diplomka.ImportExport.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Mapping
{
    public sealed class BanMap : ClassMap<BanCsvDto>
    {
        public BanMap()
        {
            Map(m => m.RefereeId).Name("RefereeId");
            Map(m => m.SlotId).Name("SlotId");
        }
    }
}
