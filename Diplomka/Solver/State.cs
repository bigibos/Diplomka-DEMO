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
        private Dictionary<Referee, List<Slot>> refereeToSlots = new();

        public List<Referee?> GetReferees()
        {
            return assignments.Values.ToList();
        }

        public List<Slot> GetSlots()
        {
            return assignments.Keys.ToList();
        }

        public List<Slot> GetEmptySlots()
        {
            return assignments
                .Where(p => p.Value == null)
                .Select(p => p.Key)
                .ToList();
        }

        public List<Slot> GetSlotsByReferee(Referee referee)
        {
            if (refereeToSlots.TryGetValue(referee, out var slots))
            {
                return slots;
            }
            return new List<Slot>();
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
            if (assignments.TryGetValue(slot, out var existingReferee) && existingReferee != null)
            {
                RemoveFromIndex(existingReferee, slot);
                assignments[slot] = null;
            }
        }

        public void SetReferee(Slot slot, Referee? referee)
        {
            if (!assignments.ContainsKey(slot))
                return;

            var oldReferee = assignments[slot];
            if (oldReferee != null)
                RemoveFromIndex(oldReferee, slot);   

            assignments[slot] = referee;

            if (referee != null)
            {
                if (!refereeToSlots.ContainsKey(referee))
                {
                    refereeToSlots[referee] = new List<Slot>();
                }
                refereeToSlots[referee].Add(slot);
            }
        }

        private void RemoveFromIndex(Referee referee, Slot slot)
        {
            if (refereeToSlots.TryGetValue(referee, out var list))
            {
                list.Remove(slot);
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
            var sb = new StringBuilder();
            sb.AppendLine("Stav:");
            sb.AppendLine("---------------------");
            foreach (var assignment in assignments)
            {
                sb.Append($"{assignment.Key}\t-> ");
                if (assignment.Value != null)
                {
                    var dist = Math.Round(assignment.Value.Location.DistanceTo(assignment.Key.Location), 2);
                    sb.AppendLine($"{assignment.Value}\t(Den {assignment.Key.Start.Day})({assignment.Key.RequiredRank}/{assignment.Value.Rank})({dist} km)");
                }
                else
                {
                    sb.AppendLine("No Referee Assigned");
                }
            }
            return sb.ToString();
        }

        public object Clone()
        {
            var cloned = new State();

            foreach (var assignment in assignments)
            {
                cloned.assignments[assignment.Key] = assignment.Value;
            }


            foreach (var entry in refereeToSlots)
            {
                cloned.refereeToSlots[entry.Key] = new List<Slot>(entry.Value);
            }

            return cloned;
        }
    }
}
