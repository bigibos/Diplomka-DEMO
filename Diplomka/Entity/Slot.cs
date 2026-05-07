using Diplomka.Routing;
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
        public Geo Location { get; set; } = null!;

        public DateTime Start { get; set; } = DateTime.MinValue;

        [DateRange(nameof(Start))]
        public DateTime End { get; set; } = DateTime.MinValue;


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
                    Location.Equals(other.Location) &&
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
