using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Model
{
    public class Slot
    {
        public int Id {  get; private set; }
        public int RequiredRank { get; private set; } = 0; 

        public Match Match { get; set; }

        public Slot(int id, int requiredRank, Match match)
        {
            Id = id;
            RequiredRank = requiredRank;
            Match = match;
        }

        public override string ToString()
        {
            return $"Slot {Id}";
        }
    }
}
