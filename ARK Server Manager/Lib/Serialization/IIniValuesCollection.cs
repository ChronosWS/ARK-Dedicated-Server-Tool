using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib
{
    public interface IIniValuesCollection
    {
        IEnumerable<string> ToIniValues();
        void FromIniValues(IEnumerable<string> values);
        bool IsEnabled { get; set; }
        string IniCollectionKey { get; }
    }
}
