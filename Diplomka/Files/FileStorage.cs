using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Files
{
    internal class FileStorage : IFileStorage
    {
        public void Save<T>(string path, T data, ISerializer<T> serializer)
        {
            using var fs = new FileStream(path, FileMode.Create);
            serializer.Serialize(fs, data);
        }

        public T Load<T>(string path, ISerializer<T> serializer)
        {
            using var fs = new FileStream(path, FileMode.Open);
            return serializer.Deserialize(fs);
        }

    }
}
