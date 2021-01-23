using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcSysAPI.Models;
using GeoArcSysPACker.Utils;
using GeoArcSysPACker.Utils.Extensions;

namespace GeoArcSysPACker
{
    internal class Program
    {
        private enum Procedure
        {
            Pack = 0,
            Unpack = 1
        }

        [Flags]
        public enum OptionParams
        {
            Recursive = 0x1,
            NameID = 0x2,
            NameIDExt = 0x4
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("\nGeo BlazBlue Packer\nprogrammed by: Geo\n\n");
            Console.ForegroundColor = ConsoleColor.White;

            var optionalArgs = ConsoleTools.GetOptionalParams(args).Select(p => p.ToLower()).ToArray();

            args = ConsoleTools.GetParams(args);

            try
            {
                if (args.Length > 0)
                {
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

                    var optionalParams = new OptionParams();

                    if (optionalArgs.Length > 0)
                    {
                        if (optionalArgs.Contains("-r") || optionalArgs.Contains("--recursive"))
                            optionalParams |= OptionParams.Recursive;

                        if (optionalArgs.Contains("-ni") || optionalArgs.Contains("--nameid"))
                        {
                            optionalParams |= OptionParams.NameID;
                        }
                        else if (optionalArgs.Contains("-nie") || optionalArgs.Contains("--nameidext"))
                        {
                            optionalParams |= OptionParams.NameIDExt;
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
                        var createExtNameID = optionalParams.HasFlag(OptionParams.NameIDExt);

                        var createNameID = optionalParams.HasFlag(OptionParams.NameID) ||
                                           createExtNameID;
                        var pacParams =
                            createExtNameID ? PACFileInfo.PACParameters.GenerateExtendedNameID :
                            createNameID ? PACFileInfo.PACParameters.GenerateNameID : 0;

                        File.WriteAllBytes(savePath, new PACFileInfo(path, pacParams).GetBytes());
                    }
                    else
                    {
                        var paths = new List<string>();

                        var baseDirectory = string.Empty;

                        var saveFolder = string.Empty;

                        var isDirectory = attr.HasFlag(FileAttributes.Directory);

                        var isRecursive = optionalParams.HasFlag(OptionParams.Recursive);

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

                            var mainPACFile = new PACFileInfo(filePath);

                            mainPACFile.Active = true;

                            if (!mainPACFile.IsValidPAC)
                            {
                                Console.WriteLine($"{mainPACFile.Name} is not a valid PAC file.");
                                continue;
                            }
                            else if (string.IsNullOrWhiteSpace(mainPACFile.Extension))
                            {
                                Console.WriteLine($"{mainPACFile.Name} has no extension.");
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
                    Console.WriteLine("Done!");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("Please input the path of a folder.");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.WriteLine("Something went wrong!");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public static void ProcessFile(VirtualFileSystemInfo vfsi, string baseDirectory, string saveFolder)
        {
            var extPathsList = new List<string>();
            extPathsList.Add(vfsi.GetPrimaryPath());
            extPathsList.AddRange(vfsi.GetExtendedPaths());
            var extPaths = extPathsList.ToArray();
            extPaths[0] = extPaths[0].Replace(baseDirectory, string.Empty);
            var ext = vfsi.VirtualRoot.Extension;
            if (string.IsNullOrWhiteSpace(ext))
                extPaths[0] += "_unpack";

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
                    if (!ConsoleTools.OverwritePrompt(savePath))
                        return;

            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            File.WriteAllBytes(savePath, vfsi.GetBytes());
        }

        public static VirtualFileSystemInfo[] RecursivePACExplore(PACFileInfo pfi, int level = 0)
        {
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
    }
}