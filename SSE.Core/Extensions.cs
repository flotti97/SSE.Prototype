using System.Numerics;

namespace SSE.Core
{
    public static class Extensions
    {
        public static BigInteger AsBigInteger(this byte[] bytes, bool isBigEndian = true, bool isUnsigned = true)
        {
            return new BigInteger(bytes, isBigEndian: isBigEndian, isUnsigned: isUnsigned);
        }
    }
}
