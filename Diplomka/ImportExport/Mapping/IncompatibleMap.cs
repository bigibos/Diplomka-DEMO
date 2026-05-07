using CsvHelper.Configuration;
using Diplomka.ImportExport.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Mapping
{
    public sealed class IncompatibleMap : ClassMap<IncompatibleCsvDto>
    {
        public IncompatibleMap()
        {
            Map(m => m.RefereeIdA).Name("RefereeIdA");
            Map(m => m.RefereeIdB).Name("RefereeIdB");
        }
    }
}
