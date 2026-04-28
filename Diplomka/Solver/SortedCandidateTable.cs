using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class SortedCandidateTable
    {
        private Dictionary<Slot, List<(Referee Referee, double Cost)>> _sortedCandidates = new();

        private CostCalculator _costCalculator;
        private ConflictChecker _conflictChecker;

        public SortedCandidateTable(CostCalculator costCalculator, ConflictChecker conflictChecker)
        {
            _costCalculator = costCalculator;
            _conflictChecker = conflictChecker;
        }

        public List<(Referee Referee, double Cost)> GetCandidatesWithCosts(Slot slot)
        {
            _sortedCandidates.TryGetValue(slot, out var candidates);

            if (candidates == null)
                return new List<(Referee Referee, double Cost)>();

            return candidates;
        }

        /*
         * Ziska kadnidata ze seznamu
         * Vychozi je prvni kandidat - s nejlepsi cenou
         */
        public Referee? GetCandidate(Slot slot, int index = 0)
        {
            _sortedCandidates.TryGetValue(slot, out var candidates);

            if (candidates == null || index >= candidates.Count)
                return null;

            var candidate = candidates[index].Referee;

            return candidate;
        }

        /*
         * Najde nejlepsiho kandidate
         * Postupne prohledava nevhodnejsi kandidaty serazene podle ceny
         * Prvni kadnidat, ktery muze byt prirazen (neexistuje kolize) je vybran
         */
        public Referee? GetBestCandidate(State state, Slot slot)
        {
            _sortedCandidates.TryGetValue(slot, out var candidates);

            if (candidates == null)
                return null;

            foreach (var c in candidates)
                if (_conflictChecker.CanAssign(state, slot, c.Referee))
                    return c.Referee;

            return null;
        }

        public void Initialize(List<Slot> slots, List<Referee> referees, int maxCandidatesPerSlot = 30)
        {
            _sortedCandidates.Clear();
            _sortedCandidates = CalculateCosts(slots, referees, maxCandidatesPerSlot); 
        }

        private Dictionary<Slot, List<(Referee Referee, double Cost)>> CalculateCosts(
            List<Slot> slots, 
            List<Referee> referees, 
            int maxCandidatesPerSlot = 30)
        {
            // Inicializace tvé kalkulačky
            var costMatrix = new Dictionary<Slot, List<(Referee Referee, double Cost)>>();

            foreach (var slot in slots)
            {
                var candidatesForSlot = referees
                    // 1. Zachováváme tvrdý filtr na Rank (pokud na to nemá rozhodčí papíry, ani nepočítáme cenu)
                    .Where(r => r.Rank >= slot.RequiredRank)
                    .Select(referee =>
                    {
                        // 2. Použití tvé metody AssignmentCost
                        double cost = _costCalculator.AssignmentCost(slot, referee);
                        return (Referee: referee, Cost: cost);
                    })
                    // 3. Seřazení od nejlevnějšího
                    .OrderBy(c => c.Cost)
                    // 4. Omezení na N nejlepších (aby se algoritmus neutopil v možnostech)
                    .Take(maxCandidatesPerSlot)
                    .ToList();

                costMatrix.Add(slot, candidatesForSlot);
            }

            return costMatrix;
        }
    }
}
