using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class SolverConfiguration
    {
        public TimeSpan RefereePrepTime { get; set; } = TimeSpan.FromMinutes(90);
        public TimeSpan RefereePostpTime { get; set;} = TimeSpan.FromMinutes(120);

    }
}
