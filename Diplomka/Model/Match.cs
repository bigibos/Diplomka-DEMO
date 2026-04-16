using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Model
{
    public class Match
    {
        public int Id { get; set; }
        public Geo? Location { get; set; }

        public DateTime Start { get; set; } = DateTime.MinValue;
        public DateTime End { get; set; } = DateTime.MinValue;

        public Team? Home { get; set; }
        public Team? Away { get; set; }

        public List<Slot> Slots { get; private set; } = new List<Slot>();


        public override string ToString()
        {
            string result = "";

            result += $"Match: {Home?.Name ?? "TBD"} vs {Away?.Name ?? "TBD"}\n";

            result += $"  Location: {Location}\n";
            result += $"  Time: {Start:yyyy-MM-dd HH:mm} - {End:yyyy-MM-dd HH:mm}\n";
            result += $"  Slots:\n";
            result += string.Join("\n", Slots.Select(s => $"    - {s}"));
            result += "\n";

            return result;
        }
    }
}
