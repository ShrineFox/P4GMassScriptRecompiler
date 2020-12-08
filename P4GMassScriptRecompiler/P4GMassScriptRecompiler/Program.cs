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

        public static string fieldFlow = Path.Combine(Directory.GetCurrentDirectory(), "init_free\\field.bf.flow");
        public static string fieldBf = Path.Combine(Path.GetDirectoryName(fieldFlow), Path.GetFileNameWithoutExtension(fieldFlow)) + ".flow.bf";
        public static string dngFlow = Path.Combine(Directory.GetCurrentDirectory(), "field\\dungeon.bf.flow");
        public static string dngBf = Path.Combine(Path.GetDirectoryName(dngFlow), Path.GetFileNameWithoutExtension(dngFlow)) + ".flow.bf";
        public static List<(string, string)> toggleAbles = new List<(string, string)>
        {
            ("\t//BIT_ON(6420);","\tBIT_ON(6420);"), //Permanently Disable First Time Setup
            ("\t//BIT_OFF(6421);","\tBIT_OFF(6421);"), //Permanently Disable Mod Menu
            ("\t//BIT_OFF(6429);","\tBIT_OFF(6429);"), //Permanently Disable Options

            ("\t//BIT_ON(6422);","\tBIT_ON(6422);"), //Permanent QuickTravelPlus
            ("\t//BIT_ON(6423);","\tBIT_ON(6423);"), //Permanent MobileCalendar
            //("\t//BIT_ON(6424);","\tBIT_ON(6424);"), //Permanent DungeonOptions
            ("\t//BIT_ON(6425);","\tBIT_ON(6425);"), //Permanent Find a Friend
            ("\t//BIT_ON(6426);","\tBIT_ON(6426);"), //Permanent Save Anywhere
            ("\t//BIT_ON(6427);","\tBIT_ON(6427);"), //Permanent Game Over Skip
            ("\t//BIT_ON(6428);","\tBIT_ON(6428);"), //Permanent Reap-Balanced Encounters

            ("\t//BIT_OFF(6430);","\tBIT_OFF(6430);"), //Permanently Disable Fox (DungeonOptions)
            ("\t//BIT_OFF(6431);","\tBIT_OFF(6431);"), //Permanently Floor Select (DungeonOptions)
            ("\t//BIT_OFF(6432);","\tBIT_OFF(6432);"), //Permanently Goho-M (DungeonOptions)
            ("\t//BIT_OFF(6433);","\tBIT_OFF(6433);"), //Permanently Organize Party (DungeonOptions)
        };

        public static List<string> toggleAbleNames = new List<string>
        {
            "No Setup",
            "No ModMenu",
            "No Options",

            "QuickTravelPlus",
            "MobileCalendar",
            //"DungeonOptions",
            "FindAFriend",
            "SaveAnywhere",
            "VRGameOverSkip",
            "ConsistentReaper",
            
            "Fox",
            "FloorSelect",
            "Goho-",
            "OrganizeParty"
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

            string[] lines = File.ReadAllLines(fieldFlow);
            var combinations = Permutations(toggleAbleNames);
            int id = 0;
            foreach (var combination in combinations.Where(x => x.Count() > 0))
            {
                if (id >= Options.Start)
                {
                    //Kill cmd processes
                    foreach (var process in Process.GetProcessesByName("cmd"))
                        process.Kill();
                    //Remove new bf if it already exists
                    using (WaitForFile(fieldBf, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Wait))) { };
                    if (File.Exists(fieldBf))
                        File.Delete(fieldBf);
                    using (WaitForFile(dngBf, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Wait))) { };
                    if (File.Exists(dngBf))
                        File.Delete(dngBf);

                    //Set all lines to false/disabled
                    foreach (var modToggle in toggleAbles)
                        for (int i = 0; i < lines.Length; i++)
                            if (lines[i].StartsWith(modToggle.Item1))
                                lines[i] = lines[i].Replace(modToggle.Item1, modToggle.Item2);

                    //Enable mods, build description and modify flowscript lines
                    string description = "Custom Square Menu with ";
                    foreach (var modName in combination)
                    {
                        foreach (var modToggle in toggleAbles.Where(x => x.Item1.ToLower().Contains(modName.ToLower())))
                        {
                            for (int i = 0; i < lines.Length; i++)
                                if (lines[i].StartsWith(modToggle.Item2))
                                    lines[i] = lines[i].Replace(modToggle.Item2, modToggle.Item1);
                            description += modName + ", ";
                        }
                    }

                    //Remove last comma and space from description
                    description = description.TrimEnd(' ');
                    description = description.TrimEnd(',');

                    //Update flowscript file with changes
                    using (WaitForFile(fieldFlow, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Wait))) { };
                    System.IO.File.WriteAllText(fieldFlow, string.Join("\n", lines));

                    //Wait for bf to be usable
                    using (WaitForFile(fieldBf, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Wait))) { };
                    if (File.Exists(fieldBf))
                        File.Delete(fieldBf);

                    Console.WriteLine($"Creating new mod: {description} ({id + 1}/{combinations.Count()})");
                    //Create new field BF
                    Compile(Options.Compiler, fieldFlow, fieldBf);
                    Console.WriteLine($"  Created new Field BF...");

                    //Create field bf (if Aemulus format)
                    using (WaitForFile(fieldBf, FileMode.Open, FileAccess.ReadWrite, FileShare.None, Convert.ToInt32(Options.Wait))) { };
                    string newFieldBfPath = Path.GetDirectoryName(Options.Bin) + "\\init_free\\field\\script\\field.bf";
                    if (!Options.Aemulus)
                    {
                        if (Directory.Exists(Path.GetDirectoryName(newFieldBfPath)))
                            Directory.Delete(Path.GetDirectoryName(newFieldBfPath), true);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newFieldBfPath));
                        File.Copy(fieldBf, newFieldBfPath, true);
                    }
                    File.Copy(fieldFlow, Path.Combine(Path.GetDirectoryName(Options.Bin), "field.flow"), true);

                    //Repack BIN if not using Aemulus format
                    if (!Options.Aemulus)
                        Repack(Options.Bin, fieldBf, "field/script/field.bf");

                    Console.WriteLine($"  Creating Aemulus folder...");
                    //Create new Mod folder containing changes
                    CreateFolder(fieldBf, Options.Bin, description, id);
                }
                
                id++;

                Console.WriteLine($"  Done");
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
                    xmlTxt[i] = xmlTxt[i].Substring(0, xmlTxt[i].IndexOf("</Id>") - 3) + id.ToString("000") + "</Id>";
                //Set new description
                if (xmlTxt[i].Contains("<Description>"))
                    xmlTxt[i] = $"  <Description>{description}</Description>";
            }
            //Overwrite mod.xml
            File.WriteAllText(modXml, string.Join("\n", xmlTxt));

            //Remove empty dirs
            RemoveEmptyDirs(Path.GetDirectoryName(binPath));

            //Create folder
            string newDir = Path.Combine(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(modXml)), description.Replace(", ", "\\").Replace("Square Menu with ","")), "Custom Square Menu");
            Directory.CreateDirectory(newDir);
            CopyDir(Path.GetDirectoryName(modXml), newDir);
        }

        private static void Repack(string binPath, string newBf, string replacePath)
        {
            Console.WriteLine("  Repacking BIN...");
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

        private static void Compile(string compilerPath, string flow, string bf)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "cmd";
            start.Arguments = $"/C {compilerPath} \"{flow}\" -Compile -Encoding P4 -Library P4G -OutFormat V1 -OutFile \"{bf}\" -Hook";
            start.UseShellExecute = false;
            start.RedirectStandardOutput = false;
            start.CreateNoWindow = false;
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

                if (Options.Aemulus && name.Contains(".bin") || (!Options.Flow && name.Contains(".flow")))
                {
                    // Skip
                }
                else 
                {
                    string dest = Path.Combine(destFolder, name);
                    File.Copy(file, dest, true);
                }
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

        private static void RemoveEmptyDirs(string dir)
        {
            foreach (var directory in Directory.GetDirectories(dir))
            {
                RemoveEmptyDirs(directory);
                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
        }
    }

    public class ProgramOptions
    {
        [Option("c", "compiler", "path", "The path to the AtlusScriptCompiler exe.", Required = true)]
        public string Compiler { get; set; } = "";

        [Option("b", "bin", "path", "The path to the init_free.bin in the sample Mod folder.", Required = true)]
        public string Bin { get; set; } = "";

        [Option("a", "aemulus", "boolean", "If enabled, use Aemulus Package structure instead of repacking .bin.")]
        public bool Aemulus { get; set; } = false;

        [Option("f", "flow", "boolean", "If enabled, include uncompiled field/dungeon flowscripts.")]
        public bool Flow { get; set; } = false;

        [Option("w", "wait", "integer", "The number of milliseconds to wait before trying to modify a file that was just in use (default is 150).")]
        public int Wait { get; set; } = 150;

        [Option("s", "start", "integer", "The number of the iteration to start at (default is 0).")]
        public int Start { get; set; } = 0;
    }
}
