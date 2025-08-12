using System.Text.RegularExpressions;

namespace SSE.Core.Models
{
    public class Database<T>
    {
        public List<T> Data { get; set; }
        private readonly Func<T, string> _keySelector;
        private readonly Func<T, string> contentSelector;

        public Database(IEnumerable<T> data, Func<T, string> keySelector, Func<T, string> contentSelector)
        {
            Data = data.ToList();
            _keySelector = keySelector;
            this.contentSelector = contentSelector;
        }

        /// <summary>
        /// Generates a dictionary of metadata by tokenizing the input data and associating tokens with their
        /// corresponding identifiers.
        /// </summary>
        /// <remarks>This method processes the data collection by applying a key selector function to
        /// extract identifiers and a tokenizer function to generate tokens. Each token is mapped to a list of
        /// identifiers that are associated with it.</remarks>
        /// <returns>A dictionary where the keys are tokens (strings) and the values are lists of identifiers (strings)
        /// associated with those tokens. If no tokens are generated, the dictionary will be empty.</returns>
        public Dictionary<string, List<string>> GetMetadata()
        {
            var metadata = new Dictionary<string, List<string>>();

            foreach (var item in Data)
            {
                var identifier = _keySelector(item);
                var tokens = Tokenize(item);

                foreach (var token in tokens)
                {
                    if (metadata.TryGetValue(token, out var identifiers))
                    {
                        identifiers.Add(identifier);
                    }
                    else
                    {
                        metadata[token] = [identifier];
                    }
                }
            }

            return metadata;
        }

        public HashSet<string> Tokenize(T item)
        {
            var content = contentSelector(item);
            if (string.IsNullOrWhiteSpace(content))
                return [];

            // Use Regex to split on non-word characters (excluding underscore)
            var tokens = Regex
                .Split(content, @"\W+")
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Select(token => token.ToLowerInvariant());

            return [.. tokens];
        }
    }
}
