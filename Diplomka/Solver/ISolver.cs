using Diplomka.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public interface ISolver
    {
        State Solve(List<Slot> slots, SolverConfiguration config);
        State Solve(State state, SolverConfiguration config);

    }
}
