using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diplomka.Model;

namespace Diplomka.Solver
{
    public class BBSolver
    {
        private State? _bestState;
        private int _bestCost;

        // --- Předpočítané struktury pro maximální rychlost ---
        private int _numSlots;
        private int _numReferees;
        private int[,] _costMatrix = null!;
        private bool[,] _conflictMatrix = null!;
        private List<int>[] _refereeAssignments = null!;

        // --- Proměnné pro limitování času a šířky prohledávání ---
        private Stopwatch _timer = new Stopwatch();
        private long _timeLimitMs;
        private bool _timeOutReached;
        private int _maxBranchingFactor;

        // ---------------------------------------------------------
        // 1. INICIALIZACE A PŘEDVÝPOČTY
        // ---------------------------------------------------------
        private void InitializeData(List<Slot> allSlots, List<Referee> referees)
        {
            _numSlots = allSlots.Count;
            _numReferees = referees.Count;

            _costMatrix = new int[_numSlots, _numReferees];
            _conflictMatrix = new bool[_numSlots, _numSlots];

            _refereeAssignments = new List<int>[_numReferees];
            for (int i = 0; i < _numReferees; i++)
            {
                _refereeAssignments[i] = new List<int>(_numSlots);
            }

            // Předvýpočet cen a časových konfliktů
            for (int i = 0; i < _numSlots; i++)
            {
                for (int j = 0; j < _numReferees; j++)
                {
                    _costMatrix[i, j] = AssignmentCost(allSlots[i], referees[j]);
                }

                for (int k = 0; k < _numSlots; k++)
                {
                    // True, pokud se sloty časově překrývají (kolize)
                    _conflictMatrix[i, k] = (allSlots[i].Start < allSlots[k].End && allSlots[k].Start < allSlots[i].End);
                }
            }
        }

        public int AssignmentCost(Slot slot, Referee referee)
        {
            int levelDifference = Math.Abs(slot.RequiredRank - referee.Rank);
            double distance = referee.Location.DistanceTo(slot.Location);
            return (int)Math.Round(levelDifference * 100.0 + distance * 2.0);
        }

        // ---------------------------------------------------------
        // 2. HLAVNÍ METODA SOLVE
        // ---------------------------------------------------------
        public State Solve(State initialState, List<Referee> referees, int maxSeconds = 60, int maxBranchingFactor = 4)
        {
            var allSlots = initialState.GetSlots().ToList();
            if (allSlots.Count == 0) return initialState;

            InitializeData(allSlots, referees);

            _timeLimitMs = maxSeconds * 1000;
            _timeOutReached = false;
            _maxBranchingFactor = maxBranchingFactor;

            _bestCost = int.MaxValue;
            _bestState = null;

            // Získáme úvodní horní mez pomocí Greedy algoritmu
            var greedy = GreedySolve(initialState, referees, allSlots);
            if (greedy != null)
            {
                _bestState = greedy;
                _bestCost = CalculateTotalCost(greedy);
            }

            // Pole pro sledování aktuálního přiřazení (index rozhodčího nebo -1)
            var currentAssignments = new int[_numSlots];
            Array.Fill(currentAssignments, -1);

            // Pole indexů slotů, které budeme in-place přehazovat (Swap)
            var slotIndices = Enumerable.Range(0, _numSlots).ToArray();

            // Spuštění stopek a samotného B&B algoritmu
            _timer.Restart();
            DFS(slotIndices, 0, 0, currentAssignments, referees, allSlots);
            _timer.Stop();

            if (_timeOutReached)
            {
                Console.WriteLine($"[INFO] Algoritmus přerušen kvůli časovému limitu ({maxSeconds}s). Vracím nejlepší nalezené řešení.");
            }

            return _bestState ?? initialState;
        }

        // ---------------------------------------------------------
        // 3. GREEDY ALGORITMUS
        // ---------------------------------------------------------
        private State? GreedySolve(State initialState, List<Referee> referees, List<Slot> allSlots)
        {
            var state = (State)initialState.Clone();

            var greedyAssignments = new List<int>[_numReferees];
            for (int i = 0; i < _numReferees; i++)
                greedyAssignments[i] = new List<int>();

            var sortedSlotIndices = Enumerable.Range(0, _numSlots)
                                              .OrderBy(i => allSlots[i].Start)
                                              .ToList();

            foreach (int sIdx in sortedSlotIndices)
            {
                int bestR = -1;
                int bestCost = int.MaxValue;

                for (int r = 0; r < _numReferees; r++)
                {
                    bool isFeasible = true;
                    foreach (int assignedSIdx in greedyAssignments[r])
                    {
                        if (_conflictMatrix[sIdx, assignedSIdx])
                        {
                            isFeasible = false;
                            break;
                        }
                    }

                    if (!isFeasible) continue;

                    int cost = _costMatrix[sIdx, r];
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestR = r;
                    }
                }

                if (bestR == -1) return null; // Greedy selhal, nevadí, B&B to zkusí napravit

                greedyAssignments[bestR].Add(sIdx);
                state.SetReferee(allSlots[sIdx], referees[bestR]);
            }

            return state;
        }

        // ---------------------------------------------------------
        // 4. BRANCH & BOUND - DFS (Backtracking in-place)
        // ---------------------------------------------------------
        private void DFS(int[] slotIndices, int currentIdx, int currentCost, int[] assignments, List<Referee> referees, List<Slot> allSlots)
        {
            // OCHRANA PROTI ZAMRZNUTÍ: Kontrola času
            if (_timeOutReached) return;
            // Abychom nebrzdili výkon, kontrolujeme čas jen občas, ale pro jistotu v každém uzlu:
            if (_timer.ElapsedMilliseconds > _timeLimitMs)
            {
                _timeOutReached = true;
                return;
            }

            // Pruning: Pokud je aktuální cena horší nebo stejná jako nejlepší nalezená
            if (currentCost >= _bestCost) return;

            // Bounding: Vypočítáme teoretické minimum pro zbytek slotů
            int bound = LowerBound(slotIndices, currentIdx, currentCost);
            if (bound >= _bestCost) return;

            // Base case: Všechny sloty byly úspěšně přiřazeny
            if (currentIdx == _numSlots)
            {
                _bestCost = currentCost;
                _bestState = BuildState(allSlots, referees, assignments);
                return;
            }

            // MRV (Minimum Remaining Values): Vybereme nejpřísněji omezený slot
            int bestSlotPos = SelectNextSlot(slotIndices, currentIdx);

            // Přehodíme vybraný slot na aktuální pozici v poli
            Swap(slotIndices, currentIdx, bestSlotPos);
            int sIdx = slotIndices[currentIdx];

            // Získáme kandidáty seřazené podle ceny
            var candidates = GetSortedCandidates(sIdx);
            int branchesExplored = 0;

            foreach (var cand in candidates)
            {
                // OMEZENÍ VĚTVENÍ (Beam Search): Zkusíme jen TOP X nejlepších
                if (branchesExplored >= _maxBranchingFactor) break;

                if (IsFeasible(cand.RefIdx, sIdx))
                {
                    branchesExplored++;

                    // Assign (In-place)
                    assignments[sIdx] = cand.RefIdx;
                    _refereeAssignments[cand.RefIdx].Add(sIdx);

                    // Rekurze
                    DFS(slotIndices, currentIdx + 1, currentCost + cand.Cost, assignments, referees, allSlots);

                    // Unassign (Backtrack)
                    _refereeAssignments[cand.RefIdx].RemoveAt(_refereeAssignments[cand.RefIdx].Count - 1);
                    assignments[sIdx] = -1;
                }
            }
        }

        // ---------------------------------------------------------
        // 5. POMOCNÉ FUNKCE PRO B&B
        // ---------------------------------------------------------
        private bool IsFeasible(int refIdx, int slotIdx)
        {
            var assigned = _refereeAssignments[refIdx];
            for (int i = 0; i < assigned.Count; i++)
            {
                if (_conflictMatrix[slotIdx, assigned[i]])
                    return false;
            }
            return true;
        }

        private int LowerBound(int[] slotIndices, int startIdx, int currentCost)
        {
            int bound = currentCost;
            for (int i = startIdx; i < _numSlots; i++)
            {
                int sIdx = slotIndices[i];
                int minCostForSlot = int.MaxValue;

                for (int r = 0; r < _numReferees; r++)
                {
                    if (_costMatrix[sIdx, r] < minCostForSlot)
                    {
                        minCostForSlot = _costMatrix[sIdx, r];
                    }
                }
                bound += minCostForSlot;
                if (bound >= _bestCost) return bound;
            }
            return bound;
        }

        private int SelectNextSlot(int[] slotIndices, int startIdx)
        {
            int bestPos = startIdx;
            int minFeasibleCount = int.MaxValue;

            for (int i = startIdx; i < _numSlots; i++)
            {
                int sIdx = slotIndices[i];
                int feasibleCount = 0;

                for (int r = 0; r < _numReferees; r++)
                {
                    if (IsFeasible(r, sIdx)) feasibleCount++;
                }

                if (feasibleCount < minFeasibleCount)
                {
                    minFeasibleCount = feasibleCount;
                    bestPos = i;

                    if (feasibleCount <= 1) break;
                }
            }
            return bestPos;
        }

        private List<(int Cost, int RefIdx)> GetSortedCandidates(int slotIdx)
        {
            var list = new List<(int Cost, int RefIdx)>(_numReferees);
            for (int r = 0; r < _numReferees; r++)
            {
                list.Add((_costMatrix[slotIdx, r], r));
            }

            list.Sort((a, b) => a.Cost.CompareTo(b.Cost));
            return list;
        }

        private void Swap(int[] arr, int i, int j)
        {
            if (i == j) return;
            int temp = arr[i];
            arr[i] = arr[j];
            arr[j] = temp;
        }

        // ---------------------------------------------------------
        // 6. FINÁLNÍ SESTAVENÍ OBJEKTŮ
        // ---------------------------------------------------------
        private State BuildState(List<Slot> allSlots, List<Referee> referees, int[] assignments)
        {
            var state = new State();
            for (int i = 0; i < _numSlots; i++)
            {
                if (assignments[i] != -1)
                {
                    state.SetReferee(allSlots[i], referees[assignments[i]]);
                }
            }
            return state;
        }

        private int CalculateTotalCost(State state)
        {
            int total = 0;
            foreach (var (slot, refObj) in state)
            {
                if (refObj != null)
                    total += AssignmentCost(slot, refObj);
            }
            return total;
        }

        public int StateCost(State state)
        {
            return CalculateTotalCost(state);
        }
    }
}