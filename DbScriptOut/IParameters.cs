using System.Collections.Generic;

namespace DbScriptOut
{
    internal interface IParameters
    {
        Dictionary<string, string> GetParameters();
        void Parse(string[] args);
    }
}