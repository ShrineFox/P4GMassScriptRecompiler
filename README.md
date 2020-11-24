# P4GMassScriptRecompiler
Alters .flow and and outputs each combination of changes as a new mod. For building releases of the [P4G Mod Menu](https://github.com/ShrineFox/Persona-4-Golden-Mod-Menu).  
Uses [SimpleCommandLine](https://github.com/TGEnigma/SimpleCommandLine) by [TGEnigma](https://github.com/TGEnigma) and relies on [AtlusScriptCompiler](https://ci.appveyor.com/project/TGEnigma/atlusscripttoolchain/build/artifacts) and [PackTools](https://github.com/TGEnigma/AtlusFileSystemLibrary/releases).  
You must use a Mod Compendium folder containing the init_free.bin and Mod.xml you want to update/duplicate for each combination.  

# Usage
1. Place the program in the root of Persona-4-Golden-Mod-Menu with its dependencies.
2. Download [AtlusScriptCompiler](https://ci.appveyor.com/project/TGEnigma/atlusscripttoolchain/build/artifacts) and [PackTools](https://github.com/TGEnigma/AtlusFileSystemLibrary/releases).
3. Unpack Persona-4-Golden-Mod-Menu\build\input\init_free.bin using PakPack.exe, so that there is a build\input\extracted\field\script\field.bf.  
You can more easily do this by running Persona-4-Golden-Mod-Menu\build.bat once (removing the lines at the end about deleting the unpacked files).
2. Supply the following arguments via commandline:  
```P4GMassScriptRecompiler.exe -c "C:\Path\To\AtlusScriptCompiler.exe" -p "C:\Path\To\PakPack.exe" -b "C:\Path\To\SampleMod\Data\data00004\init_free.bin"```
3. Wait for mod folders to populate the location of your Sample Mod.
