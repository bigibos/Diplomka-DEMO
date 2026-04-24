using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class SolverConfiguration
    {
        /*
         * Parametry pro cas potrebny pred zapasem (priprava) a po zapase (uzavreni)
         */
        public TimeSpan RefereePrepTime { get; set; } = TimeSpan.FromMinutes(90);
        public TimeSpan RefereePostTime { get; set;} = TimeSpan.FromMinutes(120);

        // TODO: Nasledujici oba configy se budou mazat - vazba na vypocty, ktere se nepouzivaji
        public TimeSpan HomeReturnMaxGap { get; set; } = TimeSpan.FromHours(12);
        public double HomeReturnScoreThreshold { get; set; } = 30.0;

        /*
         * Maximalni mozny promarneny cas
         * Vyuziva se k rozhodovani, jestli bude rozhodci cestovat ze sveho zazemi, nebo z akt. slotu
         */
        public TimeSpan MaxWasteTime { get; set; } = TimeSpan.FromHours(4);

        /*
         * Vahove koeficienty manipulaci pocitane ceny
         */
        public double RankFactor { get; set; } = 1.0; // Jakou vahu rozdil v urovni
        public double DistanceFactor { get; set; } = 1.0; // Jakou vahu ma vzdalenost
        public double UnderRankFactor { get; set; } = 1.0; // Jakou vahu ma nekvalifikovanost
        public double OverRankFactor { get; set; } = 1.0; // Jakou vahu ma prekvalifikovanost

        /*
         * Velka cena pro nevyplneny slot
         */
        public double UnassignedCost { get; set; } = 1000;
    }
}
