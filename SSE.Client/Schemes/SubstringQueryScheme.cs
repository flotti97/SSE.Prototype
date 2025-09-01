using SSE.Core.Models;
using SSE.Server;

namespace SSE.Client.Schemes
{
    /// <summary>
    /// SUB-OXT style scheme (Faber et al.)
    /// </summary>
    public class SubstringQueryScheme
    {
        private readonly BooleanQueryScheme booleanScheme;
        private readonly int qMin;
        private readonly int qMax;
        private readonly char boundaryChar;
        private Dictionary<string, List<string>>? tokenToDocIds;
        private BooleanEncryptedStorageServer? server;

        public SubstringQueryScheme(int qMin = 3, int qMax = 5, char boundaryChar = '#')
        {
            if (qMin < 2 || qMin > qMax) throw new ArgumentException("Invalid q range");
            this.qMin = qMin;
            this.qMax = qMax;
            this.boundaryChar = boundaryChar;
            booleanScheme = new BooleanQueryScheme();
        }

        /// <summary>
        /// Builds token metadata (q-gram + adjacency tokens) and underlying encrypted OXT structures from plaintext database content.
        /// Expected database tuple: (id, content)
        /// </summary>
        public void Setup(Database<(string id, string content)> database)
        {
            tokenToDocIds = BuildTokenMetadata(database);
            var (_, db) = booleanScheme.SetupFromMetadata(tokenToDocIds);
            server = new BooleanEncryptedStorageServer(db);
        }

        /// <summary>
        /// Execute a substring search returning matching document identifiers.
        /// Pattern must have length >= qMin.
        /// </summary>
        public IEnumerable<string> Search(string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || server == null || tokenToDocIds == null) return Enumerable.Empty<string>();
            var norm = pattern.ToLowerInvariant();
            if (norm.Length < qMin) return Enumerable.Empty<string>();

            int qSel = Math.Min(qMax, norm.Length);
            var grams = ExtractFixedQGrams(norm, qSel).ToList();
            if (grams.Count == 0) return Enumerable.Empty<string>();

            List<string> tokens = new();
            foreach (var g in grams) tokens.Add($"G:{qSel}:{g}");
            for (int i = 0; i + 1 < grams.Count; i++) tokens.Add($"A:{qSel}:{grams[i]}|{grams[i + 1]}");
            // full-pattern token if pattern fits within qMax and is not identical to qSel single gram case already covered
            if (norm.Length >= qMin && norm.Length <= qMax && norm.Length != qSel)
            {
                tokens.Add($"G:{norm.Length}:{norm}");
            }
            // If pattern length exactly equals qSel but contained multiple grams (only when norm.Length > qSel) we already covered; if exactly one gram we already added it.
            if (tokens.Count == 0) return Enumerable.Empty<string>();

            var pivot = tokens.OrderBy(t => tokenToDocIds.TryGetValue(t, out var lst) ? lst.Count : int.MaxValue).First();
            var reordered = new List<string> { pivot };
            reordered.AddRange(tokens.Where(t => t != pivot));
            return booleanScheme.Search(server, reordered.ToArray());
        }

        /// <summary>
        /// Build mapping token -> docIds. Tokens include:
        ///  G:gram (per occurrence set semantics)
        ///  A:gram_i|gram_{i+1} (adjacent consecutive grams)
        /// A document is associated with a token if the token occurs at least once (no frequency leakage beyond presence).
        /// </summary>
        private Dictionary<string, List<string>> BuildTokenMetadata(Database<(string id, string content)> database)
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var (id, content) in database.Data)
            {
                var norm = content.ToLowerInvariant();
                // Pad with boundary chars so prefix/suffix patterns become distinct
                var padded = new string(boundaryChar, qMax - 1) + norm + new string(boundaryChar, qMax - 1);
                var seen = new HashSet<string>();
                for (int q = qMin; q <= qMax; q++)
                {
                    var grams = ExtractFixedQGrams(padded, q).ToList();
                    foreach (var g in grams)
                    {
                        // Skip all-boundary grams
                        if (g.All(c => c == boundaryChar)) continue;
                        var token = $"G:{q}:{g}";
                        if (seen.Add(token)) Append(map, token, id);
                    }
                    for (int i = 0; i + 1 < grams.Count; i++)
                    {
                        var g1 = grams[i];
                        var g2 = grams[i + 1];
                        if ((g1.All(c => c == boundaryChar)) || (g2.All(c => c == boundaryChar))) continue;
                        var atok = $"A:{q}:{g1}|{g2}";
                        if (seen.Add(atok)) Append(map, atok, id);
                    }
                }
            }
            return map;
        }

        private static void Append(Dictionary<string, List<string>> map, string token, string docId)
        {
            if (!map.TryGetValue(token, out var list)) map[token] = list = new List<string>();
            list.Add(docId);
        }

        /// <summary>
        /// Extract fixed-length q-grams from text (no padding). Overlapping grams length q.
        /// </summary>
        private static IEnumerable<string> ExtractFixedQGrams(string text, int q)
        {
            if (string.IsNullOrEmpty(text) || text.Length < q) yield break;
            for (int i = 0; i + q <= text.Length; i++) yield return text.Substring(i, q);
        }
    }
}
