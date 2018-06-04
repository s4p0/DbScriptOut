using LibGit2Sharp;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DbScriptOut
{
    //public static class Extensions {
    //    public static UrnCollection GetObjectsInDepedencyOrder(this Scripter sc, Urn[] objects)
    //    {
    //        var result = new UrnCollection();
    //        var tree = sc.DiscoverDependencies(objects, true);
    //        var walk = sc.WalkDependencies(tree);
    //        foreach (var item in walk)
    //            result.Add(item.Urn);

    //        return result;
    //    }

    //    public static void ExportFilesToScriptAndManifest( this Scripter scp,  Urn[] objects, string manifestName, HashSet<Urn> hash = null, string outputDir = "output")
    //    {
    //        const string fileNameFormat = "{0}_{1}.SQL";
    //        using (var file = System.IO.File.CreateText(System.IO.Path.Combine(outputDir, manifestName)))
    //        {
    //            foreach (var item in scp.GetObjectsInDepedencyOrder(objects))
    //            {
    //                if (hash == null || !hash.Contains(item))
    //                {
    //                    if (hash != null) hash.Add(item);
    //                    string objName = null;

    //                    var objEntity = scp.Server.GetSmoObject(item);
    //                    if (objEntity as View != null)
    //                        objName = (objEntity as View).Name;
    //                    else if (objEntity as Table != null)
    //                        objName = (objEntity as Table).Name;
    //                    else
    //                        throw new Exception(string.Format("Urn Type Not Expected: {0}", objEntity));
                        
    //                    if (objName != null)
    //                    {
    //                        var onlyData = scp.Options.ScriptData;
    //                        var fileName = string.Format(fileNameFormat,  onlyData ? "DATA" : item.Type, objName);
    //                        file.WriteLine(fileName);
    //                        scp.Options.FileName = System.IO.Path.Combine(outputDir, fileName);
    //                        scp.EnumScript(new[] { item });
    //                    }
    //                }
    //            }
    //        }

    //    }

    //}

    class Program
    {
        const string directory = "OUTPUT_AMCEL";
        static void Main(string[] args)
        {
            var parameters = new ParametersParser(args);
            if (parameters.IsMissingMandatory)
            {
                Console.WriteLine(parameters.CommandLineUse);
                Environment.Exit(0);
            }

            var db = new DbVersioning(parameters);
            try
            {
                var path = parameters["Folder"];
                if (!Directory.Exists(path))
                    path = Path.Combine(Environment.CurrentDirectory, parameters["Folder"]);

                var isGit = Directory.Exists(Path.Combine(path, ".git"));

                if (!isGit)
                {
                    Repository.Init(path);
                    using (var repo = new Repository(path))
                    {
                        var readmePath = Path.Combine(path, "README");
                        using (var file = File.CreateText(readmePath))
                        {
                            file.WriteLine("Db Versioning.");
                        }
                        Commands.Stage(repo, readmePath);

                        Signature author = new Signature("Felipe Correa", "felipe.correa@gmail.com", DateTime.Now);
                        Signature committer = author;

                        var committed = repo.Commit($"Initial Commit", author, committer);
                    }
                }   

                using (var repo = new Repository(path))
                {
                    if (repo.Branches.Any())
                    {
                        Branch originMaster = repo.Branches["master"];
                        repo.Reset(ResetMode.Hard, originMaster.Tip);
                    }
                    
                    db.Connect()
                        .Setup(null)
                        .Export()
                        .ShowDifference();
                    var b = 0;
                    if(repo.Diff != null && repo.Diff.Compare<TreeChanges>(repo.Head.Tip.Tree,
                                                  DiffTargets.Index | DiffTargets.WorkingDirectory) != null)
                    {
                        Console.Clear();
                        foreach (TreeEntryChanges c in repo.Diff.Compare<TreeChanges>(repo.Head.Tip.Tree,
                                                  DiffTargets.Index | DiffTargets.WorkingDirectory))
                        {
                            Console.WriteLine($"File: {System.IO.Path.GetFileName(c.Path)} status is: {c.Status}");
                        }
                        Console.Write("Would you like to commit this?: [NO/yes] ");
                        var input = Console.ReadLine();
                        if (input.ToLowerInvariant().Equals("yes") || input.ToLowerInvariant().Equals("y"))
                        {
                            Console.WriteLine("Type a commit message: ");
                            var message = Console.ReadLine();
                            Commands.Stage(repo, "*");

                            Signature author = new Signature("Felipe Correa", "felipe.correa@gmail.com", DateTime.Now);
                            Signature committer = author;

                            var committed = repo.Commit($"Auto: {message}", author, committer);
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {

                throw;
            }
            
        }

        static void Main1(string[] args)
        {

            //var server = @"(LOCAL)";
            //var dbName = "DEMO";  // "ZENITH_PRODUCTION";
            //var user = "sa";
            //var password = "savcor";


            var server = @"10.0.11.103\SQL2012";
            var dbName = "AMCEL_ZENITH_PRODUCTION";  // "ZENITH_PRODUCTION";
            var user = "sa";
            var password = "savcor";

            var workingDir = AppDomain.CurrentDomain.BaseDirectory;
            var outputDir = System.IO.Path.Combine(workingDir, directory);

            if (!System.IO.Directory.Exists(outputDir))
                System.IO.Directory.CreateDirectory(outputDir);
            

            /// connection
            var conn = new ServerConnection(server, user, password);
            //conn.BeginTransaction();
            //conn.CommitTransaction();

            // server
            Server srv = new Server(conn);

            // database
            var db = srv.Databases[dbName];
            db.CompatibilityLevel = CompatibilityLevel.Version100;

            // scripter engine
            Scripter scp = new Scripter(srv);

            #region Scripter Options
            scp.Options.NoCollation = true;
            scp.Options.NoCommandTerminator = true;
            // only schema
            scp.Options.ScriptSchema = true;
            scp.Options.ScriptData = false;
            // no GO's
            scp.Options.NoCommandTerminator = false;
            // without output stream (all objects at once)
            scp.Options.ToFileOnly = true;
            // objects defaults
            scp.Options.AllowSystemObjects = false;
            scp.Options.Permissions = true;
            scp.Options.SchemaQualify = true;
            scp.Options.AnsiFile = true;
            scp.Options.AnsiPadding = false;

            scp.Options.SchemaQualifyForeignKeysReferences = true;
            scp.Options.DriAllConstraints = true;
            scp.Options.DriIndexes = true;
            scp.Options.DriClustered = true;
            scp.Options.DriNonClustered = true;
            scp.Options.Indexes = true;
            scp.Options.NonClusteredIndexes = true;
            scp.Options.ClusteredIndexes = true;
            scp.Options.FullTextIndexes = true;

            scp.Options.EnforceScriptingOptions = true;
            /// case of our tests
            scp.Options.WithDependencies = false;
            #endregion


            var values = db.Tables["SAVGLOBALIZATION_VALUES"];
            var globalization = db.Tables["SAVGLOBALIZATION_VALUES"];



            using (var repo = new Repository(outputDir))
            {
                var branchName = GetCurrentBranchName();
                var branch = repo.CreateBranch(branchName);
                var master = repo.Branches["master"];
                Commands.Checkout(repo, branch);

                // remove all .sql files before start
                foreach (var file in System.IO.Directory.GetFiles(outputDir, "*.SQL"))
                {
                    System.IO.File.Delete(file);
                }

                scp.Options.ScriptSchema = false;
                scp.Options.ScriptData = true;
                scp.ExportFilesToScriptAndManifest(new[] { values.Urn, globalization.Urn }, "DataManifest.txt", null, outputDir: outputDir);

                scp.Options.ScriptSchema = true;
                scp.Options.ScriptData = false;

                // prepare tables to export
                var tables = db.Tables.Cast<Table>()
                    .Where(tbl => !tbl.IsSystemObject)
                    .Select(n => n.Urn).ToArray();

                // Export it as manifest
                HashSet<Urn> hash = new HashSet<Urn>();

                scp.FilterCallbackFunction = FilterTablesOnly;
                scp.ExportFilesToScriptAndManifest(tables, "TableManifest.txt", hash, outputDir: outputDir);

                // Export tables to script and manifest
                var views = db.Views.Cast<View>()
                    .Where(vw => !vw.IsSystemObject)
                    .Select(n => n.Urn).ToArray();

                // Export views to script and manifest
                scp.FilterCallbackFunction = FilterViewsOnly;
                scp.ExportFilesToScriptAndManifest(views, "ViewManifest.txt", hash, outputDir: outputDir);

                
                Commands.Stage(repo, "*");

                Signature author = new Signature("Felipe Correa", "felipe.correa@gmail.com", DateTime.Now);
                Signature committer = author;

                var committed = repo.Commit(string.Format("Adds {0} ", branchName), author, committer);

                
                Commands.Checkout(repo, master);
                var result = repo.Merge(branch, author);

                repo.Branches.Remove(branch);
            }
        }
        
        private static string GetCurrentBranchName()
        {
            var now = DateTime.Now;
            return string.Format("BRANCH{0}{1}{2}{3}{4}{5}", now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        }        

        private static bool FilterViewsOnly(Urn urn)
        {
            return !urn.Type.Equals("View");
        }

        private static bool FilterTablesOnly(Urn urn)
        {
            return !urn.Type.Equals("Table");
        }
    }
}
