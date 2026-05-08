using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Entity
{
    /// <summary>
    /// Datová entita rozhočího
    /// </summary>
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
        public Geo? Location { get; set; } = null;

        public double Lat
        {
            get => Location?.Lat ?? 0;
            set
            {
                if (Location == null) Location = new Geo(value, 0);
                else Location.Lat = value;
            }
        }

        public double Lon
        {
            get => Location?.Lon ?? 0;
            set
            {
                if (Location == null) Location = new Geo(0, value);
                else Location.Lon = value;
            }
        }

        public bool HasCar { get; set; } = true;
        public List<int> BannedSlotIds { get; set; } = new();
        public List<int> IncompatibleRefereeIds { get; set; } = new();

        public Referee() { }

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
