using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcSysAPI.Common.Enums;
using ArcSysAPI.Models;
using GeoArcSysPACker.Models;
using GeoArcSysPACker.Utils.Extensions;

namespace GeoArcSysPACker
{
    internal class Program
    {
        [Flags]
        public enum Options
        {
            MinNameLength = 0x1,
            Recursive = 0x2,
            NameID = 0x4,
            NameIDExt = 0x8,
            Endianness = 0x8000000,
            Continue = 0x40000000
        }

        public static ConsoleOption[] ConsoleOptions =
        {
            new ConsoleOption
            {
                Name = "Minimum Name Length",
                ShortOp = "-mnl",
                LongOp = "--minnamelength",
                Description =
                    "Specifies the minimum file name length. {Auto} will \n\t\t\tautomatically calculate it.",
                HasArg = true,
                Flag = Options.MinNameLength
            },
            new ConsoleOption
            {
                Name = "Recursive",
                ShortOp = "-r",
                LongOp = "--recursive",
                Description =
                    "Specifies, if the tool is unpacking, to look through every \n\t\t\tfolder, from the parent, recursively.",
                Flag = Options.Recursive
            },
            new ConsoleOption
            {
                Name = "NameID",
                ShortOp = "-ni",
                LongOp = "--nameid",
                Description =
                    "Applies a unique ID based the file's name. (32 character limit)",
                Flag = Options.NameID
            },
            new ConsoleOption
            {
                Name = "NameIDExt",
                ShortOp = "-nie",
                LongOp = "--nameidext",
                Description =
                    "Applies a unique ID based the file's name. (64 character limit)",
                Flag = Options.NameIDExt
            },
            new ConsoleOption
            {
                Name = "Endianness",
                ShortOp = "-en",
                LongOp = "--endianness",
                Description = "Specify the output file's endianness. {LittleEndian|BigEndian}",
                HasArg = true,
                Flag = Options.Endianness
            },
            new ConsoleOption
            {
                Name = "Continue",
                ShortOp = "-c",
                LongOp = "--continue",
                Description = "Don't pause the application when finished.",
                Flag = Options.Continue
            }
        };

        public static string assemblyPath = string.Empty;
        private static bool AlwaysOverwrite;
        private static int MinNameLength;

        public static Options options;
        public static ByteOrder endianness = ByteOrder.LittleEndian;

        [STAThread]
        private static void Main(string[] args)
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            assemblyPath = Path.GetFullPath(Uri.UnescapeDataString(uri.Path));

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\nGeo ArcSys Packer\nprogrammed by: Geo\n\n");
            Console.ForegroundColor = ConsoleColor.White;

            ProcessOptions(args);

            try
            {
                if (args.Length == 0 ||
                    args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
                {
                    ShowUsage();
                    Pause();
                    return;
                }

                var procedureType = Procedure.Pack;

                if (args.Length > 1)
                {
                    args[1] = args[1].ToLower().FirstCharToUpper();
                    byte b;
                    if (byte.TryParse(args[1], out b))
                    {
                        if (b <= 1)
                            procedureType = (Procedure) b;
                    }
                    else if (Enum.IsDefined(typeof(Procedure), args[1]))
                    {
                        procedureType = (Procedure) Enum.Parse(typeof(Procedure), args[1]);
                    }
                }

                var path = Path.GetFullPath(args[0]);

                var attr = new FileAttributes();
                try
                {
                    attr = File.GetAttributes(path);
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Can't retrieve \"{path}\" file system attributes.");
                    Console.ForegroundColor = ConsoleColor.White;
                    return;
                }


                if (!attr.HasFlag(FileAttributes.Directory)) procedureType = Procedure.Unpack;

                if (procedureType == Procedure.Pack)
                {
                    if (!Directory.Exists(path))
                    {
                        Console.WriteLine($"The \"{path}\" directory does not exist.");
                        return;
                    }

                    var savePath = path + ".pac";
                    var createExtNameID = options.HasFlag(Options.NameIDExt);

                    var createNameID = options.HasFlag(Options.NameID) ||
                                       createExtNameID;
                    var pacParams =
                        createExtNameID ? PACFileInfo.PACParameters.GenerateExtendedNameID :
                        createNameID ? PACFileInfo.PACParameters.GenerateNameID : 0;

                    if (File.Exists(savePath))
                        if (new FileInfo(savePath).Length > 0)
                            if (!OverwritePrompt(savePath))
                                return;

                    Console.WriteLine($"Packing {Path.GetFileNameWithoutExtension(savePath)} in {Enum.GetName(typeof(ByteOrder), endianness)}...");

                    File.WriteAllBytes(savePath,
                        new PACFileInfo(path, pacParams, MinNameLength, endianness).GetBytes());
                    Console.WriteLine($"Successfully packed.");
                }
                else
                {
                    var paths = new List<string>();

                    var baseDirectory = string.Empty;

                    var saveFolder = string.Empty;

                    var isDirectory = attr.HasFlag(FileAttributes.Directory);

                    var isRecursive = options.HasFlag(Options.Recursive);

                    if (isRecursive)
                    {
                        baseDirectory = path;
                        saveFolder = baseDirectory + "_unpack";
                    }

                    if (isRecursive && isDirectory)
                        paths.AddRange(DirSearch(path));
                    else if (isDirectory)
                        paths.AddRange(Directory.GetFiles(path));
                    else
                        paths.Add(path);

                    foreach (var filePath in paths)
                    {
                        if (!File.Exists(filePath))
                        {
                            Console.WriteLine($"The \"{filePath}\" file does not exist.");
                            continue;
                        }

                        if (!isRecursive) saveFolder = baseDirectory = Directory.GetParent(filePath).FullName;

                        var mainPACFile = new PACFileInfo(filePath, MinNameLength);

                        mainPACFile.Active = true;

                        if (!mainPACFile.IsValidPAC)
                        {
                            Console.WriteLine($"{mainPACFile.Name} is not a valid PAC file.");
                            continue;
                        }

                        ProcessFile(mainPACFile, baseDirectory, saveFolder);

                        var vfiles = RecursivePACExplore(mainPACFile);

                        foreach (var vfile in vfiles) ProcessFile(vfile, baseDirectory, saveFolder);

                        mainPACFile.Active = false;
                    }
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Complete!");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.WriteLine("Something went wrong!");
                Console.ForegroundColor = ConsoleColor.White;
            }
            Pause();
        }

        public static void ProcessOptions(string[] args)
        {
            var newArgsList = new List<string>();

            for (var i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.First() != '-')
                    continue;

                newArgsList.Add(arg);

                foreach (var co in ConsoleOptions)
                    if (arg == co.ShortOp || arg == co.LongOp)
                    {
                        options |= (Options) co.Flag;
                        if (co.HasArg)
                        {
                            var subArgsList = new List<string>();
                            var lastArg = string.Empty;
                            for (var j = i; j < args.Length - 1; j++)
                            {
                                var subArg = args[j + 1];

                                if (subArg.First() == '-')
                                    break;

                                if (string.IsNullOrWhiteSpace(lastArg) || subArg.ToLower() != lastArg.ToLower())
                                    subArgsList.Add(subArg);
                                i++;
                            }

                            co.SpecialObject = subArgsList.ToArray();
                        }
                    }
            }

            foreach (var co in ConsoleOptions)
            {
                if (co.Flag == null)
                    continue;

                if (co.HasArg)
                {
                    var subArgs = (string[]) co.SpecialObject;
                    if ((Options) co.Flag == Options.MinNameLength &&
                        options.HasFlag(Options.MinNameLength))
                    {
                        int length;
                        if (subArgs.Length > 0 && subArgs[0].ToLower() == "auto")
                            length = 0;
                        else if (subArgs.Length == 0 || !int.TryParse(subArgs[0], out length))
                            length = 24;

                        MinNameLength = length;
                    }

                    if ((Options) co.Flag == Options.Endianness &&
                        options.HasFlag(Options.Endianness))
                    {
                        if (subArgs.Length > 0)
                        {
                            ByteOrder endian;
                            if (Enum.TryParse(subArgs[0], true, out endian)) endianness = endian;
                        }
                        else
                        {
                            endianness = ByteOrder.LittleEndian;
                        }
                    }
                }
            }
        }

        public static void ProcessFile(VirtualFileSystemInfo vfsi, string baseDirectory, string saveFolder)
        {
            var extPathsList = new List<string>();
            extPathsList.Add(vfsi.GetPrimaryPath());
            extPathsList.AddRange(vfsi.GetExtendedPaths().Select(ep => Path.GetFileNameWithoutExtension(ep)));
            var extPaths = extPathsList.ToArray();
            extPaths[0] = extPaths[0].Replace(baseDirectory, string.Empty);
            var ext = vfsi.VirtualRoot.Extension;
            if (string.IsNullOrWhiteSpace(ext))
                extPaths[0] += "_unpack";

            if (!(vfsi is PACFileInfo)) extPaths[extPaths.Length - 1] = vfsi.Name;

            var extPath = string.Join("\\", extPaths);
            var savePath = Path.GetFullPath(saveFolder + extPath.Replace("?", "_"));

            if (!string.IsNullOrWhiteSpace(ext))
                savePath = savePath.Replace(ext, string.Empty);

            if (vfsi is PACFileInfo)
            {
                Directory.CreateDirectory(savePath);
                return;
            }

            if (File.Exists(savePath))
                if (new FileInfo(savePath).Length > 0)
                    if (!OverwritePrompt(savePath))
                        return;

            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            Console.WriteLine($"Saving {vfsi.Name}...");

            File.WriteAllBytes(savePath, vfsi.GetBytes());

            Console.WriteLine($"Successfully saved.");
        }

        public static VirtualFileSystemInfo[] RecursivePACExplore(PACFileInfo pfi, int level = 0)
        {
            Console.WriteLine($"Searching {pfi.Name}...");
            var vfiles = new List<VirtualFileSystemInfo>();
            vfiles.AddRange(pfi.GetFiles());

            var len = vfiles.Count;

            for (var i = 0; i < len; i++)
            {
                var pacFileInfo = vfiles[i] as PACFileInfo;
                if (pacFileInfo != null)
                    vfiles.AddRange(RecursivePACExplore(pacFileInfo, level + 1));
            }

            return vfiles.ToArray();
        }

        public static string[] DirSearch(string sDir)
        {
            var stringList = new List<string>();
            foreach (var f in Directory.GetFiles(sDir)) stringList.Add(f);
            foreach (var d in Directory.GetDirectories(sDir)) stringList.AddRange(DirSearch(d));

            return stringList.ToArray();
        }

        public static bool OverwritePrompt(string file)
        {
            if (AlwaysOverwrite)
                return true;

            var firstTime = true;

            var result = false;

            while (true)
            {
                if (firstTime)
                {
                    Console.WriteLine($"\nThe file: {file} already exist. Do you want to overwrite it? Y/N/A");
                    firstTime = false;
                }

                var overwrite = Convert.ToString(Console.ReadKey().KeyChar);
                if (overwrite.ToUpper().Equals("Y"))
                {
                    Console.WriteLine();
                    result = true;
                    break;
                }

                if (overwrite.ToUpper().Equals("N"))
                {
                    Console.WriteLine();
                    result = false;
                    break;
                }

                if (overwrite.ToUpper().Equals("A"))
                {
                    Console.WriteLine();
                    result = true;
                    AlwaysOverwrite = result;
                    break;
                }
            }

            Console.SetCursorPosition(0, Console.CursorTop - 1);
            ClearCurrentConsoleLine();
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            ClearCurrentConsoleLine();
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            ClearCurrentConsoleLine();
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            ClearCurrentConsoleLine();

            return result;
        }

        public static void ClearCurrentConsoleLine()
        {
            var currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private static void ShowUsage()
        {
            var shortOpMaxLength =
                ConsoleOptions.Select(co => co.ShortOp).OrderByDescending(s => s.Length).First().Length;
            var longOpMaxLength =
                ConsoleOptions.Select(co => co.LongOp).OrderByDescending(s => s.Length).First().Length;

            Console.WriteLine(
                $"Usage: {Path.GetFileName(assemblyPath)} <file/folder path> [pack/unpack] [options...]");

            Console.WriteLine("Options:");
            foreach (var co in ConsoleOptions)
                Console.WriteLine(
                    $"{co.ShortOp.PadRight(shortOpMaxLength)}\t{co.LongOp.PadRight(longOpMaxLength)}\t{co.Description}");
        }

        private static void Pause(bool force = false)
        {
            if (options.HasFlag(Options.Continue) && !force)
                return;

            Console.WriteLine("\rPress Any Key to exit...");
            Console.ReadKey();
        }

        private enum Procedure
        {
            Pack = 0,
            Unpack = 1
        }
    }
}