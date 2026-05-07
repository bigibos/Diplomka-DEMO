using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Entity
{
    public class Referee
    {
        [Range(0, int.MaxValue)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Jméno je povinné.")]
        [MinLength(1, ErrorMessage = "Jméno nesmí být prázdné.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "Úroveň musí být nezáporná.")]
        public int Rank { get; set; } = 0;

        [Required(ErrorMessage = "Lokace je povinná.")]
        public Geo Location { get; set; } = null!;

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
                    (Location?.Equals(other.Location) ?? false);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, Rank, Location);
        }
    }
}
