using System.Numerics;
using System.Security.Cryptography;

namespace SSE.Core
{
    public static class Extensions
    {
        public static BigInteger AsBigInteger(this byte[] bytes, bool isBigEndian = true, bool isUnsigned = true)
        {
            return new BigInteger(bytes, isBigEndian: isBigEndian, isUnsigned: isUnsigned);
        }

        public static void ShuffleInPlace<T>(this List<T> list)
        {
            Span<byte> buf = stackalloc byte[4];
            for (int i = list.Count - 1; i > 0; i--)
            {
                RandomNumberGenerator.Fill(buf);
                int r = BitConverter.ToInt32(buf) & int.MaxValue;
                int j = r % (i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
