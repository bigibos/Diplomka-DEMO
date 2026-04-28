using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Diplomka.Solver
{
    public class SortedCandidateTable
    {
        // Pomocná statická kolekce pro úsporu paměti
        private static readonly List<(Referee Referee, double Cost)> _emptyList = new(0);

        private Dictionary<Slot, List<(Referee Referee, double Cost)>> _sortedCandidates = new();

        private readonly CostCalculator _costCalculator;
        private readonly ConflictChecker _conflictChecker;

        public SortedCandidateTable(CostCalculator costCalculator, ConflictChecker conflictChecker)
        {
            _costCalculator = costCalculator;
            _conflictChecker = conflictChecker;
        }

        /// <summary>
        /// Vrátí předpočítaný seznam kandidátů pro daný slot bez ohledu na aktuální stav (kolize).
        /// </summary>
        public List<(Referee Referee, double Cost)> GetCandidatesWithCosts(Slot slot)
        {
            return _sortedCandidates.TryGetValue(slot, out var candidates) ? candidates : _emptyList;
        }

        /// <summary>
        /// Vrátí pouze ty kandidáty, kteří mohou být v daném stavu přiřazeni (využívá tvůj ConflictChecker).
        /// </summary>
        public List<(Referee Referee, double Cost)> GetBestCandidatesWithCosts(State state, Slot slot)
        {
            var allCandidates = GetCandidatesWithCosts(slot);
            if (allCandidates.Count == 0) return _emptyList;

            // Použijeme tvůj ConflictChecker.CanAssign, který kontroluje jak rank, tak časové překryvy
            return allCandidates
                .Where(c => _conflictChecker.CanAssign(state, slot, c.Referee))
                .ToList();
        }

        /// <summary>
        /// MRV Heuristika: Najde nejtěžší slot z prázdných slotů a vrátí ho i s jeho volnými kandidáty.
        /// </summary>
        public (Slot? Slot, List<(Referee Referee, double Cost)> Candidates) GetHardestSelection(State state)
        {
            var emptySlots = state.GetEmptySlots();
            Slot? bestSlot = null;
            List<(Referee, double)>? bestCandidates = null;
            int minCount = int.MaxValue;

            foreach (var slot in emptySlots)
            {
                // Získáme kandidáty, kteří v tomto uzlu stromu nemají kolizi
                var availableCandidates = GetBestCandidatesWithCosts(state, slot);
                int count = availableCandidates.Count;

                // MRV: Slot s nejmenším počtem možností je prioritní
                if (count < minCount || (count == minCount && slot.RequiredRank > (bestSlot?.RequiredRank ?? 0)))
                {
                    minCount = count;
                    bestSlot = slot;
                    bestCandidates = availableCandidates;
                }

                // Pokud narazíme na slot, který nejde obsadit, okamžitě končíme (Pruning)
                if (count == 0) break;
            }

            return (bestSlot, bestCandidates ?? _emptyList);
        }

        /// <summary>
        /// Vybere prvního (nejlevnějšího) kandidáta, který splňuje podmínky ConflictCheckeru.
        /// </summary>
        public Referee? GetBestCandidate(State state, Slot slot)
        {
            var candidates = GetCandidatesWithCosts(slot);
            foreach (var c in candidates)
            {
                if (_conflictChecker.CanAssign(state, slot, c.Referee))
                    return c.Referee;
            }
            return null;
        }

        /// <summary>
        /// Inicializuje tabulku. Pro každý slot předvypočítá ceny pro všechny způsobilé rozhodčí.
        /// </summary>
        public void Initialize(List<Slot> slots, List<Referee> referees, int maxCandidatesPerSlot = 30)
        {
            _sortedCandidates.Clear();
            var costMatrix = new Dictionary<Slot, List<(Referee Referee, double Cost)>>();

            foreach (var slot in slots)
            {
                var candidatesForSlot = referees
                    // Filtrujeme pouze ty, kteří pro daný slot vůbec přichází v úvahu z pohledu ranku.
                    // (Pozn: CanAssign v ConflictCheckeru kontroluje rank znovu, což je v pořádku)
                    .Where(r => IsRankEligible(r, slot))
                    .Select(referee => (Referee: referee, Cost: _costCalculator.AssignmentCost(slot, referee)))
                    .OrderBy(c => c.Cost)
                    .Take(maxCandidatesPerSlot)
                    .ToList();

                costMatrix.Add(slot, candidatesForSlot);
            }

            _sortedCandidates = costMatrix;
        }

        /// <summary>
        /// Pomocná metoda pro základní filtraci při inicializaci. 
        /// Musí odpovídat logice v ConflictChecker.CanAssign.
        /// </summary>
        private bool IsRankEligible(Referee referee, Slot slot)
        {
            // Tady by měla být stejná logika jako ve tvém ConflictCheckeru (Rank + Margin)
            // Pokud nemáš přístup ke konfiguraci přímo zde, CanAssign to pak stejně dofiltruje.
            // Pro začátek stačí i tento základní filtr:
            return referee.Rank >= (slot.RequiredRank - 20); // Rezerva pro marži
        }
    }
}