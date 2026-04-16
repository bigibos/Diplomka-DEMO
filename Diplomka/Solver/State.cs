using Diplomka.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class State : IEnumerable<KeyValuePair<Slot, Referee?>>, ICloneable
    {
        private Dictionary<Slot, Referee?> assignments = new();


        public List<Slot> GetSlots()
        {
            return assignments.Keys.ToList();
        }

        public List<Referee?> GetReferees()
        {
            return assignments.Values.ToList();
        }

        public List<Slot> GetEmptySlots()
        {
            return assignments
                .Where(p => p.Value == null)
                .Select(p => p.Key)
                .ToList();
        }

        public void AddSlot(Slot slot)
        {
            assignments[slot] = null;
        }

        public void RemoveSlot(Slot slot)
        {
            if (assignments.ContainsKey(slot))
            {
                assignments.Remove(slot);
            }
        }

        public void ClearSlot(Slot slot)
        {
            if (assignments.ContainsKey(slot))
            {
                assignments[slot] = null;
            }
        }

        public void SetReferee(Slot slot, Referee? referee)
        {
            if (assignments.ContainsKey(slot))
            {
                assignments[slot] = referee;
            }
        }

        public IEnumerator<KeyValuePair<Slot, Referee?>> GetEnumerator()
        {
            return assignments.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            string result = "";

            result += "Stav:\n";
            result += "---------------------\n";
            foreach (var assignment in assignments)
            {
                result += $"{assignment.Key}\t-> ";
                if (assignment.Value != null)
                {
                    result += $"{assignment.Value}\t(Den {assignment.Key.Match.Start.Day})({assignment.Key.RequiredRank}/{assignment.Value.Rank})({Math.Round(assignment.Value.Location.DistanceTo(assignment.Key.Match.Location), 2)} km)\n";
                }
                else
                {
                    result += "No Referee Assigned\n";
                }
            }

            return result;
        }

        public object Clone()
        {
            var cloned = new State();

            foreach (var assignment in assignments)
            {
                cloned.assignments[assignment.Key] = assignment.Value;
            }

            return cloned;
        }
    }
}
