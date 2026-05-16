using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    /// <summary>
    /// Typy událostí, které mohou být emitovány při spouštění solveru
    /// </summary>
    public abstract record SolverEvent()
    {
        public IReadOnlyList<Type> CallStack { get; init; } = [];
        public record StartEvent(double? InitialCost = null, string Message = "") : SolverEvent;
        public record FinishEvent(double FinalCost, string Message = "") : SolverEvent;
        public record ImprovementEvent(double PreviousCost, double NewCost, string Message = "") : SolverEvent;
        public record InfoEvent(string Message = "") : SolverEvent;
        public record TimeOutEvent(string Message = "") : SolverEvent;
        public record TimeCheckEvent(long ElapsedMs, long? NodesExplored = null, string Message = "") : SolverEvent;
    }
}
