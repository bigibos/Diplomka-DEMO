using Diplomka.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class BBSolver
    {
        public bool IsTimeCollision(Slot slotA, Slot slotB)
        {
            return slotA.Start < slotB.End && slotB.Start < slotA.End;
        }
        /**
         * 
         * Striknti podminky pro přiřazení rozhodčího do slotu
         * Neresi zadnou cenu, jen jestli muzou byt prirazen
         * 
         */
        private bool CanAssign(State state, Slot slot, Referee referee)
        {
            foreach (var pair in state)
            {
                if (pair.Value == null)
                    continue;

                if (pair.Value.Id == referee.Id && IsTimeCollision(pair.Key, slot))
                {
                    return false; // kolize dne
                }
            }

            return true;
        }

        /**
         * 
         * Cena přiřazení rozhodčího do slotu
         * 
         */
        public int AssignmentCost(Slot slot, Referee referee)
        {
            // 1. Rozdíl úrovní (např. 0, 1, 2...)
            int levelDifference = Math.Abs(slot.RequiredRank - referee.Rank);

            // 2. Vzdálenost v km (např. 15.5 km)
            double distance = referee.Location.DistanceTo(slot.Location);

            // 3. Vážená suma
            // Příklad: 1 stupeň úrovně navíc je "stejně drahý" jako 50 km cesty.
            // Tuhle váhu si můžeš upravit podle potřeby.
            double levelWeight = 100.0;
            double distanceWeight = 2.0;

            double totalCost = (levelDifference * levelWeight) + (distance * distanceWeight);

            return (int)Math.Round(totalCost);
        }


        /**
         * 
         * Celkova cena stavu
         * 
         */
        public int StateCost(State state)
        {
            int cost = 0;

            foreach (var pair in state)
            {
                if (pair.Value != null)
                {
                    cost += AssignmentCost(pair.Key, pair.Value);
                }
            }

            return cost;
        }



        private int LowerBound(State state, List<Referee> referees)
        {
            int bound = StateCost(state);

            foreach (var slot in state.GetEmptySlots())
            {
                int min = int.MaxValue;

                foreach (var referee in referees)
                {
                    min = Math.Min(min, AssignmentCost(slot, referee));
                }

                bound += min;
            }

            return bound;
        }

        private State? bestState = null;
        private int bestCost = int.MaxValue;

        public State Solve(State state, List<Referee> referees)
        {
            int currentCost = StateCost(state);

            // všechny sloty přiřazené
            if (state.GetEmptySlots().Count == 0)
            {
                if (currentCost < bestCost)
                {
                    bestCost = currentCost;
                    bestState = (State)state.Clone();
                }
                return bestState;
            }

            // bound
            if (LowerBound(state, referees) >= bestCost)
                return bestState;

            // vybereme další slot (zatím první – lze zlepšit heuristikou)
            Slot slot = state.GetEmptySlots().First();

            foreach (var referee in referees)
            {
                if (!CanAssign(state, slot, referee))
                    continue;

                State next = (State)state.Clone();
                next.SetReferee(slot, referee);

                Solve(next, referees);
            }

            return bestState;
        }



    }
}
