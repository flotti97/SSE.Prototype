namespace SSE.Core.Models
{
    public class QueryMessage
    {
        public required byte[] Stag { get; set; }
        public Dictionary<int, List<byte[]>> ConjunctiveTokenSets { get; set; } = new();
    }
}
