using System;
using System.IO;

namespace SSE.Benchmark
{
    public static class BenchmarkHelper
    {
        public static string GetTestDocumentsPath()
        {
            var path = FindDirectory("test_documents");
            if (path == null)
            {
                throw new DirectoryNotFoundException("Could not find 'test_documents' directory.");
            }

            // We are looking for 20news-18828/sci.crypt inside test_documents
            var fullPath = Path.Combine(path, "20news-18828", "sci.crypt");
            
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
            
            throw new DirectoryNotFoundException($"Could not find '20news-18828/sci.crypt' inside '{path}'.");
        }

        private static string? FindDirectory(string dirName)
        {
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (currentDir != null)
            {
                var target = Path.Combine(currentDir.FullName, dirName);
                if (Directory.Exists(target))
                {
                    return target;
                }
                currentDir = currentDir.Parent;
            }
            return null;
        }
    }
}
