using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Data
{
    public class Referee
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public int Level { get; private set; } = 0;

        public Geo Location { get; private set; }

        public Referee(int id, string name, int level, Geo location)
        {
            Id = id; 
            Name = name;
            Level = level;
            Location = location;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
