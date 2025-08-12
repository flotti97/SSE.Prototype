using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SSE.Cryptography
{
    public static class CryptoUtils
    {
        private const int DEFAULT_KEY_SIZE = 32;
        public static readonly BigInteger Prime256Bit = BigInteger.Parse("89717256085762065735428409900757717635795337683454659872469413210581406231563");

        public static byte[] GenerateRandomKey(int keySize = DEFAULT_KEY_SIZE)
        {
            return RandomNumberGenerator.GetBytes(keySize);
        }

        public static byte[] DeriveKey(byte[] masterKey, string context, int index)
        {
            using var hmac = new HMACSHA256(masterKey);

            var keywordBytes = Encoding.UTF8.GetBytes(context);
            var indexBytes = BitConverter.GetBytes(index);
            var combined = new byte[keywordBytes.Length + indexBytes.Length];

            Buffer.BlockCopy(keywordBytes, 0, combined, 0, keywordBytes.Length);
            Buffer.BlockCopy(indexBytes, 0, combined, keywordBytes.Length, indexBytes.Length);

            return hmac.ComputeHash(combined);
        }

        public static byte[] Randomize(byte[] key, int counter)
        {
            using var hmac = new HMACSHA256(key);
            var counterBytes = BitConverter.GetBytes(counter);
            return hmac.ComputeHash(counterBytes);
        }

        public static byte[] Randomize(byte[] key, string keyword)
        {
            using var hmac = new HMACSHA256(key);
            var keywordBytes = Encoding.UTF8.GetBytes(keyword);
            return hmac.ComputeHash(keywordBytes);
        }

        public static BigInteger Randomize(byte[] key, string value, BigInteger p)
        {
            if (p <= 2)
                throw new ArgumentException("p must be greater than 2 for Zp*.");

            // Generate a random value in [1, p-1] using HMAC-SHA256 as PRF
            using var hmac = new HMACSHA256(key);
            var valueBytes = Encoding.UTF8.GetBytes(value);
            byte[] hash = hmac.ComputeHash(valueBytes);

            // Convert hash to BigInteger (positive)
            BigInteger result = new BigInteger(hash, isUnsigned: true, isBigEndian: true);

            // Map to [1, p-1]
            result = (result % (p - 1)) + 1;
            return result;
        }

        public static byte[] Encrypt(byte[] key, string data)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                using var sw = new StreamWriter(cs);
                sw.Write(data);
            }
            return ms.ToArray();
        }

        public static string Decrypt(byte[] key, byte[] encryptedData)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = encryptedData.Take(aes.BlockSize / 8).ToArray();
            aes.Mode = CipherMode.CBC;
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(encryptedData.Skip(aes.BlockSize / 8).ToArray());
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
        public static BigInteger ModInverse(byte[] aBytes, BigInteger p)
        {
            // Extended Euclidean Algorithm for modular inverse
            if (p <= 1)
                throw new ArgumentException("Modulus p must be greater than 1.");

            BigInteger a = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);

            BigInteger t = 0, newT = 1;
            BigInteger r = p, newR = a % p;

            while (newR != 0)
            {
                BigInteger quotient = r / newR;
                (t, newT) = (newT, t - quotient * newT);
                (r, newR) = (newR, r - quotient * newR);
            }

            if (r > 1)
                throw new ArgumentException("a is not invertible modulo p.");

            if (t < 0)
                t += p;

            return t;
        }

        public static BigInteger ModMultiply(BigInteger a, BigInteger b, BigInteger p)
        {
            if (p <= 0)
                throw new ArgumentException("Modulus p must be positive.");


            return ((a % p) * (b % p)) % p;
        }
        /// <summary>
        /// Returns a generator g for the multiplicative group Zp* (where p is a prime).
        /// This implementation uses a fixed value for g=2, which is a generator for many safe primes.
        /// For cryptographic use, ensure p is a safe prime and g is a valid generator.
        /// </summary>
        public static BigInteger Generator(BigInteger p)
        {
            if (p <= 2)
                throw new ArgumentException("p must be greater than 2 for Zp*.");

            // Try small candidates for generator (g) in Zp*
            // A generator g must satisfy: g^((p-1)/q) != 1 mod p for all prime divisors q of p-1
            // For safe primes, p = 2q+1, so p-1 = 2q, divisors are 2 and q

            BigInteger q = (p - 1) / 2;
            BigInteger[] candidates = { 2, 3, 5, 7, 11, 13, 17, 19 };

            foreach (var g in candidates)
            {
                // g^((p-1)/2) mod p != 1 and g^((p-1)/q) mod p != 1
                if (BigInteger.ModPow(g, (p - 1) / 2, p) != 1 &&
                    BigInteger.ModPow(g, (p - 1) / q, p) != 1)
                {
                    return g;
                }
            }

            throw new InvalidOperationException("No generator found for the given prime p.");
        }

    }
}
