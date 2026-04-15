using Diplomka.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class Comparer
    {
        public static bool IsTimeCollision(Slot slotA, Slot slotB)
        {
            return slotA.Match.Start < slotB.Match.End && slotB.Match.Start < slotA.Match.End;
        }
    }
}
