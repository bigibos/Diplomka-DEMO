using Diplomka.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka
{
    public class AppData
    {
        public List<Team> Teams { get; set; } = new();
        public List<Referee> Referees { get; set; } = new();
        public List<Match> Matches { get; set; } = new();
        public List<Slot> Slots { get; set; } = new();


        public override string ToString()
        {
            string result = "";

            result += "Teams:\n";
            foreach (var team in Teams)
                result += team + "\n";

            result += "Referees:\n";
            foreach (var referee in Referees)
                result += referee + "\n";

            result += "Matches:\n";
            foreach (var match in Matches)
                result += match + "\n";

            return result;
        }
    }

}
