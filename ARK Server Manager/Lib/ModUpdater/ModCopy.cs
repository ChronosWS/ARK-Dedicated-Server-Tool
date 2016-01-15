using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ionic.Zlib;

namespace ARK_Server_Manager.Lib
{
    public static class ModCopy
    {
        private class FCompressedChunkInfo
        {
            public const uint LOADING_COMPRESSION_CHUNK_SIZE = 131072U;
            public const uint PACKAGE_FILE_TAG = 2653586369U;
            public const uint PACKAGE_FILE_TAG_SWAPPED = 3246598814U;

            public long CompressedSize;
            public long UncompressedSize;

            public void Serialize(BinaryReader reader)
            {
                CompressedSize = reader.ReadInt64();
                UncompressedSize = reader.ReadInt64();
            }
        }

        public static bool InstallMod(string steamCmdPath, string serverPath, string[] ids)
        {
            if (string.IsNullOrWhiteSpace(steamCmdPath) || string.IsNullOrWhiteSpace(serverPath) || ids == null || ids.Length == 0)
                return false;

            if (!Directory.Exists(steamCmdPath))
                return false;

            try
            {
                foreach (string id in ids)
                {
                    string workshopModPath = Path.Combine(steamCmdPath, @"steamapps\workshop\content\346110\", id);

                    if (Directory.Exists(workshopModPath))
                    {
                        string fileName1 = Path.Combine(workshopModPath, "mod.info");
                        List <string> list = new List<string>();
                        ParseBaseInformation(fileName1, list);

                        string fileName2 = Path.Combine(workshopModPath, "modmeta.info");
                        Dictionary<string, string> metaInformation = new Dictionary<string, string>();
                        if (ParseMetaInformation(fileName2, metaInformation))
                            workshopModPath = Path.Combine(workshopModPath, "WindowsNoEditor");

                        string serverModPath = Path.Combine(serverPath, @"ShooterGame\Content\Mods\", id);
                        bool flag = false;

                        foreach (string workshopModFile in Directory.GetFiles(workshopModPath, "*.*", SearchOption.AllDirectories))
                        {
                            string serverModFile = workshopModFile.Replace(workshopModPath, serverModPath);
                            string serverModFilePath = Path.GetDirectoryName(serverModFile);

                            if (!Directory.Exists(serverModFilePath))
                                Directory.CreateDirectory(serverModFilePath);

                            string serverModFileExtension = Path.GetExtension(workshopModFile).ToUpper();
                            if (Path.GetFileNameWithoutExtension(workshopModFile).Contains("PrimalGameData"))
                                flag = true;

                            if (string.Compare(serverModFileExtension, ".uncompressed_size", StringComparison.OrdinalIgnoreCase) != 0)
                            {
                                if (string.Compare(serverModFileExtension, ".z", StringComparison.OrdinalIgnoreCase) == 0)
                                    UE4ChunkUnzip(workshopModFile, serverModFile.Substring(0, serverModFile.Length - 2));
                                else
                                    File.Copy(workshopModFile, serverModFile, true);
                            }
                        }

                        if (metaInformation.Count == 0 && flag)
                            metaInformation["ModType"] = "1";

                        WriteModFile($"{serverModPath}.mod", id, metaInformation, list);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void GetModDetails(string serverPath, string id, out Dictionary<string, string> metaInformation, out List<string> mapNames)
        {
            metaInformation = null;
            mapNames = null;

            if (string.IsNullOrWhiteSpace(serverPath) || string.IsNullOrWhiteSpace(id))
                return;

            if (!Directory.Exists(serverPath))
                return;

            string serverModPath = Path.Combine(serverPath, @"ShooterGame\Content\Mods\", id);
            string modId;

            ReadModFile($"{serverModPath}.mod", out modId, out metaInformation, out mapNames);
        }

        private static bool ParseBaseInformation(string fileName, List<string> mapNames)
        {
            if (!File.Exists(fileName))
                return false;

            using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                string readString1;
                ReadUE4String(reader, out readString1);

                int num = reader.ReadInt32();
                for (int index = 0; index < num; ++index)
                {
                    string readString2;
                    ReadUE4String(reader, out readString2);
                    mapNames.Add(readString2);
                }
            }
            return true;
        }

        private static bool ParseMetaInformation(string fileName, Dictionary<string, string> metaInformation)
        {
            if (!File.Exists(fileName))
                return false;

            using (BinaryReader binaryReader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                int num = binaryReader.ReadInt32();
                for (int index1 = 0; index1 < num; ++index1)
                {
                    string index2 = string.Empty;
                    int count1 = binaryReader.ReadInt32();
                    bool flag1 = false;
                    if (count1 < 0)
                    {
                        flag1 = true;
                        count1 = -count1;
                    }
                    if (!flag1 && count1 > 0)
                    {
                        byte[] bytes = binaryReader.ReadBytes(count1);
                        index2 = Encoding.UTF8.GetString(bytes, 0, bytes.Length - 1);
                    }
                    string str = string.Empty;
                    int count2 = binaryReader.ReadInt32();
                    bool flag2 = false;
                    if (count2 < 0)
                    {
                        flag2 = true;
                        count2 = -count2;
                    }
                    if (!flag2 && count2 > 0)
                    {
                        byte[] bytes = binaryReader.ReadBytes(count2);
                        str = Encoding.UTF8.GetString(bytes, 0, bytes.Length - 1);
                    }
                    metaInformation[index2] = str;
                }
            }
            return true;
        }

        private static void ReadModFile(string fileName, out string modID, out Dictionary<string, string> metaInformation, out List<string> mapNames)
        {
            modID = null;
            metaInformation = new Dictionary<string, string>();
            mapNames = new List<string>();

            using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                ulong num1 = reader.ReadUInt64();
                modID = num1.ToString();

                string readString1;
                ReadUE4String(reader, out readString1);
                string readString2;
                ReadUE4String(reader, out readString2);

                int count1 = reader.ReadInt32();
                for (int index = 0; index < count1; ++index)
                {
                    string readString3;
                    ReadUE4String(reader, out readString3);
                    mapNames.Add(readString3);
                }

                uint num2 = reader.ReadUInt32();
                int num3 = reader.ReadInt32();
                byte num4 = reader.ReadByte();

                int count2 = reader.ReadInt32();
                for (int index = 0; index < count2; ++index)
                {
                    string readString4;
                    ReadUE4String(reader, out readString4);
                    string readString5;
                    ReadUE4String(reader, out readString5);
                    metaInformation.Add(readString4, readString5);
                }
            }
        }

        private static void ReadUE4String(BinaryReader reader, out string readString)
        {
            readString = string.Empty;
            int count = reader.ReadInt32();
            bool flag = false;
            if (count < 0)
            {
                flag = true;
                count = -count;
            }
            if (flag || count <= 0)
                return;
            byte[] bytes = reader.ReadBytes(count);
            readString = Encoding.UTF8.GetString(bytes, 0, bytes.Length - 1);
        }

        private static void UE4ChunkUnzip(string source, string destination)
        {
            using (BinaryReader inReader = new BinaryReader(File.Open(source, FileMode.Open)))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(destination, FileMode.Create)))
                {
                    FCompressedChunkInfo fcompressedChunkInfo1 = new FCompressedChunkInfo();
                    fcompressedChunkInfo1.Serialize(inReader);
                    FCompressedChunkInfo fcompressedChunkInfo2 = new FCompressedChunkInfo();
                    fcompressedChunkInfo2.Serialize(inReader);

                    long num1 = fcompressedChunkInfo1.CompressedSize;
                    long num2 = fcompressedChunkInfo1.UncompressedSize;
                    if (num2 == 2653586369L)
                        num2 = 131072L;
                    long length = (fcompressedChunkInfo2.UncompressedSize + num2 - 1L) / num2;

                    FCompressedChunkInfo[] fcompressedChunkInfoArray = new FCompressedChunkInfo[length];
                    long val2 = 0L;

                    for (int index = 0; index < length; ++index)
                    {
                        fcompressedChunkInfoArray[index] = new FCompressedChunkInfo();
                        fcompressedChunkInfoArray[index].Serialize(inReader);
                        val2 = Math.Max(fcompressedChunkInfoArray[index].CompressedSize, val2);
                    }

                    for (long index = 0L; index < length; ++index)
                    {
                        FCompressedChunkInfo fcompressedChunkInfo3 = fcompressedChunkInfoArray[index];
                        byte[] buffer = ZlibStream.UncompressBuffer(inReader.ReadBytes((int)fcompressedChunkInfo3.CompressedSize));
                        binaryWriter.Write(buffer);
                    }
                }
            }
        }

        private static void WriteModFile(string fileName, string modID, Dictionary<string, string> metaInformation, List<string> mapNames)
        {
            using (BinaryWriter outWriter = new BinaryWriter(File.Open(fileName, FileMode.Create)))
            {
                ulong num1 = ulong.Parse(modID);
                outWriter.Write(num1);
                WriteUE4String("ModName", outWriter);
                WriteUE4String(string.Empty, outWriter);
                int count1 = mapNames.Count;
                outWriter.Write(count1);
                for (int index = 0; index < mapNames.Count; ++index)
                {
                    WriteUE4String(mapNames[index], outWriter);
                }
                uint num2 = 4280483635U;
                outWriter.Write(num2);
                int num3 = 2;
                outWriter.Write(num3);
                byte num4 = metaInformation.ContainsKey("ModType") ? (byte)1 : (byte)0;
                outWriter.Write(num4);
                int count2 = metaInformation.Count;
                outWriter.Write(count2);
                foreach (KeyValuePair<string, string> keyValuePair in metaInformation)
                {
                    WriteUE4String(keyValuePair.Key, outWriter);
                    WriteUE4String(keyValuePair.Value, outWriter);
                }
            }
        }

        private static void WriteUE4String(string writeString, BinaryWriter writer)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(writeString);
            int num1 = bytes.Length + 1;
            writer.Write(num1);
            writer.Write(bytes);
            byte num2 = 0;
            writer.Write(num2);
        }
    }
}
