using AtlusFileSystemLibrary;
using AtlusFileSystemLibrary.FileSystems.PAK;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TGE.SimpleCommandLine;

namespace P4GMassScriptRecompiler
{
    class Program
    {
        public static ProgramOptions Options { get; private set; }

        public static string flow = Path.Combine(Directory.GetCurrentDirectory(), "field\\field.bf.flow");
        public static string newBf = Path.Combine(Path.GetDirectoryName(flow), Path.GetFileNameWithoutExtension(flow)) + ".flow.bf";
        public static List<(string, string)> toggleAbles = new List<(string, string)>
        {
            ("\tbool modMenu = true;","\tbool modMenu = false;"),
            //("\tbool quickTravel = true;","\tbool quickTravel = false;"),
            ("import ( \"../QuickTravelPlus.flow\" );","//import ( \"../QuickTravelPlus.flow\" );"),
            ("\tbool mobileCalendar = true;","\tbool mobileCalendar = false;"),
            ("import ( \"../VRGameOverSkip.flow\" );","//import ( \"../VRGameOverSkip.flow\" );"),
            ("\tbool findAFriend = true;","\tbool findAFriend = false;"),
            ("\tbool saveAnywhere = true;","\tbool saveAnywhere = false;")
        };

        public static List<string> toggleAbleNames = new List<string>
        {
            "ModMenu",
            "QuickTravelPlus",
            "MobileCalendar",
            "VRGameOverSkip",
            "FindAFriend",
            "SaveAnywhere"
        };

        public static IEnumerable<T[]> Permutations<T>(IEnumerable<T> source)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));

            T[] data = source.ToArray();

            return Enumerable
              .Range(0, 1 << (data.Length))
              .Select(index => data
                 .Where((v, i) => (index & (1 << i)) != 0)
                 .ToArray());
        }

        static void Main(string[] args)
        {
            //Validate input
            string about = SimpleCommandLineFormatter.Default.FormatAbout<ProgramOptions>("ShrineFox", "Alters lines in a .flow and recompiles/repacks each combination of changes into separate mod folders. Requires a sample Mod Compendium project (ID and Description in Mod.xml will be overwritten, as well as init_free.bin). Made for use with the P4G Mod Menu.");
            Console.WriteLine(about);
            try
            {
                Options = SimpleCommandLineParser.Default.Parse<ProgramOptions>(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            string[] lines = File.ReadAllLines(flow);
            var combinations = Permutations(toggleAbleNames);
            int id = 0;
            foreach (var combination in combinations.Where(x => x.Count() > 0))
            {
                //Kill cmd processes
                foreach (var process in Process.GetProcessesByName("cmd"))
                    process.Kill();
                //Remove new bf if it already exists
                using (WaitForFile(newBf, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Sleep))) { };
                if (File.Exists(newBf))
                    File.Delete(newBf);

                //Set all lines to false/disabled
                foreach (var modToggle in toggleAbles)
                    for (int i = 0; i < lines.Length; i++)
                        if (lines[i].StartsWith(modToggle.Item1))
                            lines[i] = lines[i].Replace(modToggle.Item1, modToggle.Item2);

                string description = "Square Menu with ";

                foreach (var modName in combination)
                {
                    //Enable mods and build description
                    foreach (var modToggle in toggleAbles.Where(x => x.Item1.ToLower().Contains(modName.ToLower())))
                    {
                        for (int i = 0; i < lines.Length; i++)
                            if (lines[i].StartsWith(modToggle.Item2))
                                lines[i] = lines[i].Replace(modToggle.Item2, modToggle.Item1);
                        description += modName + ", ";
                    }
                }

                //Fix description
                description = description.TrimEnd(' ');
                description = description.TrimEnd(',');

                //Update flowscript file
                using (WaitForFile(flow, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Sleep))) { };
                System.IO.File.WriteAllText(flow, string.Join("\n", lines));

                //Wait for bf to be usable
                using (WaitForFile(newBf, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Sleep))) { };
                if (File.Exists(newBf))
                    File.Delete(newBf);

                Console.WriteLine($"Creating new mod: {description} ({id + 1}/{combinations.Count()})");
                //Create new BF and replace BIN
                Compile(Options.Compiler);
                Console.WriteLine($"  Created new BF...");
                using (WaitForFile(newBf, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Sleep))) { };
                File.Copy(flow, Path.Combine(Path.GetDirectoryName(Options.Bin), "field.flow"), true);
                Repack(Options.Bin, newBf, "field/script/field.bf");

                Console.WriteLine($"  Repacked BIN, creating folder...");
                //Create new Mod folder containing changes
                CreateFolder(newBf, Options.Bin, description, id);
                id++;

                Console.WriteLine($"  Done");
                //Console.ReadKey();
            }
        }

        private static void CreateFolder(string newBf, string binPath, string description, int id)
        {
            //Load mod.xml text
            string modXml = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(binPath))), "Mod.xml");
            string[] xmlTxt = File.ReadAllLines(modXml);
            for (int i = 0; i < xmlTxt.Length; i++)
            {
                //Set unique ID for keeping separate installations
                if (xmlTxt[i].Contains("<Id>"))
                    xmlTxt[i] = xmlTxt[i].Substring(0, xmlTxt[i].IndexOf("</Id>") - 2) + id.ToString("00") + "</Id>";
                //Set new description
                if (xmlTxt[i].Contains("<Description>"))
                    xmlTxt[i] = $"  <Description>{description}</Description>";
            }
            //Overwrite mod.xml
            File.WriteAllText(modXml, string.Join("\n", xmlTxt));

            //Create folder
            string newDir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(modXml)), $"P4G {description}");
            Directory.CreateDirectory(newDir);
            CopyDir(Path.GetDirectoryName(modXml), newDir);
        }

        private static void Repack(string binPath, string newBf, string replacePath)
        {
            using var inputStream = File.Open(binPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            if (!PAKFileSystem.TryOpen(inputStream, false, out var pak))
                return;

            using (pak)
            {
                pak.AddFile(replacePath, newBf, ConflictPolicy.Replace);
                using var outputStream = pak.Save();

                inputStream.Seek(0, SeekOrigin.Begin);
                outputStream.Seek(0, SeekOrigin.Begin);

                outputStream.CopyTo(inputStream);
            }

        }

        private static void Compile(string compilerPath)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "cmd";
            start.Arguments = $"/C {compilerPath} \"{flow}\" -Compile -Encoding P4 -Library P4G -OutFormat V1 -OutFile \"{newBf}\" -Hook";
            start.UseShellExecute = true;
            start.RedirectStandardOutput = false;
            start.CreateNoWindow = true;
            using (Process process = Process.Start(start))
            {
                process.WaitForExit();
            }
        }

        public static void CopyDir(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);

            // Get Files & Copy
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);

                // ADD Unique File Name Check to Below!!!!
                string dest = Path.Combine(destFolder, name);
                File.Copy(file, dest, true);
            }

            // Get dirs recursively and copy files
            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyDir(folder, dest);
            }
        }

        public static FileStream WaitForFile(string fullPath, FileMode mode, FileAccess access, FileShare share, int sleepNumber)
        {
            for (int numTries = 0; numTries < 10; numTries++)
            {
                FileStream fs = null;
                try
                {
                    fs = new FileStream(fullPath, mode, access, share);
                    return fs;
                }
                catch (IOException)
                {
                    if (fs != null)
                    {
                        fs.Dispose();
                    }
                    Thread.Sleep(sleepNumber);
                }
            }

            return null;
        }


    }

    public class ProgramOptions
    {
        [Option("c", "compiler", "path", "The path to the AtlusScriptCompiler exe.", Required = true)]
        public string Compiler { get; set; } = "";

        [Option("b", "bin", "path", "The path to the init_free.bin in the sample Mod folder.", Required = true)]
        public string Bin { get; set; } = "";

        [Option("s", "sleep", "integer", "The number of milliseconds to wait before trying to modify a file that was just in use (default is 150).")]
        public int Sleep { get; set; } = 150;
    }
}
