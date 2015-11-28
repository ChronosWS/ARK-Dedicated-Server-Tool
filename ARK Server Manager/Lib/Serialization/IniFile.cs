using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARK_Server_Manager.Lib.Serialization
{
    public class IniSection
    {
        public IniSection(string sectionName)
        {
            this.SectionName = sectionName;
            this.Entries = new List<IniEntry>();
            this.Fields = new Dictionary<string, List<IniFieldEntry>>();
        }

        public class IniFieldEntry
        {
            public int Order
            {
                get;
                set;
            }

            public IniEntry Entry
            {
                get;
                set;
            }
        }

        public string SectionName
        {
            get;
            set;
        }

        public List<IniEntry> Entries
        {
            get;
            set;
        }

        public Dictionary<string, List<IniFieldEntry>> Fields
        {
            get;
            set;
        }

        internal static IEnumerable<IniSection> ReadAllSections(IEnumerator<IniFileStream.Line> iniLines, IniDefinition definition)
        {
            IniSection currentSection = null;
            int ordinal = 0;
            do
            {
                switch (iniLines.Current.Type)
                {
                    case IniFileStream.LineType.SectionHeader:
                        if(currentSection != null)
                        {
                            yield return currentSection;
                        }

                        currentSection = new IniSection(iniLines.Current.Value);
                        break;

                    default:
                        var baseEntry = IniEntry.FromLine(iniLines.Current);
                        currentSection.Entries.Add(baseEntry);

                        if (!baseEntry.IsBlank && !baseEntry.IsComment)
                        {
                            IniSectionDefinition sectionDefinition;
                            definition.Sections.TryGetValue(currentSection.SectionName, out sectionDefinition);
                            var fieldEntry = new IniFieldEntry { Order = ordinal++, Entry = GetFieldEntry(sectionDefinition, baseEntry) };

                            List<IniFieldEntry> fieldEntries;
                            if (!currentSection.Fields.TryGetValue(baseEntry.Key, out fieldEntries))
                            {
                                fieldEntries = new List<IniFieldEntry>();
                                currentSection.Fields[baseEntry.Key] = fieldEntries;
                            }

                            fieldEntries.Add(fieldEntry);
                        }
                        break;
                }
            } while (iniLines.MoveNext());

            yield return currentSection;
        }

        private static IniEntry GetFieldEntry(IniSectionDefinition definition, IniEntry baseEntry)
        {
            IniFieldDefinition fieldDefinition;
            if (definition != null &&
                definition.Fields.TryGetValue(baseEntry.Key, out fieldDefinition) && 
                fieldDefinition.IsCompound)
            {
                return CompoundIniEntry.FromIniEntry(baseEntry);
            }

            return baseEntry;            
        }
    }

    public class IniFile
    {
        public IniDefinition Definition
        {
            get;
            set;
        }

        public List<IniFileStream.Line> HeaderLines
        {
            get;
            set;
        }
        public List<IniSection> Sections
        {
            get;
            set;
        }

        public static IniFile ReadFromFile(IniDefinition definition, string file)
        {
            var iniFileStream = IniFileStream.FromFile(file);
            var lines = iniFileStream.GetLines().GetEnumerator();
            var result = new IniFile()
            {
                HeaderLines = new List<IniFileStream.Line>(),
                Definition = definition,
                Sections = new List<IniSection>()
            };

            //
            // Read any header comments
            //
            bool hasElement = lines.MoveNext();
            while(hasElement && lines.Current.Type != IniFileStream.LineType.SectionHeader)
            {
                result.HeaderLines.Add(lines.Current);
                hasElement = lines.MoveNext();
            }

            if (hasElement)
            {
                result.Sections.AddRange(IniSection.ReadAllSections(lines, definition));
            }

            return result;
        }

        public void WriteToFile(string file)
        {

        }
    }
    
    public class IniFileStream
    {
        public enum LineType
        {
            SectionHeader,
            Value,
            Blank,
            Comment,
        };

        public struct Line
        {
            public LineType Type
            {
                get;
                set;
            }

            public string Value
            {
                get;
                set;
            }
        }

        private StreamReader stream;

        public IEnumerable<Line> GetLines()
        {
            using (stream)
            {
                while (!stream.EndOfStream)
                {
                    var line = stream.ReadLine().Trim();
                    if (String.IsNullOrWhiteSpace(line))
                    {
                        yield return new Line { Type = LineType.Blank };
                    }
                    else if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        yield return new Line { Type = LineType.SectionHeader, Value = line };
                    }
                    else if (line.StartsWith("#"))
                    {
                        yield return new Line { Type = LineType.Comment, Value = line };
                    }
                    else
                    {
                        yield return new Line { Type = LineType.Value, Value = line };
                    }
                }
            }
        }

        public static IniFileStream FromFile(string file)
        {
            var stream = File.OpenText(file);
            return new IniFileStream { stream = stream };
        }
    }
}
