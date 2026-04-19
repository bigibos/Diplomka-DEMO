using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Model
{
    public class Slot
    {
        public int Id {  get; set; }
        public int RequiredRank { get; set; } = 0;
        public Geo? Location { get; set; }

        public DateTime Start { get; set; } = DateTime.MinValue;
        public DateTime End { get; set; } = DateTime.MinValue;


        public override string ToString()
        {
            string result = "";
            result += $"Slot: {RequiredRank}, {Location}, {Start:yyyy-MM-dd HH:mm} - {End:yyyy-MM-dd HH:mm}";

            return result;
        }
    }
}
