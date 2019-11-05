using System;
using System.Collections.Generic;

namespace Common
{
    public static class ShowAliases
    {
        public static string RenameByAlias(string name)
        {
            var _showAliases = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("The Americans","The Americans 2013"),
                new KeyValuePair<string, string>("Crashing","Crashing 2017"),
                new KeyValuePair<string, string>("Empire","Empire 2015"),
                new KeyValuePair<string, string>("House of Cards","House of Cards 2013"),
                new KeyValuePair<string, string>("Jack Ryan","Tom Clancys Jack Ryan"),
                new KeyValuePair<string, string>("Naked and Marooned with Ed Stafford","Marooned with Ed Stafford"),
                new KeyValuePair<string, string>("Marooned With Ed Stafford","Marooned with Ed Stafford"),
                new KeyValuePair<string, string>("Penn & Teller Bullshit","Penn and Teller Bullshit"),
                new KeyValuePair<string, string>("Penn and Teller Fool Us","Penn & Teller Fool Us"),
                new KeyValuePair<string, string>("Shameless","Shameless US"),
                new KeyValuePair<string, string>("Travelers","Travelers 2016"),
            };

            foreach (var alias in _showAliases)
            {
                var aliasFrom = new System.Text.RegularExpressions.Regex(alias.Key.Replace(" ", @"[\s\.]"));
                var aliasTo = new System.Text.RegularExpressions.Regex(alias.Value.Replace(" ", @"[\s\.]"));
                if (!aliasTo.IsMatch(name) && aliasFrom.IsMatch(name))
                {
                    name = aliasFrom.Replace(name, alias.Value);
                    return name;
                }
            }

            return name;
        }
    }
}
