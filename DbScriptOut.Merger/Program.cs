using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbScriptOut.Merger
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("use: {0} [folder]", "DbScriptOut.Merger.exe");
                Environment.Exit(0);
            }

            var files = new List<string>();

            var folder = System.IO.Path.Combine(Environment.CurrentDirectory, args[0]);

            if (!System.IO.Directory.Exists(folder))
            {
                Console.WriteLine("folder does not exist.");
                Environment.Exit(0);
            }

            var manifestFiles = new[] { "TableManifest.txt", "ViewManifest.txt"/*, "DataManifest.txt"*/ };
            var missingManifesst = false;

            foreach (var file in manifestFiles)
            {
                if (!System.IO.File.Exists(System.IO.Path.Combine(folder, file)))
                {
                    Console.WriteLine("Manifest File does not exist: {0}", file);
                    missingManifesst = true;
                }
                else
                {
                    files.Add(System.IO.Path.Combine(folder, file));
                }

                
            }
            if (missingManifesst)
                Environment.Exit(0);

            var outputFileName = System.IO.Path.Combine(".", "IMP_BASE.sql");

            using (var output = new System.IO.StreamWriter(System.IO.File.Open(outputFileName, System.IO.FileMode.Create)))
            {
                foreach (var manifest in files)
                {
                    var reading = System.IO.File.OpenText(manifest);
                    var line = string.Empty;
                    do
                    {
                        line = reading.ReadLine();

                        if (!string.IsNullOrEmpty(line) && System.IO.File.Exists(manifest))
                        {
                            var objectFileName = System.IO.Path.Combine(folder, line);
                            output.Write(System.IO.File.ReadAllText(objectFileName));
                        }

                    } while (!string.IsNullOrEmpty(line));
                }
            }
        }
    }
}
