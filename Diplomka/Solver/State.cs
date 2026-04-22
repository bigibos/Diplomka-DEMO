using Diplomka.Entity;
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
        private Dictionary<Slot, Referee?> _assignments = new();
        private Dictionary<Referee, List<Slot>> _refereeToSlots = new();
        private HashSet<Slot> _emptySlots = new();


        public List<Referee?> GetReferees()
        {
            return _assignments.Values.ToList();
        }

        public List<Slot> GetSlots()
        {
            return _assignments.Keys.ToList();
        }


        public IEnumerable<Slot> GetEmptySlots()
        {
            return _emptySlots;
        }

        public List<Slot> GetSlotsByReferee(Referee referee)
        {
            if (_refereeToSlots.TryGetValue(referee, out var slots))
            {
                return slots;
            }
            return new List<Slot>();
        }

        public void AddSlot(Slot slot)
        {
            _assignments[slot] = null;
            _emptySlots.Add(slot);
        }

        public void RemoveSlot(Slot slot)
        {
            if (_assignments.TryGetValue(slot, out var existingReferee))
            {
                if (existingReferee != null)
                    RemoveFromIndex(existingReferee, slot);
                else
                    _emptySlots.Remove(slot);
                
                _assignments.Remove(slot);
            }
        }

        public void ClearSlot(Slot slot)
        {
            if (_assignments.TryGetValue(slot, out var existingReferee) && existingReferee != null)
            {
                RemoveFromIndex(existingReferee, slot);
                _assignments[slot] = null;
                _emptySlots.Add(slot);
            }
        }

        public void SetReferee(Slot slot, Referee? referee)
        {
            if (!_assignments.TryGetValue(slot, out var oldReferee))
                return;

            if (oldReferee != null)
                RemoveFromIndex(oldReferee, slot);   

            _assignments[slot] = referee;

            if (referee != null)
            {
                _emptySlots.Remove(slot);
                if (!_refereeToSlots.ContainsKey(referee))
                    _refereeToSlots[referee] = new List<Slot>();
                
                _refereeToSlots[referee].Add(slot);
            }
            else
            {
                _emptySlots.Add(slot);
            }
        }

        private void RemoveFromIndex(Referee referee, Slot slot)
        {
            if (_refereeToSlots.TryGetValue(referee, out var list))
            {
                list.Remove(slot);
            }
        }

        public IEnumerator<KeyValuePair<Slot, Referee?>> GetEnumerator()
        {
            return _assignments.GetEnumerator();
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
            foreach (var assignment in _assignments)
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

            foreach (var assignment in _assignments)
            {
                cloned._assignments[assignment.Key] = assignment.Value;
            }


            foreach (var entry in _refereeToSlots)
            {
                cloned._refereeToSlots[entry.Key] = new List<Slot>(entry.Value);
            }

            cloned._emptySlots = new HashSet<Slot>(this._emptySlots);

            return cloned;
        }
    }
}
