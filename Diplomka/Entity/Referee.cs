using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Entity
{
    public class Referee
    {
        public int Id { get; set; } 
        public string Name { get; set; } = string.Empty;
        public int Rank { get; set; } = 0;

        public Geo Location { get; set; }

        public override string ToString()
        {
            return $"{Name}, {Rank}, {Location}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is Referee other)
            {
                return 
                    Id == other.Id && 
                    Name == other.Name && 
                    Rank == other.Rank && 
                    Location.Equals(other.Location);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, Rank, Location);
        }
    }
}
