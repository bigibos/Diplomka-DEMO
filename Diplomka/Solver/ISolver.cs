using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    /// <summary>
    /// Interface pro hlavní řešiče přiřazení
    /// </summary>
    public interface ISolver
    {
        /// <summary>
        /// Vytvoří stav řešení naplněním zadaného seznamu slotů
        /// </summary>
        /// <param name="slots">Seznam slotů k naplění</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        State Solve(IEnumerable<Slot> slots);

        /// <summary>
        /// Vytvoří stav řešení zapomocí již existujícího stavu
        /// </summary>
        /// <param name="state">Stav pro vylepšení</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        State Solve(State state);

    }
}
