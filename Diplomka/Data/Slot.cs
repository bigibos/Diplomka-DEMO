using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Data
{
    public class Slot
    {
        public int Id {  get; private set; }
        public int Level { get; private set; } = 0; 

        public int Day { get; private set; } = 0;

        public Geo Location { get; private set; }

        public Slot(int id, int level, int day, Geo location)
        {
            Id = id;
            Level = level;
            Day = day;
            Location = location;
        }

        public override string ToString()
        {
            return $"Slot {Id}";
        }
    }
}
