using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using AudioSynthesis.Bank;
using AudioSynthesis.Wave;
using AudioSynthesis.Bank.Descriptors;
using AudioSynthesis.Util;
using AudioSynthesis.Bank.Components;
using AudioSynthesis.Sfz;
using AudioSynthesis.Util.Riff;
using System.Diagnostics;

namespace BankBuilder
{
    public class BankBuilder
    {
        //sub classes
        private class PatchInterval
        {
            public short Bank;
            public byte Start;
            public byte End;

            public PatchInterval(short bank, byte start, byte end)
            {
                Bank = bank;
                Start = start;
                End = end;
            }
            public bool WithinRange(PatchInterval pInfo)
            {
                return (pInfo.Bank == Bank) && ((pInfo.Start <= End && pInfo.Start >= Start) || (pInfo.End <= End && pInfo.End >= Start));
            }
        }
        private class PatchInfo
        {
            public int Size;
            public string Name;
            public string Type;
            public List<PatchInterval> Ranges = new List<PatchInterval>();
            public DescriptorList Description;

            public PatchInfo(string name, PatchInterval pInterval)
            {
                Name = name;
                Ranges.Add(pInterval);
            }
            public override string ToString()
            {
                return string.Format("{0}, {1}", Name, Type);
            }
        }
        private class SampleAsset
        {
            public string Name;
            public byte Channels;
            public byte Bits;
            public int SampleRate;
            public short RootKey = 60;
            public short Tune = 0;
            public double LoopStart = -1;
            public double LoopEnd = -1;
            public byte[] Data;

            public SampleAsset(string name, WaveFile wf)
            {
                Name = name;
                Channels = (byte)wf.Format.ChannelCount;
                Bits = (byte)wf.Format.BitsPerSample;

                SamplerChunk smpl = wf.FindChunk<SamplerChunk>();
                if (smpl != null)
                {
                    SampleRate = (int)(44100.0 * (1.0 / (smpl.SamplePeriod / 22675.0)));
                    RootKey = (short)smpl.UnityNote;
                    Tune = (short)(smpl.PitchFraction * 100);
                    if (smpl.Loops.Length > 0)
                    {
                        if (smpl.Loops[0].Type != SamplerChunk.SampleLoop.LoopType.Forward)
                            Console.WriteLine("Warning: Loopmode was not supported on asset: " + Name);
                        LoopStart = smpl.Loops[0].Start;
                        LoopEnd = smpl.Loops[0].End + smpl.Loops[0].Fraction + 1;
                    }
                }
                else
                {
                    SampleRate = wf.Format.SampleRate;
                }


                SampleRate = wf.Format.SampleRate;
                Data = wf.Data.RawSampleData;
            }
        }
        //fields
        private static string patchPath, assetPath, comments;
        private static List<PatchInfo> patches = new List<PatchInfo>();
        private static List<PatchInfo> multiPatches = new List<PatchInfo>();
        private static List<SampleAsset> assets = new List<SampleAsset>();
        //methods
        public static bool BuildBankFile(string inputFileName, string outputFileName)
        {
            if (!File.Exists(inputFileName)) 
            { 
                Console.WriteLine("The input file can not be found."); 
                return false; 
            }
            if (outputFileName.Trim().Equals(string.Empty)) 
                outputFileName = Path.ChangeExtension(inputFileName, ".bank");
            else if (Path.GetFileName(outputFileName).Trim().Equals(string.Empty))
                outputFileName += Path.GetFileNameWithoutExtension(inputFileName) + ".bank";
            else if (!Path.HasExtension(outputFileName))
                outputFileName += ".bank";
            if (!ParseTextBank(inputFileName))
            {
                Console.WriteLine("An error occured while opening/reading the input bank.");
                return false;
            }
            DeterminePaths(inputFileName);
            CheckIfPatchesExist();
            LoadPatchData();
            Console.WriteLine(string.Format("Loaded {0} patches.", patches.Count + multiPatches.Count));
            LoadAssetData();
            Console.WriteLine(string.Format("Loaded {0} assets.", assets.Count));
            CreateBank(outputFileName);
            //clean up
            patchPath = "";
            assetPath = "";
            comments = "";
            patches.Clear(); 
            multiPatches.Clear(); 
            assets.Clear();
            Console.WriteLine("Created Bank: " + Path.GetFileName(outputFileName));
            return true;
        }
        private static bool ParseTextBank(string inputFileName)
        {
            try 
            {
                string[] strPatches;
                using (StreamReader reader = new StreamReader(inputFileName))
                {
                    if(!reader.ReadLine().Trim().Equals("[PATCHBANK]"))
                    {
                        Console.WriteLine("The text bank header was not correct.");
                        throw new InvalidDataException("The input file is not a valid patch bank.");
                    }
                    comments = ReadTag(reader, "comment").Trim();
                    patchPath = ReadTag(reader, "patchpath").Trim();
                    assetPath = ReadTag(reader, "assetpath").Trim();
                    strPatches = ReadTag(reader, "patches").Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                }
                for (int x = 0; x < strPatches.Length; x++)
                {
                    string[] args = strPatches[x].Split(new char[] { '/' }, StringSplitOptions.None);
                    short bank;
                    if (args[3][0] == 'i')
                        bank = 0;
                    else if (args[3][0] == 'd')
                        bank = PatchBank.DrumBank;
                    else
                        bank = short.Parse(args[3]);
                    PatchInfo pInfo = new PatchInfo(args[0], new PatchInterval(bank, byte.Parse(args[1]), byte.Parse(args[2])));
                    AddPatchInfo(pInfo);
                }
                return true;
            } 
            catch (Exception ex) 
            {
                Debug.WriteLine(ex.Message);
            }
            return false;
        }
        private static void AddPatchInfo(PatchInfo pInfo)
        {
            foreach (PatchInfo p in patches)
            {
                int containIndex = -1;
                for(int x = 0; x < p.Ranges.Count; x++)
                {
                    if(p.Ranges[x].WithinRange(pInfo.Ranges[0]))
                    {
                        containIndex = x; 
                        break;
                    }
                }
                if (pInfo.Name.Equals(p.Name))
                {
                    if (containIndex >= 0) //the patch exists and its range is an overlap so we merge on the overlap
                    {
                        p.Ranges[containIndex].Start = Math.Min(p.Ranges[containIndex].Start, pInfo.Ranges[0].Start);
                        p.Ranges[containIndex].End = Math.Max(p.Ranges[containIndex].End, pInfo.Ranges[0].End);
                    }
                    else//the patch already exists, but the ranges are different so we add its range the the existing one
                        p.Ranges.Add(pInfo.Ranges[0]);
                    return;
                }
                else if (containIndex >= 0)
                {
                    throw new Exception(string.Format("Patches {0} and {1} have overlapping assignments.", p.Name, pInfo.Name));
                }
            }
            patches.Add(pInfo);
        }
        private static string ReadTag(StreamReader reader, string expectedTag)
        {
            StringBuilder sbuild = new StringBuilder();
            int i = reader.Read();
            while (i >= 0 && i != '<')
            {
                i = reader.Read();
            }
            i = reader.Read();
            while (i >= 0 && i != '>')
            {
                sbuild.Append((char)i);
                i = reader.Read();
            }
            string tagName = sbuild.ToString().ToLower();
            if (!tagName.Equals(expectedTag))
                throw new Exception("Expected to find tag: " + expectedTag + ", but found tag: " + tagName);
            sbuild.Clear();
            i = reader.Read();
            while (i >= 0 && i != '<')
            {
                sbuild.Append((char)i);
                i = reader.Read();
            }
            string description = sbuild.ToString();
            sbuild.Clear();
            i = reader.Read();
            while (i >= 0 && i != '>')
            {
                sbuild.Append((char)i);
                i = reader.Read();
            }
            string endTagName = sbuild.ToString().ToLower();

            if (!endTagName.StartsWith("/") || !endTagName.Remove(0, 1).Equals(tagName))
                throw new Exception("Invalid tag: <" + tagName + ">.");
            return description;
        }
        private static void DeterminePaths(string patchBankFileName)
        {
            //determine patch patch
            string pp = Path.GetDirectoryName(patchBankFileName);
            if (!pp.Equals(string.Empty))
            {
                if (patchPath.Equals(string.Empty))
                    patchPath = pp + Path.DirectorySeparatorChar;
                else if (!Path.IsPathRooted(patchPath))
                    patchPath = pp + Path.DirectorySeparatorChar + patchPath;
            }
            patchPath = patchPath.Trim();
            if (patchPath.Length > 0)
            {
                if (!EndsWithDirectorySymbol(patchPath))
                    patchPath += Path.DirectorySeparatorChar;
            }
            //determine asset path
            string ap = Path.GetDirectoryName(patchBankFileName);
            if (!ap.Equals(string.Empty))
            {
                if (assetPath.Equals(string.Empty))
                    assetPath = ap + Path.DirectorySeparatorChar;
                else if (!Path.IsPathRooted(assetPath))
                    assetPath = ap + Path.DirectorySeparatorChar + assetPath;
            }
            assetPath = assetPath.Trim();
            if (assetPath.Length > 0)
            {
                if (!EndsWithDirectorySymbol(assetPath))
                    assetPath += Path.DirectorySeparatorChar;
            }
        }
        private static bool EndsWithDirectorySymbol(string path)
        {
            char c = path[path.Length - 1];
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }
        private static bool CheckIfPatchesExist()
        {
            for (int x = 0; x < patches.Count; x++)
            {
                if (!File.Exists(patchPath + patches[x].Name))
                {
                    Console.WriteLine(string.Format("Can not find the patch: {0}", patchPath + patches[x].Name));
                    return false;
                }
            }
            return true;
        }
        private static int LoadPatch(PatchInfo pInfo)
        {
            using (StreamReader reader = new StreamReader(patchPath + pInfo.Name))
            {
                string str = reader.ReadLine();
                if (PatchBank.BankVersion != float.Parse(str.Substring(str.IndexOf("v") + 1)))
                    throw new Exception("The patch " + pInfo.Name + " has an incorrect version.");
                pInfo.Type = reader.ReadLine().Trim().ToLower();
                pInfo.Description = new DescriptorList(reader);
            }
            if (pInfo.Type.Equals("multi"))
            {
                patches.Remove(pInfo);
                multiPatches.Add(pInfo);
                //load sub patches
                for (int i = 0; i < pInfo.Description.CustomDescriptions.Length; i++)
                {
                    if (!pInfo.Description.CustomDescriptions[i].ID.Equals("mpat"))
                        throw new Exception("Invalid multi patch: " + pInfo.Name);
                    string subPatchName = (string)pInfo.Description.CustomDescriptions[i].Objects[0];
                    if (ContainsPatch(subPatchName, patches) == false)
                    {
                        PatchInfo subPatch = new PatchInfo(subPatchName, new PatchInterval(-1, 0, 0));
                        patches.Add(subPatch);
                    }
                }
                return -1;
            }
            return 0;
        }
        private static int LoadSfz(PatchInfo pInfo)
        {
            SfzReader sfz;
            using (FileStream fs = File.Open(patchPath + pInfo.Name, FileMode.Open, FileAccess.Read))
            {
                sfz = new SfzReader(fs, pInfo.Name);
            }
            PatchInfo[] pInfos = new PatchInfo[sfz.Regions.Length];
            int nameLen = sfz.Name.Length + 1 + sfz.Regions.Length.ToString().Length;
            if (nameLen > 20)
            {
                sfz.Name = sfz.Name.Remove(0, nameLen - 20);
            }
            for (int i = 0; i < pInfos.Length; i++)
            {
                pInfos[i] = new PatchInfo(sfz.Name + "_" + i, new PatchInterval(-1, 0, 0));
                pInfos[i].Type = "sfz ";
                pInfos[i].Description = new DescriptorList(sfz.Regions[i]);
            }
            DescriptorList multiDesc = new DescriptorList();
            multiDesc.CustomDescriptions = new CustomDescriptor[sfz.Regions.Length];
            for (int i = 0; i < multiDesc.CustomDescriptions.Length; i++)
            {
                SfzRegion r = sfz.Regions[i];
                multiDesc.CustomDescriptions[i] = new CustomDescriptor("mpat", pInfos[i].Name.Length + 14, new object[] { pInfos[i].Name, r.loChan, r.hiChan, r.loKey, r.hiKey, r.loVel, r.hiVel });
            }
            pInfo.Type = "mult";
            pInfo.Description = multiDesc;
            multiPatches.Add(pInfo);
            patches.Remove(pInfo);
            patches.InsertRange(0, pInfos);
            return pInfos.Length - 1;
        }
        private static void LoadPatchData()
        {
            for (int x = 0; x < patches.Count; x++)
            {
                switch (Path.GetExtension(patches[x].Name).ToLower())
                {
                    case ".patch":
                        x += LoadPatch(patches[x]);
                        break;
                    case ".sfz":
                        x += LoadSfz(patches[x]);
                        break;
                }
            }
        }
        private static void LoadAssetData()
        {
            for (int x = 0; x < patches.Count; x++)
            {
                for (int y = 0; y < patches[x].Description.GenDescriptions.Length; y++)
                {
                    GeneratorDescriptor genDesc = patches[x].Description.GenDescriptions[y];
                    string assetName = genDesc.AssetName;
                    string extension = Path.GetExtension(assetName).ToLower();
                    if (genDesc.SamplerType == WaveformEnum.SampleData && !assetName.Equals("null") && ContainsAsset(assetName, assets) == false)
                    {
                        switch (extension)
                        {
                            case ".wav":
                                using (WaveFileReader wr = new WaveFileReader(File.Open(assetPath + assetName, FileMode.Open, FileAccess.Read)))
                                {
                                    assets.Add(new SampleAsset(assetName, wr.ReadWaveFile()));
                                }
                                break;
                            default:
                                throw new Exception(string.Format("Unknown format ({0}), AssetName: {1}, PatchName: {2}", extension, assetName, patches[x].Name));
                        }
                    }
                }
            }
        }
        private static void CreateBank(string bankFileName)
        {
            int infoSize = 12 + comments.Length;
            int assetListSize = GetAssetListSize(assets);
            int patchListSize = GetPatchListSize(patches, multiPatches);
            using (BinaryWriter bw = new BinaryWriter(File.Create(bankFileName)))
            {
                IOHelper.Write8BitString(bw, "RIFF", 4);
                bw.Write((int)(4 + infoSize + assetListSize + patchListSize));
                IOHelper.Write8BitString(bw, "BANK", 4);
                IOHelper.Write8BitString(bw, "INFO", 4);
                bw.Write((int)(infoSize- 8));
                bw.Write(PatchBank.BankVersion);
                IOHelper.Write8BitString(bw, comments, comments.Length);
                IOHelper.Write8BitString(bw, "LIST", 4);
                bw.Write((int)(assetListSize - 8));
                IOHelper.Write8BitString(bw, "ASET", 4);
                for (int x = 0; x < assets.Count; x++)
                {
                    IOHelper.Write8BitString(bw, "SMPL", 4);
                    bw.Write((int)(46 + assets[x].Data.Length));
                    IOHelper.Write8BitString(bw, Path.GetFileNameWithoutExtension(assets[x].Name), 20);
                    bw.Write((int)assets[x].SampleRate);
                    bw.Write((short)assets[x].RootKey);
                    bw.Write((short)assets[x].Tune);
                    bw.Write((double)assets[x].LoopStart);
                    bw.Write((double)assets[x].LoopEnd);
                    bw.Write((byte)assets[x].Bits);
                    bw.Write((byte)assets[x].Channels);
                    bw.Write(assets[x].Data);
                }
                assets.Clear();
                IOHelper.Write8BitString(bw, "LIST", 4);
                bw.Write((int)(patchListSize - 8));
                IOHelper.Write8BitString(bw, "INST", 4);
                for (int x = 0; x < patches.Count; x++)
                {
                    IOHelper.Write8BitString(bw, "PTCH", 4);
                    bw.Write((int)(patches[x].Size));
                    IOHelper.Write8BitString(bw, Path.GetFileNameWithoutExtension(patches[x].Name), 20);
                    IOHelper.Write8BitString(bw, patches[x].Type.PadRight(4,' '), 4);
                    bw.Write((short)patches[x].Description.DescriptorCount);
                    patches[x].Description.Write(bw);
                    bw.Write((short)patches[x].Ranges.Count);
                    for (int i = 0; i < patches[x].Ranges.Count; i++)
                    {
                        bw.Write((short)patches[x].Ranges[i].Bank);
                        bw.Write((byte)patches[x].Ranges[i].Start);
                        bw.Write((byte)patches[x].Ranges[i].End);
                    }
                }
                for (int x = 0; x < multiPatches.Count; x++)
                {
                    IOHelper.Write8BitString(bw, "PTCH", 4);
                    bw.Write((int)(multiPatches[x].Size));
                    IOHelper.Write8BitString(bw, Path.GetFileNameWithoutExtension(multiPatches[x].Name), 20);
                    IOHelper.Write8BitString(bw, multiPatches[x].Type.PadRight(4, ' '), 4);
                    bw.Write((short)multiPatches[x].Description.DescriptorCount);
                    multiPatches[x].Description.Write(bw);
                    bw.Write((short)multiPatches[x].Ranges.Count);
                    for (int i = 0; i < multiPatches[x].Ranges.Count; i++)
                    {
                        bw.Write((short)multiPatches[x].Ranges[i].Bank);
                        bw.Write((byte)multiPatches[x].Ranges[i].Start);
                        bw.Write((byte)multiPatches[x].Ranges[i].End);
                    }
                }
                patches.Clear();
                multiPatches.Clear();
                bw.Close();
            }
        }
        private static int GetAssetListSize(List<SampleAsset> assets)
        {
            int size = 12;
            for (int x = 0; x < assets.Count; x++)
                size += 54 + assets[x].Data.Length;
            return size;
        }
        private static int GetPatchListSize(List<PatchInfo> patches, List<PatchInfo> multiPatches)
        {
            int size = 12;
            for (int x = 0; x < patches.Count; x++)
            {
                int pSize = 28 + 4 * patches[x].Ranges.Count;
                for (int i = 0; i < patches[x].Description.CustomDescriptions.Length; i++)
                    pSize += patches[x].Description.CustomDescriptions[i].Size + 8;
                pSize += patches[x].Description.EnvelopeDescriptions.Length * (EnvelopeDescriptor.SIZE + 8);
                pSize += patches[x].Description.FilterDescriptions.Length * (FilterDescriptor.SIZE + 8);
                pSize += patches[x].Description.GenDescriptions.Length * (GeneratorDescriptor.SIZE + 8);
                pSize += patches[x].Description.LfoDescriptions.Length * (LfoDescriptor.SIZE + 8);
                patches[x].Size = pSize;
                size += 8 + pSize;
            }
            for (int x = 0; x < multiPatches.Count; x++)
            {
                int pSize = 28 + 4 * multiPatches[x].Ranges.Count;
                for (int i = 0; i < multiPatches[x].Description.CustomDescriptions.Length; i++)
                    pSize += multiPatches[x].Description.CustomDescriptions[i].Size + 8;
                multiPatches[x].Size = pSize;
                size += 8 + pSize;
            }
            return size;
        }
        private static bool ContainsPatch(string patchName, List<PatchInfo> patchList)
        {
            patchName = Path.GetFileNameWithoutExtension(patchName).ToLower();
            for (int x = 0; x < patchList.Count; x++)
            {
                string secondPatch = Path.GetFileNameWithoutExtension(patchList[x].Name).ToLower();
                if (patchName.Equals(secondPatch))
                    return true;
            }
            return false;
        }
        private static bool ContainsAsset(string assetName, List<SampleAsset> assetList)
        {
            assetName = assetName.ToLower();
            for (int x = 0; x < assetList.Count; x++)
            {
                if (assetName.Equals(assetList[x].Name))
                    return true;
            }
            return false;
        }
    }
}
