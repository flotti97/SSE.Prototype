using SSE.Client.Schemes;
using SSE.Core.Models;
using SSE.Server;
using System.Text;

namespace SSE.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "SSE Prototype TUI";

            while (true)
            {
                Console.Clear();
                DrawHeader("SSE Prototype - Select a Scheme");
                Console.WriteLine("1. Basic Scheme (Cash et al. 2013)");
                Console.WriteLine("2. Boolean Query Scheme (OXT - Cash et al. 2013)");
                Console.WriteLine("3. Substring Query Scheme (Faber et al.)");
                Console.WriteLine("4. Exit");
                Console.WriteLine();
                Console.Write("Select an option: ");

                var key = Console.ReadKey();
                
                switch (key.KeyChar)
                {
                    case '1':
                        RunBasicScheme();
                        break;
                    case '2':
                        RunBooleanScheme();
                        break;
                    case '3':
                        RunSubstringScheme();
                        break;
                    case '4':
                        return;
                }
            }
        }

        static void DrawHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(new string('=', Console.WindowWidth - 1));
            Console.WriteLine($" {title}");
            Console.WriteLine(new string('=', Console.WindowWidth - 1));
            Console.ResetColor();
            Console.WriteLine();
        }

        static Database<(string id, string content)> GetSampleDatabase()
        {
            var data = new List<(string id, string content)>
            {
                ("doc1", "The quick brown fox jumps over the lazy dog"),
                ("doc2", "SSE is a technique for searching encrypted data"),
                ("doc3", "Boolean queries allow searching for multiple keywords"),
                ("doc4", "Substring search is more complex than keyword search"),
                ("doc5", "Cryptography is essential for security"),
                ("doc6", "Alice and Bob are common characters in crypto examples"),
                ("doc7", "Hello world this is a test"),
                ("doc8", "Searchable Symmetric Encryption is cool")
            };

            return new Database<(string id, string content)>(
                data,
                item => item.id,
                item => item.content
            );
        }

        static void RunBasicScheme()
        {
            Console.Clear();
            DrawHeader("Basic Scheme Demo");

            var db = SelectDatabase();
            if (db == null) return;
            
            Console.WriteLine("Setting up database...");
            var (masterKey, encryptedDb) = BasicScheme.Setup(db);
            var server = new EncryptedStorageServer(encryptedDb);
            Console.WriteLine("Database encrypted and setup complete.");
            Console.WriteLine($"Contains {db.Data.Count} documents.");
            Console.WriteLine();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Enter a keyword to search (or 'exit'): ");
                Console.ResetColor();
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                Console.WriteLine($"Searching for '{input}'...");
                
                var (labelKey, identifierKey) = BasicScheme.DeriveSearchKeys(masterKey, input.ToLowerInvariant());
                var results = server.Find(labelKey, identifierKey).ToList();

                if (results.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Found {results.Count} matches: {string.Join(", ", results)}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No matches found.");
                }
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        static void RunBooleanScheme()
        {
            Console.Clear();
            DrawHeader("Boolean Query Scheme (OXT) Demo");

            var db = SelectDatabase();
            if (db == null) return;

            Console.WriteLine("Setting up database...");
            var scheme = new BooleanQueryScheme();
            var (keys, encryptedDb) = scheme.Setup(db);
            var server = new BooleanEncryptedStorageServer(encryptedDb);
            Console.WriteLine("Database encrypted and setup complete.");
            Console.WriteLine($"Contains {db.Data.Count} documents.");
            Console.WriteLine();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Enter keywords separated by space (AND query) (or 'exit'): ");
                Console.ResetColor();
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                var keywords = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Select(k => k.ToLowerInvariant())
                                    .ToArray();

                if (keywords.Length == 0) continue;

                Console.WriteLine($"Searching for AND({string.Join(", ", keywords)})...");

                var results = scheme.Search(server, keywords).ToList();

                if (results.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Found {results.Count} matches: {string.Join(", ", results)}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No matches found.");
                }
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        static void RunSubstringScheme()
        {
            Console.Clear();
            DrawHeader("Substring Query Scheme Demo");

            var db = SelectDatabase();
            if (db == null) return;

            Console.WriteLine("Setting up database...");
            // Using default qMin=3, qMax=5
            var scheme = new SubstringQueryScheme(qMin: 3, qMax: 5); 
            scheme.Setup(db);
            Console.WriteLine("Database encrypted and setup complete.");
            Console.WriteLine("Contains " + db.Data.Count + " documents.");
            Console.WriteLine("Note: qMin=3, qMax=5. Shorter patterns won't match.");
            Console.WriteLine();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Enter a substring pattern to search (or 'exit'): ");
                Console.ResetColor();
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                // Simple validation warning for user
                if (input.Length < 3)
                {
                     Console.ForegroundColor = ConsoleColor.DarkYellow;
                     Console.WriteLine("Warning: Pattern length is less than qMin (3). It might not return results.");
                     Console.ResetColor();
                }

                Console.WriteLine($"Searching for substring '{input}'...");

                // Scheme handles lowercase normalization internally, but good to know
                var results = scheme.Search(input).ToList();

                if (results.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Found {results.Count} matches: {string.Join(", ", results)}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("No matches found.");
                }
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        static Database<(string id, string content)>? SelectDatabase()
        {
            Console.WriteLine("Select Data Source:");
            Console.WriteLine("1. Use Sample Data");
            Console.WriteLine("2. Load from Directory (.txt files)");
            Console.Write("Option: ");
            var key = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine();

            if (key.KeyChar == '2')
            {
                return LoadDatabaseFromDirectory();
            }
            
            return GetSampleDatabase();
        }

        static Database<(string id, string content)>? LoadDatabaseFromDirectory()
        {
            while (true)
            {
                Console.Write("Enter absolute directory path (or 'back' to go back): ");
                var path = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (path.Equals("back", StringComparison.OrdinalIgnoreCase)) return null;
                
                if (Directory.Exists(path))
                {
                    try 
                    {
                        var files = Directory.GetFiles(path)
                            .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(Path.GetExtension(f)))
                            .ToArray();

                        if (files.Length == 0)
                        {
                             Console.ForegroundColor = ConsoleColor.Red;
                             Console.WriteLine("No suitable files (.txt or no extension) found in directory.");
                             Console.ResetColor();
                             continue;
                        }
                        
                        var data = new List<(string id, string content)>();
                        foreach (var file in files)
                        {
                            var content = File.ReadAllText(file);
                            var id = Path.GetFileName(file);
                            data.Add((id, content));
                        }
                        
                        Console.WriteLine($"Loaded {data.Count} files.");
                        return new Database<(string id, string content)>(
                            data,
                            item => item.id,
                            item => item.content
                        );
                    }
                    catch (Exception ex)
                    {
                         Console.ForegroundColor = ConsoleColor.Red;
                         Console.WriteLine($"Error reading files: {ex.Message}");
                         Console.ResetColor();
                    }
                }
                else
                {
                     Console.ForegroundColor = ConsoleColor.Red;
                     Console.WriteLine("Directory does not exist.");
                     Console.ResetColor();
                }
            }
        }
    }
}
