using CsvHelper;
using CsvHelper.Configuration;
using Diplomka.ImportExport.Dto;
using Diplomka.ImportExport.Mapping;
using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport
{
    /// <summary>
    /// Nástroj pro importování dat z CSV souboru
    /// </summary>
    public static class CsvImporter
    {
        // Konfigurace formatu CSV
        static CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ";"
        };

        /// <summary>
        /// Metoda pro načtení seznamu slotů z CSV souboru
        /// </summary>
        /// <param name="path">Cesta k souboru</param>
        /// <returns>Nový vytvořený seznam slotů</returns>
        public static List<Slot> LoadSlots(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, csvConfig);

            csv.Context.RegisterClassMap<SlotMap>();

            csv.Read();
            csv.ReadHeader();

            var records = csv.GetRecords<SlotCsvDto>().ToList();

            return records.Select(CsvMapper.ToEntity).ToList();
        }

        /// <summary>
        /// Metoda pro načtení seznamu rozhodčích z CSV souboru
        /// </summary>
        /// <param name="path">Cesta k souboru</param>
        /// <returns>Nový vytvořený seznam rozhodčích</returns>
        public static List<Referee> LoadReferees(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, csvConfig);

            csv.Context.RegisterClassMap<RefereeMap>();

            csv.Read();
            csv.ReadHeader();

            var records = csv.GetRecords<RefereeCsvDto>().ToList();

            return records.Select(CsvMapper.ToEntity).ToList();
        }

        // TODO: Upravit na vkladani referenci slotu misto indexu
        /// <summary>
        /// Metoda pro načtení zákazu rozhodčích ke slotům z CSV souboru
        /// </summary>
        /// <param name="path">Cesta k souboru</param>
        /// <param name="referees">Seznam rozhočích pro jejich nastavení</param>
        public static void LoadBans(string path, List<Referee> referees)
        {
            if (!File.Exists(path)) return;

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, csvConfig);
            csv.Context.RegisterClassMap<BanMap>();
            csv.Read(); csv.ReadHeader();

            var refById = referees.ToDictionary(r => r.Id);

            foreach (var dto in csv.GetRecords<BanCsvDto>())
            {
                if (refById.TryGetValue(dto.RefereeId, out var referee))
                    referee.BannedSlotIds.Add(dto.SlotId);
            }
        }

        // TODO: Upravit na vkladani referenci rozhodcich misto indexu
        /// <summary>
        /// Metoda pro načtení vzájemné nesnášenlivostí rozhodčích z CSV souboru
        /// </summary>
        /// <param name="path">Cesta k souboru</param>
        /// <param name="referees">Seznam rozhočích pro jejich nastavení</param>
        public static void LoadIncompatible(string path, List<Referee> referees)
        {
            if (!File.Exists(path)) return;

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, csvConfig);
            csv.Context.RegisterClassMap<IncompatibleMap>();
            csv.Read(); csv.ReadHeader();

            var refById = referees.ToDictionary(r => r.Id);

            foreach (var dto in csv.GetRecords<IncompatibleCsvDto>())
            {
                // Symetrické napojení - stačí jeden záznam v CSV, obě strany se propojí
                if (refById.TryGetValue(dto.RefereeIdA, out var refA))
                    refA.IncompatibleRefereeIds.Add(dto.RefereeIdB);

                if (refById.TryGetValue(dto.RefereeIdB, out var refB))
                    refB.IncompatibleRefereeIds.Add(dto.RefereeIdA);
            }
        }
    }
}
