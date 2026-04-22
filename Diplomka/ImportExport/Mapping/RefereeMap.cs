using CsvHelper.Configuration;
using Diplomka.ImportExport.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Mapping
{
    public sealed class RefereeMap : ClassMap<RefereeCsvDto>
    {
        public RefereeMap()
        {
            Map(m => m.Id).Name("Id");  
            Map(m => m.Name).Name("Name");
            Map(m => m.Rank).Name("Rank");
            Map(m => m.Lat).Name("Lat");
            Map(m => m.Lon).Name("Lon");
        }
    }
}
