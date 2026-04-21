using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class SolverConfiguration
    {
        // Casove parametry
        public TimeSpan RefereePrepTime { get; set; } = TimeSpan.FromMinutes(90);
        public TimeSpan RefereePostpTime { get; set;} = TimeSpan.FromMinutes(120);


        // Vahy pro vypocet cen
        public double RankWeight { get; set; } = 1.0;
        public double DistanceWeight { get; set; } = 1.0;
    }
}
