using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ARK_Server_Manager.Lib
{
    public static class ModUtilities
    {
        public static Dictionary<String, ModDetails> GetAllModDetails(String serverPath)
        {
            if (String.IsNullOrWhiteSpace(serverPath) || !Directory.Exists(serverPath))
                return null;

            var serverModPath = Path.Combine(serverPath, Config.Default.ServerModsRelativePath);
            var result = new Dictionary<String, ModDetails>();

            foreach (var modFile in Directory.GetFiles(serverModPath, "*.mod"))
            {
                var modDetails = ReadModFile(modFile);
                if (modDetails != null)
                    result.Add(Path.GetFileNameWithoutExtension(modFile), modDetails);
            }

            return result;
        }

        public static ModDetails GetModDetails(String serverPath, String id)
        {
            if (String.IsNullOrWhiteSpace(serverPath) || String.IsNullOrWhiteSpace(id))
                return null;

            if (!Directory.Exists(serverPath))
                return null;

            var serverModFile = Path.Combine(serverPath, Config.Default.ServerModsRelativePath, $"{id}.mod");

            return ReadModFile(serverModFile);
        }

        private static ModDetails ReadModFile(String fileName)
        {
            var modDetails = new ModDetails();

            using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                var num1 = reader.ReadUInt64();
                modDetails.Id = num1.ToString();

                var readString1 = ReadUE4String(reader);
                modDetails.Name = ReadUE4String(reader);

                var count1 = reader.ReadInt32();
                for (var index = 0; index < count1; ++index)
                {
                    var readString2 = ReadUE4String(reader);

                    modDetails.MapNames.Add(readString2);
                }

                var num2 = reader.ReadUInt32();
                var num3 = reader.ReadInt32();
                var num4 = reader.ReadByte();

                var count2 = reader.ReadInt32();
                for (var index = 0; index < count2; ++index)
                {
                    var readString3 = ReadUE4String(reader);
                    var readString4 = ReadUE4String(reader);

                    modDetails.MetaInformation.Add(readString3, readString4);
                }
            }

            return modDetails;
        }

        private static String ReadUE4String(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var flag = false;

            if (length < 0)
            {
                flag = true;
                length = -length;
            }

            if (flag || length <= 0)
                return String.Empty;

            var bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length - 1);
        }
    }
}
