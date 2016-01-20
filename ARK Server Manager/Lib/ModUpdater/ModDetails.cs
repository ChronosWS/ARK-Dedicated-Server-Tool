using System;
using System.Collections.Generic;

namespace ARK_Server_Manager.Lib
{
    public class ModDetails
    {
        public ModDetails()
        {
            Id = null;
            Name = null;
            MapNames = new List<String>();
            MetaInformation = new Dictionary<String, String>();
        }

        public String Id;
        public String Name;
        public List<String> MapNames;
        public Dictionary<String, String> MetaInformation;
    }
}
