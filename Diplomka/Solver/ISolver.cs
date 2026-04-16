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
        State Solve(List<Slot> slots, List<Referee> referees);
        State Solve(State state, List<Referee> referees);
        int AssignmentCost(Slot slot, Referee referee);
        int StateCost(State state);

    }
}
