using System;
using System.Linq;
using System.Collections.Generic;

namespace DbScriptOut
{
    internal class ParametersParser : IParameters
    {
        private string[] args;
        private Dictionary<string, string> parameter = new Dictionary<string, string>();

        public string this[string index] => parameter[index]; 

        public ParametersParser(string[] args)
        {
            this.args = args;
            parameter = DefaultParameters();
            Parse(args);
        }

        public bool IsMissingMandatory
        {
            get
            {
                return parameter["DataSource"] == null
                 || parameter["Database"] == null
                 || parameter["User"] == null
                 || parameter["Password"] == null;
            }
        }
        public string CommandLineUse
        {
            get
            {
                var name = System.AppDomain.CurrentDomain.FriendlyName;
                var parameters = string.Join(" ", parameter.Select(n => $"--{n.Key}=[{n.Value ?? string.Empty}] "));
                return $"{name} {parameters}";
            }
        }

        public static Dictionary<string, string> DefaultParameters()
        {
            var parameters = new Dictionary<string, string>();
            parameters.Add("DataSource", null);
            parameters.Add("Database", null);
            parameters.Add("User", null);
            parameters.Add("Password", null);
            parameters.Add("Tables", "1");
            parameters.Add("Views", "1");
            parameters.Add("Globalization", "1");
            parameters.Add("ExportOnly", "0");
            parameters.Add("Folder", ".");
            return parameters;
        }

        public Dictionary<string, string> GetParameters()
        {
            throw new NotImplementedException();
        }

        public void Parse(string[] args)
        {
            var splitter = new[] { '=' };
            foreach (var item in args.Where(n=>n.StartsWith("--")))
            {
                var parsed = ParseItem(item.Replace("\r\n", "").Replace("--", string.Empty));
                if (!parameter.ContainsKey(parsed.Key))
                    parameter.Add(parsed.Key, parsed.Value);
                else
                {
                    parameter[parsed.Key] = parsed.Value;
                }
            }
        }

        internal KeyValuePair<string, string> ParseItem(string item)
        {

            var splitter = new[] { '=' };
            var keyValue = item.Split(splitter, StringSplitOptions.RemoveEmptyEntries);
            return new KeyValuePair<string, string>(keyValue[0], keyValue[1]);
        }
    }
}