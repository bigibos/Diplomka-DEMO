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
    }
}
