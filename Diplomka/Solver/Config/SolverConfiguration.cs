using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver.Config
{
    /// <summary>
    /// Konfiguracni záznam pro nastavování globálních parametrů
    /// </summary>
    public record SolverConfiguration
    {
        /// <summary>
        /// Maximální přípstné množství slotů, ke kterým může být jednotlivý rozhodčí přiřazen
        /// </summary>
        public int MaxRefereSlots { get; set; } = 20;


        /// <summary>
        /// Čas na přípravu před zápasovým slotem
        /// </summary>
        /// <example>
        /// Seznámení s prostředím, přípřava vybavení apod.
        /// </example>
        public TimeSpan RefereePrepTime { get; set; } = TimeSpan.FromMinutes(90);

        /// <summary>
        /// Čas na dokončení por zápasovém slotu
        /// </summary>
        /// <example>
        /// Provedení zápisu o utkání, převlékání apod.
        /// </example>
        public TimeSpan RefereePostTime { get; set;} = TimeSpan.FromMinutes(60);

        /// <summary>
        /// Maximální čas, který může rozhodčí promarnit na místě zápasového slotu pro jeho skončení
        /// </summary>
        /// <example>
        /// Rozhodčí dokončí jeden slot ve 13:00 a další mu začíná v 17:00
        /// Pokud čas promarnění bude:
        ///     a) 6h - tak bude počítána cesta do dalšího slotu přímo z aktuálního (nebo setrvá na místě pokud se koná ve stejné lokaci)
        ///     c) 2h - tak se mezitím bude předpokládat návrat do zázemí a cesta se bude počítat do dalšího slotu odtamtud
        /// </example>
        public TimeSpan MaxWasteTime { get; set; } = TimeSpan.FromHours(6);

        /// <summary>
        /// Koeficient pro nastavení váhy vzdálenosti při výpočtu ceny
        /// </summary>
        public double DistanceFactor { get; set; } = 1.0;

        /// <summary>
        /// Koeficient pro nastavení váhy podkvalifikovanosti při výpočtu ceny
        /// </summary>
        public double UnderRankFactor { get; set; } = 1.0;

        /// <summary>
        /// Koeficient pro nastavení váhy překvalifikovanosti při výpočtu ceny
        /// </summary>
        public double OverRankFactor { get; set; } = 1.0;



        /// <summary>
        /// Staticky nastavené cena pro nepřiřazené sloty. Měla by být velká, aby se maximálně zlepšila efektivnost opt. algortimů
        /// </summary>
        public double UnassignedCost { get; set; } = 100000;

        /// <summary>
        /// Maximální povolená odchylka při kontrole podkvalifikovanosti
        /// </summary>
        public double RankDiffMargin { get; set; } = 10;

        /// <summary>
        /// Konfigurace opravného algoritmu. Řiká kolik opravných pokusů se má provést předtím, než to algoritmus vzdá
        /// </summary>
        public int MaxRepairPasses { get; set; } = 4;
    }
}
