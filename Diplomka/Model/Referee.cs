using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Model
{
    public class Referee
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Rank { get; set; } = 0;

        public Geo Location { get; set; }

        public Referee(int id, string name, int rank, Geo location)
        {
            Id = id; 
            Name = name;
            Rank = rank;
            Location = location;
        }

        public override string ToString()
        {
            return $"{Name}, {Rank}, {Location}";
        }
    }
}
