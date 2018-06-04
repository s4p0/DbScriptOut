using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbScriptOut
{
    public static class ScripterHelper
    {

        public static UrnCollection GetObjectsInDepedencyOrder(this Scripter sc, Urn[] objects)
        {
            var result = new UrnCollection();
            var tree = sc.DiscoverDependencies(objects, true);
            var walk = sc.WalkDependencies(tree);
            foreach (var item in walk)
                result.Add(item.Urn);

            return result;
        }

        public static void ExportFilesToScriptAndManifest(this Scripter scp, Urn[] objects, string manifestName, HashSet<Urn> hash = null, string outputDir = "output")
        {
            const string fileNameFormat = "{0}_{1}.SQL";
            using (var file = System.IO.File.CreateText(System.IO.Path.Combine(outputDir, manifestName)))
            {
                foreach (var item in scp.GetObjectsInDepedencyOrder(objects))
                {
                    if (hash == null || !hash.Contains(item))
                    {
                        if (hash != null) hash.Add(item);
                        string objName = null;

                        var objEntity = scp.Server.GetSmoObject(item);
                        if (objEntity as View != null)
                            objName = (objEntity as View).Name;
                        else if (objEntity as Table != null)
                            objName = (objEntity as Table).Name;
                        else
                            throw new Exception(string.Format("Urn Type Not Expected: {0}", objEntity));

                        if (objName != null)
                        {
                            var onlyData = scp.Options.ScriptData;
                            var fileName = string.Format(fileNameFormat, onlyData ? "DATA" : item.Type, objName);
                            file.WriteLine(fileName);
                            scp.Options.FileName = System.IO.Path.Combine(outputDir, fileName);
                            scp.EnumScript(new[] { item });
                        }
                    }
                }
            }

        }


    }
}
