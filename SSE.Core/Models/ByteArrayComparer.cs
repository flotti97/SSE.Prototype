public class ByteArrayComparer : IEqualityComparer<byte[]>
{
    public bool Equals(byte[]? x, byte[]? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        if (x.Length != y.Length) return false;
        return x.SequenceEqual(y);
    }

    public int GetHashCode(byte[] obj)
    {
        if (obj is null) return 0;
        unchecked
        {
            int hash = 17;
            foreach (var b in obj)
                hash = hash * 31 + b;
            return hash;
        }
    }
}