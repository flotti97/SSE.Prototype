using SSE.Core.Models;
using SSE.Server;
using System.Text;

namespace SSE.Client.Schemes
{
    /// <summary>
    /// Substring Searchable Symmetric Encryption (SUB-SSE-OXT) following Faber et al. 2015 design idea:
    /// Reduce substring search to a conjunctive multi-keyword query over q-grams using the underlying OXT boolean scheme.
    /// This prototype uses fixed q-gram window sizes (defaults 3..5) and performs client-side post-filtering.
    /// </summary>
    public class SubstringQueryScheme
    {
        private readonly BooleanQueryScheme booleanScheme;
        private readonly int qMin;
        private readonly int qMax;
        private readonly char boundaryChar;
        private Dictionary<string, List<string>>? gramToDocIds; // built metadata
        private BooleanEncryptedDatabase? encryptedDb;
        private BooleanEncryptedStorageServer? server;

        public SubstringQueryScheme(int qMin = 3, int qMax = 5, char boundaryChar = '#')
        {
            if (qMin < 2 || qMin > qMax) throw new ArgumentException("Invalid q-range");
            this.qMin = qMin;
            this.qMax = qMax;
            this.boundaryChar = boundaryChar;
            this.booleanScheme = new BooleanQueryScheme();
        }

        /// <summary>
        /// Builds q-gram metadata and underlying encrypted OXT structures from plaintext database content.
        /// Expected database tuple: (id, content)
        /// </summary>
        public void Setup(Database<(string id, string content)> database)
        {
            gramToDocIds = BuildQGramMetadata(database);
            var (_, db) = booleanScheme.SetupFromMetadata(gramToDocIds);
            encryptedDb = db;
            server = new BooleanEncryptedStorageServer(db);
        }

        /// <summary>
        /// Execute a substring search returning matching document identifiers.
        /// </summary>
        public IEnumerable<string> Search(string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || encryptedDb == null || server == null || gramToDocIds == null)
                return Enumerable.Empty<string>();

            // Short pattern handling: if pattern length < qMin, fall back to scanning all docs (leakage heavy) - here return empty for simplicity.
            if (pattern.Length < qMin) return Enumerable.Empty<string>();

            var grams = ExtractQueryQGrams(pattern).ToArray();
            if (grams.Length == 0) return Enumerable.Empty<string>();

            // Choose pivot = rarest gram (min posting list size) to optimize OXT
            var pivot = grams.OrderBy(g => gramToDocIds.TryGetValue(g, out var lst) ? lst.Count : int.MaxValue).First();
            var reordered = new List<string> { pivot };
            reordered.AddRange(grams.Where(g => g != pivot));

            var candidateDocIds = booleanScheme.Search(server, reordered.ToArray()).ToList();

            // Post-filter: verify substring actually appears (would need original plaintext; we only stored id->content externally)
            // Here we cannot access plaintext database inside this class after setup unless we store mapping; store simple map for filtering.
            var idToContent = plaintextCache; // may be null if not captured.
            if (idToContent == null)
            {
                // Without plaintext, return candidates (could include false positives).
                return candidateDocIds;
            }
            var patternLower = pattern.ToLowerInvariant();
            return candidateDocIds.Where(id => idToContent.TryGetValue(id, out var c) && c.Contains(patternLower, StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, string>? plaintextCache; // optional mapping for verification

        /// <summary>
        /// Build mapping q-gram -> docIds (sets) for all docs.
        /// </summary>
        private Dictionary<string, List<string>> BuildQGramMetadata(Database<(string id, string content)> database)
        {
            var map = new Dictionary<string, List<string>>();
            plaintextCache = database.Data.ToDictionary(t => t.id, t => t.content.ToLowerInvariant());
            foreach (var (id, content) in database.Data)
            {
                foreach (var gram in ExtractAllDocumentQGrams(content))
                {
                    if (!map.TryGetValue(gram, out var list))
                    {
                        list = new List<string>();
                        map[gram] = list;
                    }
                    list.Add(id);
                }
            }
            return map;
        }

        private IEnumerable<string> ExtractAllDocumentQGrams(string content)
        {
            if (string.IsNullOrEmpty(content)) yield break;
            var norm = content.ToLowerInvariant();
            var padded = new string(boundaryChar, qMax - 1) + norm + new string(boundaryChar, qMax - 1);
            for (int q = qMin; q <= qMax; q++)
            {
                for (int i = 0; i + q <= padded.Length; i++)
                {
                    var slice = padded.AsSpan(i, q);
                    // Skip grams consisting solely of boundary chars
                    if (slice.IndexOf(boundaryChar) == 0 && slice.ToString().All(c => c == boundaryChar)) continue;
                    yield return slice.ToString();
                }
            }
        }

        private IEnumerable<string> ExtractQueryQGrams(string pattern)
        {
            var norm = pattern.ToLowerInvariant();
            if (norm.Length < qMin) yield break;
            int qUpper = Math.Min(qMax, norm.Length);
            for (int q = qUpper; q >= qMin; q--) // prefer longer grams first (better selectivity)
            {
                for (int i = 0; i + q <= norm.Length; i++)
                {
                    yield return norm.Substring(i, q);
                }
                // Use only one q size (largest fitting) for query to bound token set size
                break;
            }
        }
    }
}
