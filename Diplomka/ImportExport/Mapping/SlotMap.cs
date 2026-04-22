using CsvHelper.Configuration;
using Diplomka.ImportExport.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Mapping
{
    public sealed class SlotMap : ClassMap<SlotCsvDto>
    {
        public SlotMap()
        {
            Map(m => m.Id).Name("Id");
            Map(m => m.RequiredRank).Name("RequiredRank");
            Map(m => m.Lat).Name("Lat");
            Map(m => m.Lon).Name("Lon");

            Map(m => m.StartDay).Name("StartDay");
            Map(m => m.StartMonth).Name("StartMonth");
            Map(m => m.StartYear).Name("StartYear");
            Map(m => m.StartHour).Name("StartHour");
            Map(m => m.StartMinute).Name("StartMinute");

            Map(m => m.EndDay).Name("EndDay");
            Map(m => m.EndMonth).Name("EndMonth");
            Map(m => m.EndYear).Name("EndYear");
            Map(m => m.EndHour).Name("EndHour");
            Map(m => m.EndMinute).Name("EndMinute");
        }
    }
}
