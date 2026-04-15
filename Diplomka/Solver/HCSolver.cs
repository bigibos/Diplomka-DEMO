using Diplomka.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class HCSolver
    {
        public int AssignmentCost(Slot slot, Referee referee)
        {
            // 1. Rozdíl úrovní (např. 0, 1, 2...)
            int levelDifference = Math.Abs(slot.RequiredRank - referee.Rank);

            // 2. Vzdálenost v km (např. 15.5 km)
            double distance = referee.Location.DistanceTo(slot.Match.Location);

            // 3. Vážená suma
            // Příklad: 1 stupeň úrovně navíc je "stejně drahý" jako 50 km cesty.
            // Tuhle váhu si můžeš upravit podle potřeby.
            double levelWeight = 100.0;
            double distanceWeight = 2.0;

            double totalCost = (levelDifference * levelWeight) + (distance * distanceWeight);

            return (int)Math.Round(totalCost);
        }


        public int StateCost(State state)
        {
            int cost = 0;

            foreach (var pair in state)
            {
                if (pair.Value != null)
                {
                    cost += AssignmentCost(pair.Key, pair.Value);
                }
                else
                {
                    cost += 10000;
                }
            }

            return cost;
        }

        public State RandomState(List<Slot> slots, List<Referee> referees)
        {
            Random random = new Random();
            State state = new State();

            foreach (var slot in slots)
            {
                state.AddSlot(slot);
                state.AddReferee(slot, referees[random.Next(referees.Count)]);
            }

            return state;
        }

        private void ApplyRandomMove(State state, List<Referee> referees)
        {
            Random random = new Random();

            var slots = state.GetSlots();


            var randomSlot = slots[random.Next(slots.Count)];
            var randomReferee = referees[random.Next(referees.Count)];

            state.AddReferee(randomSlot, randomReferee);

        }

        public State Solve(List<Slot> slots, List<Referee> referees)
        {
            int maxAttempts = 10;
            int maxIterations = 1000;
            int maxMoves = 20;

            State currentState = RandomState(slots, referees);

            // Console.WriteLine(currentState.GetSlots().Count);

            State bestState = (State)currentState.Clone();
            int bestCost = StateCost(bestState);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int currentCost = StateCost(currentState);

                for (int iteration = 0; iteration < maxIterations; iteration++)
                {
                    bool foundNextState = false;    

                    for (int move = 0; move < maxMoves; move++)
                    {
                        State nextState = (State)currentState.Clone();
                        
                        ApplyRandomMove(nextState, referees);

                        int nextCost = StateCost(nextState);

                        if (nextCost == 0)
                            return nextState;   

                        if (nextCost < currentCost)
                        {
                            currentState = nextState;
                            currentCost = nextCost;
                            foundNextState = true;
                            break;
                        }
                    }

                    if (currentCost < bestCost)
                    {
                        bestState = (State)currentState.Clone();
                        bestCost = currentCost;
                        // Console.WriteLine($"Nalezeno lepsi reseni! Cena: {bestCost} (Pokus: {attempt})");
                    }
                    else if (!foundNextState)
                    {
                        // Nenasel se lepsi stav, ukoncit iteraci
                        break;
                    }

                }

                currentState = RandomState(slots, referees);
            }

            return bestState;
        }
    }
}
