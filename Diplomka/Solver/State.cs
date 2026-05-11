using Diplomka.Entity;
using Diplomka.Routing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    // TODO: Sjednoti kolekce, nekde je IEnumerable a nekde List
    /// <summary>
    /// Stav řešení uchovávájící přiřezení rozhodčích ke slotům.
    /// Jsou zde uchovávány i dalčí pomocné struktury pro rychlejší přístupy.
    /// </summary>
    public class State : IEnumerable<KeyValuePair<Slot, Referee?>>, ICloneable
    {
        /// <summary>
        /// Hlavní slovníková struktura pro přiřazení
        /// </summary>
        private Dictionary<Slot, Referee?> _assignments = new();

        /// <summary>
        /// Pomocná struktura pro uchovávání slotů, která jsou zaplněny daným rozhodčím
        /// </summary>
        private Dictionary<Referee, List<Slot>> _refereeToSlots = new();

        /// <summary>
        /// Pomocná struktura pro uchovávání prázdných slotů
        /// </summary>
        private HashSet<Slot> _emptySlots = new();

        /// <summary>
        /// Získá rozhodčího přiřazeného ke slotu
        /// </summary>
        /// <param name="slot">Slot jako klíč k hledání</param>
        /// <returns>Rozhodčí, když je nějaký ke slotu přiřazen, jinak null</returns>
        public Referee? GetReferee(Slot slot)
        {
            return _assignments[slot];
        }

        /// <summary>
        /// Získání seznamu všech přiřazených rozhodčích ze stavu
        /// </summary>
        /// <returns>Seznam rozhodčích</returns>
        public List<Referee?> GetReferees()
        {
            return _assignments.Values.ToList();
        }

        /// <summary>
        /// Získání všech slotů ze stavu (včetně nezaplněných)
        /// </summary>
        /// <returns>Seznam slotů</returns>
        public List<Slot> GetSlots()
        {
            return _assignments.Keys.ToList();
        }

        /// <summary>
        /// Získání nezaplněných slotů
        /// </summary>
        /// <returns>Seznam slotů</returns>
        public IEnumerable<Slot> GetEmptySlots()
        {
            return _emptySlots;
        }

        /// <summary>
        /// Získání slotů, které jsou zaplněny daným rozhodčím
        /// </summary>
        /// <param name="referee">Rozhodčí k prohledávání</param>
        /// <returns>Seznam slotů</returns>
        public List<Slot> GetSlotsByReferee(Referee referee)
        {
            if (_refereeToSlots.TryGetValue(referee, out var slots))
            {
                return slots;
            }
            return new List<Slot>();
        }

        /// <summary>
        /// Přidání nezaplněného slotu do stavu
        /// </summary>
        /// <param name="slot">Slot k přidání</param>
        public void AddSlot(Slot slot)
        {
            _assignments[slot] = null;
            _emptySlots.Add(slot);
        }

        /// <summary>
        /// Odebrání a vyprázndění slotu ze stavu
        /// </summary>
        /// <param name="slot">Slot k odebrání</param>
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

        /// <summary>
        /// Vyprázdnění slotu ve stavu (odebrání přiřazeného rozhodčího)
        /// </summary>
        /// <param name="slot">Slot k vyprázdnění</param>
        public void ClearSlot(Slot slot)
        {
            if (_assignments.TryGetValue(slot, out var existingReferee) && existingReferee != null)
            {
                RemoveFromIndex(existingReferee, slot);
                _assignments[slot] = null;
                _emptySlots.Add(slot);
            }
        }

        /// <summary>
        /// Přiřazení rozhodčího ke slotu
        /// </summary>
        /// <param name="slot">Slot k zaplnění</param>
        /// <param name="referee">Rozhodčí k přiřazení</param>
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

        /// <summary>
        /// Pomocná metoda pro odebrání slotu z pomocné datové struktury uchovávající sloty zaplněné rozhodčím
        /// </summary>
        /// <param name="referee">Rozhodčí k prohledávání</param>
        /// <param name="slot">Slot k odebrání</param>
        private void RemoveFromIndex(Referee referee, Slot slot)
        {
            if (_refereeToSlots.TryGetValue(referee, out var list))
            {
                list.Remove(slot);
            }
        }

        // Vytvoření enumerátoru pro lepší procházení stavu vně třídy
        public IEnumerator<KeyValuePair<Slot, Referee?>> GetEnumerator()
        {
            return _assignments.GetEnumerator();
        }

        // Získání vytvořeného emurátoru
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // TODO: Zastarale, upravit
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
