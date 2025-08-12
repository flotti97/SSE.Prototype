namespace SSE.Core.Interfaces
{
    public interface IIndex
    {
        byte[]? Get(byte[] label);
        bool Insert(byte[] label, byte[] identifier);
        bool Remove(byte[] label);
    }

}
