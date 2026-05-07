using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    public record IncompatibleCsvDto
    {
        [Range(0, int.MaxValue)]
        public int RefereeIdA { get; set; }

        [Range(0, int.MaxValue)]
        public int RefereeIdB { get; set; }
    }
}
