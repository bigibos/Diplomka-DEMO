using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Model
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;


        override public string ToString()
        {
            return $"Team: {Name}";
        }
    }
}
