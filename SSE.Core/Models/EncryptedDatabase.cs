using SSE.Core.Interfaces;

namespace SSE.Core.Models
{
    public class EncryptedDatabase : IIndex
    {
        private readonly Dictionary<byte[], byte[]> index;

        public EncryptedDatabase(Dictionary<byte[], byte[]> index)
        {
            this.index = index;
        }

        public byte[]? Get(byte[] label)
        {
            if (index.TryGetValue(label, out var identifier))
            {
                return identifier;
            }
            return null;
        }

        public bool Insert(byte[] label, byte[] identifier)
        {
            throw new NotImplementedException();
        }

        public bool Remove(byte[] label)
        {
            throw new NotImplementedException();
        }
    }
}
