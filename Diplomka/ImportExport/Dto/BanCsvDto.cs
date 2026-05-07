using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport.Dto
{
    public record BanCsvDto
    {
        [Range(0, int.MaxValue)]
        public int RefereeId { get; set; }

        [Range(0, int.MaxValue)]
        public int SlotId { get; set; }
    }
}
