using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Files
{
    public interface ISerializer<T>
    {
        void Serialize(Stream stream, T data);
        T Deserialize(Stream stream);
    }
}
