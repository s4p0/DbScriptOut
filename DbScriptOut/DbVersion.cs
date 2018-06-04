using System;
using System.Linq;
using LibGit2Sharp;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using System.Collections.Generic;

namespace DbScriptOut
{
    internal class DbVersioning
    {
        private ParametersParser parameters;
        private readonly string directory;
        private ExportType exporting;

        public ServerConnection DbConnection { get; private set; }
        public Server DbServer { get; private set; }
        public Database DbName { get; private set; }
        public Scripter DbScripter { get; private set; }
        public string ManifestFileName { get; private set; }
        

        public DbVersioning(ParametersParser parameters)
        {
            this.parameters = parameters;
            directory = System.IO.Path.Combine(Environment.CurrentDirectory, parameters["Folder"]);
        }


        internal DbVersioning Connect()
        {
            DbConnection = new Microsoft.SqlServer.Management.Common.ServerConnection(parameters["DataSource"], parameters["User"], parameters["Password"]);

            // server
            DbServer = new Microsoft.SqlServer.Management.Smo.Server(DbConnection);

            // database
            DbName = DbServer.Databases[parameters["Database"]];

            DbScripter = new Scripter(DbServer);

            return this;
        }

        internal DbVersioning Export()
        {
            if (parameters["Tables"].Equals("1"))
            {
                ManifestFileName = "TableManifest.txt";
                Export(ExportType.TABLES, null);
            }


            if (parameters["Views"].Equals("1"))
            {
                ManifestFileName = "ViewManifest.txt";
                Export(ExportType.VIEWS, null);
            }

            if (parameters["Globalization"].Equals("1"))
            {
                ManifestFileName = "DataManifest.txt";
                Export(ExportType.DATA, new[] { "SAVGLOBALIZATION", "SAVGLOBALIZATION_VALUES" });
            }

            return this;
        }

        private void Export(ExportType exportType, string[] objectNames)
        {
            exporting = exportType;
            if (exportType == ExportType.DATA)
            {
                DbScripter.FilterCallbackFunction = null;
                ExportData(objectNames);
            }
            else
            {
                ExportObjects(exportType);
            }
        }

        private void ExportData(string[] objectNames)
        {
            DbScripter.Options.ScriptSchema = false;
            DbScripter.Options.ScriptData = true;
            var urns = from table in objectNames
                       select DbName.Tables[table].Urn;
            DbScripter.FilterCallbackFunction = null;
            ExportFilesToScriptAndManifest(urns.ToArray(), null);
        }

        private void ExportObjects(ExportType type) 
        {
            DbScripter.Options.ScriptSchema = true;
            DbScripter.Options.ScriptData = false;
            DbScripter.FilterCallbackFunction = FilterByExportType;

            Urn[] urns = null;
            // prepare tables to export
            if (exporting == ExportType.TABLES)
                urns = DbName.Tables.Cast<Table>()
                .Where(tbl => !tbl.IsSystemObject)
                .Select(n => n.Urn).ToArray();
            else if (exporting == ExportType.VIEWS)
                urns = DbName.Views.Cast<View>()
                .Where(tbl => !tbl.IsSystemObject)
                .Select(n => n.Urn).ToArray();
            // Export it as manifest
            HashSet<Urn> hash = new HashSet<Urn>();
            ExportFilesToScriptAndManifest(urns.ToArray(), hash);
        }

        internal DbVersioning ShowDifference()
        {
            return this;
        }

        internal DbVersioning Commit()
        {
            return this;
        }

        internal DbVersioning Setup(ScriptingOptions options)
        {
            if (options != null)
                DbScripter.Options = options;
            else
            {
                #region Scripter Options
                DbScripter.Options.NoCollation = true;
                DbScripter.Options.NoCommandTerminator = true;
                // only schema
                DbScripter.Options.ScriptSchema = true;
                DbScripter.Options.ScriptData = false;
                // no GO's
                DbScripter.Options.NoCommandTerminator = false;
                // without output stream (all objects at once)
                DbScripter.Options.ToFileOnly = true;
                // objects defaults
                DbScripter.Options.AllowSystemObjects = false;
                DbScripter.Options.Permissions = true;
                DbScripter.Options.SchemaQualify = true;
                DbScripter.Options.AnsiFile = true;
                DbScripter.Options.AnsiPadding = false;

                DbScripter.Options.SchemaQualifyForeignKeysReferences = true;
                DbScripter.Options.DriAllConstraints = true;
                DbScripter.Options.DriIndexes = true;
                DbScripter.Options.DriClustered = true;
                DbScripter.Options.DriNonClustered = true;
                DbScripter.Options.Indexes = true;
                DbScripter.Options.NonClusteredIndexes = true;
                DbScripter.Options.ClusteredIndexes = true;
                DbScripter.Options.FullTextIndexes = true;

                DbScripter.Options.EnforceScriptingOptions = true;
                /// case of our tests
                DbScripter.Options.WithDependencies = false;
                #endregion
            }

            return this;
        }

        private void ExportFilesToScriptAndManifest(Urn[] objects, HashSet<Urn> hash = null)
        {
            const string fileNameFormat = "{0}_{1}.SQL";
            using (var file = System.IO.File.CreateText(System.IO.Path.Combine(directory, ManifestFileName)))
            {
                foreach (var item in DbScripter.GetObjectsInDepedencyOrder(objects))
                {
                    if (hash == null || !hash.Contains(item))
                    {
                        if (hash != null) hash.Add(item);
                        string objName = null;

                        var objEntity = DbScripter.Server.GetSmoObject(item);
                        if (objEntity as View != null)
                            objName = (objEntity as View).Name;
                        else if (objEntity as Table != null)
                            objName = (objEntity as Table).Name;
                        else
                            throw new Exception(string.Format("Urn Type Not Expected: {0}", objEntity));

                        if (objName != null)
                        {
                            var onlyData = DbScripter.Options.ScriptData;
                            var fileName = string.Format(fileNameFormat, onlyData ? "Data" : item.Type, objName);
                            file.WriteLine(fileName);
                            DbScripter.Options.FileName = System.IO.Path.Combine(directory, fileName);
                            Console.WriteLine($"Exporting... {fileName}");
                            DbScripter.EnumScript(new[] { item });
                        }
                    }
                }
            }
        }

        private bool FilterByExportType(Urn urn)
        {
            if (exporting == ExportType.VIEWS)
                return !urn.Type.Equals("View");
            else if (exporting == ExportType.TABLES)
                return !urn.Type.Equals("Table");
            return false;
        }
    }
}