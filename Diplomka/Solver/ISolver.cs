using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public interface ISolver
    {
        State Solve(IEnumerable<Slot> slots);
        State Solve(State state);

    }
}
