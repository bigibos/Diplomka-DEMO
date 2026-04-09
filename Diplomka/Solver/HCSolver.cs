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
        public int EvaluateCost(State state)
        {
            int cost = 0;
            HashSet<string> scheduleLog = new HashSet<string>();

            foreach (var item in state)
            {
                Slot slot = item.Key;
                Referee? referee = item.Value;

                // --- 1. KRITICKÁ PENALIZACE: NEPRIDELENY SLOT ---
                if (referee == null)
                {
                    cost += 10000; // Zvýšeno, aby algoritmus prioritně zaplnil všechny sloty
                    continue;
                }

                // --- 2. KRITICKÁ PENALIZACE: KOLIZE DNE ---
                string scheduleKey = $"{referee.Id}:{slot.Day}";
                if (scheduleLog.Contains(scheduleKey))
                {
                    cost += 5000;
                }
                else
                {
                    scheduleLog.Add(scheduleKey);
                }

                // --- 3. PENALIZACE ZA LEVEL ---
                if (referee.Level < slot.Level)
                {
                    // Rozhodčí má nižší level než slot (vážný problém)
                    cost += (slot.Level - referee.Level) * 200;
                }
                else if (referee.Level > slot.Level)
                {
                    // Rozhodčí má vyšší level než slot (mírná penalizace, 
                    // chceme si "lepší" rozhodčí šetřit pro těžší zápasy)
                    cost += (referee.Level - slot.Level) * 20;
                }

                // --- 4. PENALIZACE ZA VZDÁLENOST (KLÍČOVÁ ZMĚNA) ---
                // Vypočítáme vzdálenost v km
                double distance = referee.Location.DistanceTo(slot.Location);

                // Přidáme vzdálenost k ceně. 
                // 1 km = 1 jednotka ceny. 
                // Pokud je vzdálenost 100 km, přičte to 100 k ceně.
                cost += (int)Math.Round(distance);
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
            int bestCost = EvaluateCost(bestState);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int currentCost = EvaluateCost(currentState);

                for (int iteration = 0; iteration < maxIterations; iteration++)
                {
                    bool foundNextState = false;    

                    for (int move = 0; move < maxMoves; move++)
                    {
                        State nextState = (State)currentState.Clone();
                        
                        ApplyRandomMove(nextState, referees);

                        int nextCost = EvaluateCost(nextState);

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
