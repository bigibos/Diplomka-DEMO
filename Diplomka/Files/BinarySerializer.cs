using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Diplomka.Files
{
    public class BinarySerializer<T> : ISerializer<T>
    {
        public void Serialize(Stream stream, T data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            using var writer = new BinaryWriter(stream);
            writer.Write(json);
        }

        public T Deserialize(Stream stream)
        {
            using var reader = new BinaryReader(stream);
            var json = reader.ReadString();
            return System.Text.Json.JsonSerializer.Deserialize<T>(json);
        }

    }
}
