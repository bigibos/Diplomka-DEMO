using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Data
{
    public class Match
    {
        public int Id { get; private set; }
        public Geo Location { get; private set; }

        public DateTime Start { get; set; } = DateTime.MinValue;
        public DateTime End { get; set; } = DateTime.MinValue;

        public Team? Home { get; set; }
        public Team? Away { get; set; }

        public List<Slot> Slots { get; private set; } = new List<Slot>();

        public Match(Geo location)
        {
            Location = location;
        }
    }
}
