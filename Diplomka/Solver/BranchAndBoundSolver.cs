using Diplomka.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Diplomka.Solver
{
    /// <summary>
    /// Branch & Bound solver pro přiřazování rozhodčích do slotů.
    ///
    /// Cena přiřazení (referee R → slot S):
    ///   cost(R, S) = DistanceTo(R.Location, S.Location)
    ///              + |R.Rank - S.RequiredRank| * RankPenaltyWeight
    ///
    /// Striktní omezení:
    ///   1. Každý slot musí mít přiřazeného rozhodčího.
    ///   2. Rozhodčí nesmí být přiřazen do dvou časově se překrývajících slotů.
    /// </summary>
    public class BranchAndBoundSolver
    {
        // Váha penalizace za rozdíl ranku. Upravte dle potřeby.
        public double RankPenaltyWeight { get; set; } = 10.0;

        private List<Referee> _referees = new();
        private List<Slot> _slots = new();

        // Nejlepší nalezené řešení
        private State? _bestState = null;
        private double _bestCost = double.MaxValue;

        // Počet navštívených uzlů (pro diagnostiku)
        public int NodesVisited { get; private set; } = 0;

        public State? Solve(State initialState, List<Referee> referees)
        {
            _referees = referees;
            _slots = initialState.GetSlots();
            _bestState = null;
            _bestCost = double.MaxValue;
            NodesVisited = 0;

            // Seřaď sloty podle počtu dostupných rozhodčích (fail-first heuristika).
            // Sloty s méně kandidáty se větví dříve → rychlejší prořezání.
            _slots = _slots
                .OrderBy(s => GetEligibleReferees(s, new List<(Slot, Referee)>()).Count)
                .ToList();

            // Spusť DFS rekurzi
            BranchAndBound(initialState, new List<(Slot slot, Referee referee)>(), 0.0, 0);

            return _bestState;
        }

        // ----------------------------------------------------------------
        // Rekurzivní B&B (DFS)
        // ----------------------------------------------------------------
        private void BranchAndBound(
            State state,
            List<(Slot slot, Referee referee)> assignments,
            double currentCost,
            int depth)
        {
            NodesVisited++;

            // --- Prořezání: aktuální cena už překračuje nejlepší ---
            if (currentCost >= _bestCost)
                return;

            // --- Výběr dalšího neohodnoceného slotu ---
            var emptySlots = state.GetEmptySlots();

            if (emptySlots.Count == 0)
            {
                // Všechny sloty přiřazeny → kompletní řešení
                if (currentCost < _bestCost)
                {
                    _bestCost = currentCost;
                    _bestState = (State)state.Clone();
                }
                return;
            }

            // Vyber první prázdný slot (pořadí určeno v Solve)
            Slot slot = emptySlots[0];

            // Získej kandidáty respektující časové omezení
            var candidates = GetEligibleReferees(slot, assignments);

            if (candidates.Count == 0)
            {
                // Slepá větev — žádný dostupný rozhodčí
                return;
            }

            // Seřaď kandidáty podle dolního odhadu přírůstkové ceny (best-first uvnitř DFS)
            candidates = candidates
                .OrderBy(r => AssignmentCost(r, slot))
                .ToList();

            foreach (var referee in candidates)
            {
                double incrementalCost = AssignmentCost(referee, slot);
                double newCost = currentCost + incrementalCost;

                // Prořezání s lower boundem
                double lb = newCost + LowerBound(state, slot, assignments, referee);
                if (lb >= _bestCost)
                    continue;

                // Vytvoř větev
                state.SetReferee(slot, referee);
                assignments.Add((slot, referee));

                BranchAndBound(state, assignments, newCost, depth + 1);

                // Zpět (backtrack)
                state.ClearSlot(slot);
                assignments.RemoveAt(assignments.Count - 1);
            }
        }

        // ----------------------------------------------------------------
        // Lower bound pro zbývající prázdné sloty
        //
        // Pro každý dosud nepřiřazený slot (kromě právě větvěného) odhadneme
        // minimální možnou cenu: nejlevnější dostupný rozhodčí.
        // Tím dostaneme optimistický (dolní) odhad zbytku stromu.
        // ----------------------------------------------------------------
        private double LowerBound(
            State state,
            Slot justAssigned,
            List<(Slot slot, Referee referee)> assignments,
            Referee justReferee)
        {
            double lb = 0.0;

            // Simuluj přiřazení, které právě děláme
            var tempAssignments = new List<(Slot, Referee)>(assignments) { (justAssigned, justReferee) };

            foreach (var emptySlot in state.GetEmptySlots())
            {
                if (emptySlot == justAssigned)
                    continue;

                var eligible = GetEligibleReferees(emptySlot, tempAssignments);
                if (eligible.Count == 0)
                    return double.MaxValue; // Infeasible → nekonečno

                double minCost = eligible.Min(r => AssignmentCost(r, emptySlot));
                lb += minCost;
            }

            return lb;
        }

        // ----------------------------------------------------------------
        // Cena jednoho přiřazení
        // ----------------------------------------------------------------
        public double AssignmentCost(Referee referee, Slot slot)
        {
            double distance = referee.Location.DistanceTo(slot.Location!);
            double rankPenalty = Math.Abs(referee.Rank - slot.RequiredRank) * RankPenaltyWeight;
            return distance + rankPenalty;
        }

        // ----------------------------------------------------------------
        // Vrátí rozhodčí, kteří:
        //   1. Splňují rank (>= RequiredRank) — volitelné, uprav dle pravidel
        //   2. Nemají časový konflikt s dosavadními přiřazeními
        // ----------------------------------------------------------------
        private List<Referee> GetEligibleReferees(
            Slot slot,
            List<(Slot slot, Referee referee)> currentAssignments)
        {
            return _referees
                .Where(r => r.Rank >= slot.RequiredRank)
                .Where(r => !HasTimeConflict(r, slot, currentAssignments))
                .ToList();
        }

        // ----------------------------------------------------------------
        // Detekce časového konfliktu
        //
        // Dva sloty se překrývají, když:
        //   slotA.Start < slotB.End  &&  slotA.End > slotB.Start
        // ----------------------------------------------------------------
        private bool HasTimeConflict(
            Referee referee,
            Slot newSlot,
            List<(Slot slot, Referee referee)> currentAssignments)
        {
            foreach (var (assignedSlot, assignedReferee) in currentAssignments)
            {
                if (assignedReferee.Id != referee.Id)
                    continue;

                bool overlaps = assignedSlot.Start < newSlot.End &&
                                assignedSlot.End > newSlot.Start;

                if (overlaps)
                    return true;
            }

            return false;
        }

        // ----------------------------------------------------------------
        // Výpočet celkové ceny hotového State (pro ověření / HC kompatibilita)
        // ----------------------------------------------------------------
        public double CalculateTotalCost(State state)
        {
            double total = 0.0;

            foreach (var (slot, referee) in state)
            {
                if (referee == null)
                    throw new InvalidOperationException($"Slot {slot.Id} nemá přiřazeného rozhodčího.");

                total += AssignmentCost(referee, slot);
            }

            return total;
        }
    }
}
