using System;
using System.Linq;

namespace GeoArcSysPACker.Utils
{
    public static class ConsoleTools
    {
        public static string[] GetParams(string[] args)
        {
            return args.Where(a =>
            {
                if (a.First() != '-')
                    return true;
                return false;
            }).ToArray();
        }

        public static string[] GetOptionalParams(string[] args)
        {
            return args.Where(a =>
            {
                if (a.First() == '-')
                    return true;
                return false;
            }).ToArray();
        }

        private static bool AlwaysOverwrite;

        public static bool OverwritePrompt(string file)
        {
            if (AlwaysOverwrite)
                return true;

            var firstTime = true;

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
                    return true;
                }

                if (overwrite.ToUpper().Equals("N"))
                {
                    Console.WriteLine();
                    return false;
                }

                if (overwrite.ToUpper().Equals("A"))
                {
                    Console.WriteLine();
                    return AlwaysOverwrite = true;
                }

                ClearCurrentConsoleLine();
            }
        }

        public static void ClearCurrentConsoleLine()
        {
            var currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }
    }
}