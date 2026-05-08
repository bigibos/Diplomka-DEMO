using Diplomka.Validators;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Entity
{
    /// <summary>
    /// Datová entita zápasového slotu
    /// </summary>
    public class Slot
    {
        [Range(0, int.MaxValue)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Jméno je povinné.")]
        [MinLength(1, ErrorMessage = "Jméno nesmí být prázdné.")]
        [MaxLength(100)]
        public string Name {  get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "Požadovaná úroveň musí být nezáporná.")]
        public int RequiredRank { get; set; } = 0;
        
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

        public DateTime Start { get; set; } = DateTime.MinValue;

        [DateRange(nameof(Start))]
        public DateTime End { get; set; } = DateTime.MinValue;

        public Slot() { }

        public override string ToString()
        {
            return $"Slot: {RequiredRank}, {Location}, {Start:yyyy-MM-dd HH:mm} - {End:yyyy-MM-dd HH:mm}";
        }

        public override bool Equals(object? obj)
        {
            if (obj is Slot other)
            {
                return 
                    Id == other.Id &&
                    Name == other.Name &&
                    RequiredRank == other.RequiredRank &&
                    Equals(Location, other.Location) &&
                    Start == other.Start &&
                    End == other.End;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, RequiredRank, Location, Start, End);
        }   
    }
}
