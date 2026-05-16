using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{

    /// <summary>
    /// Základová třída pro všechny solvery, implementující společné rozhraní <see cref="ISolver"/>.
    /// </summary>
    public abstract class SolverBase : ISolver
    {
        public event Action<SolverEvent>? OnEvent;

        public abstract State Solve(IEnumerable<Slot> slots);

        public abstract State Solve(State state);

        /// <summary>
        /// Emitování události s automatickým nastavením zdroje na název solveru
        /// </summary>
        /// <param name="e">Událost solveru, která bude emitována</param>
        protected void Emit(SolverEvent e) => OnEvent?.Invoke(e with { CallStack = [GetType()] });

        /// <summary>
        /// Emitování události s automatickým nastavením zdroje na název solveru a přidáním původního zdroje pro sledování řetězce volání solverů
        /// </summary>
        /// <param name="e">Událost solveru, která bude emitována</param>
        protected void Forward(SolverEvent e) => OnEvent?.Invoke(e with { CallStack = [GetType(), .. e.CallStack] });

    }
}
