using CsvHelper;
using CsvHelper.Configuration;
using Diplomka.Data;
using Diplomka.Solver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka
{
    public class FileManager
    {
        /*
        public static List<Referee> ReadReferees()
        {
            using (var reader = new StreamReader("..\\..\\..\\referees.csv"))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" }))
            {
                var records = csv.GetRecords<Referee>().ToList();
                return records;
            }
        }

        public static List<Slot> ReadSlots()
        {
            using (var reader = new StreamReader("..\\..\\..\\slots.csv"))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" }))
            {
                var records = csv.GetRecords<Slot>().ToList();
                return records;
            }
        }
        */

        public static List<Referee> ReadReferees()
        {
            var referees = new List<Referee>();

            using var reader = new StreamReader("..\\..\\..\\referees.csv");

            // přeskočíme hlavičku
            reader.ReadLine();

            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(';');

                int id = int.Parse(parts[0]);
                string name = parts[1];
                int level = int.Parse(parts[2]);

                double lat = double.Parse(parts[3], CultureInfo.InvariantCulture);
                double lon = double.Parse(parts[4], CultureInfo.InvariantCulture);
                var location = new Geo(lat, lon);

                referees.Add(new Referee(id, name, level, location));
            }

            return referees;
        }

        public static List<Slot> ReadSlots()
        {
            var slots = new List<Slot>();

            using var reader = new StreamReader("..\\..\\..\\slots.csv");

            // hlavička
            reader.ReadLine();

            while (!reader.EndOfStream)
            {
                string? line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(';');

                int id = int.Parse(parts[0]);
                int level = int.Parse(parts[1]);
                int day = int.Parse(parts[2]);

                double lat = double.Parse(parts[3], CultureInfo.InvariantCulture);
                double lon = double.Parse(parts[4], CultureInfo.InvariantCulture);
                var location = new Geo(lat, lon);

                slots.Add(new Slot(id, level, day, location));
            }

            return slots;
        }


        public static void WriteState(State state, string fileName)
        {
            using var writer = new StreamWriter($"..\\..\\..\\{fileName}", false, Encoding.UTF8);

            // Rozšířená hlavička i o vzdálenost pro kontrolu
            writer.WriteLine("slot;ref;slot level;ref level;day;distance_km");

            foreach (var pair in state)
            {
                Slot slot = pair.Key;
                Referee? referee = pair.Value;

                double dist = 0;
                if (referee != null)
                {
                    dist = referee.Location.DistanceTo(slot.Location);
                }

                writer.WriteLine(
                    $"{slot.Id};" +
                    $"{referee?.Name ?? "UNASSIGNED"};" +
                    $"{slot.Level};" +
                    $"{referee?.Level.ToString() ?? ""};" +
                    $"{slot.Day};" +
                    $"{dist:F2}" // Vzdálenost na 2 desetinná místa
                );
            }
        }
    }
}
