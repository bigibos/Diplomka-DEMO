using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Diplomka.Files
{
    internal class JsonSerializer<T> : ISerializer<T>
    {
        private readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true, // čitelné
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = ReferenceHandler.Preserve, // pro cyklické reference
        };

        public void Serialize(Stream stream, T data)
        {
            System.Text.Json.JsonSerializer.Serialize(stream, data, _options);
        }

        public T Deserialize(Stream stream)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(stream, _options);
        }

    }
}
