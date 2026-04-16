using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Files
{
    public interface IFileStorage
    {
        void Save<T>(string path, T data, ISerializer<T> serializer);
        T Load<T>(string path, ISerializer<T> serializer);
    }
}
