using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class DisableXrefScan
{
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: DisableXrefScan <Il2CppInterop.Generator.dll>");
            return 2;
        }

        string path = Path.GetFullPath(args[0]);
        string backup = path + ".xref-enabled.bak";
        string temp = path + ".tmp";
        Console.WriteLine("Reading " + path);
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(path));
        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadWrite = false, InMemory = true, AssemblyResolver = resolver }))
        {
            Console.WriteLine("Strong named: " + assembly.Name.HasPublicKey);
            TypeDefinition type = assembly.MainModule.Types.FirstOrDefault(t =>
                t.FullName == "Il2CppInterop.Generator.Passes.Pass16ScanMethodRefs");
            MethodDefinition method = type == null ? null : type.Methods.FirstOrDefault(m => m.Name == "DoPass");
            if (method == null || method.ReturnType.FullName != "System.Void")
                throw new InvalidOperationException("Compatible Pass16ScanMethodRefs.DoPass method was not found.");

            if (method.Body.Instructions.Count == 1 && method.Body.Instructions[0].OpCode == OpCodes.Ret)
            {
                Console.WriteLine("XRef scan is already disabled: " + path);
                return 0;
            }

            if (!File.Exists(backup))
                File.Copy(path, backup);
            method.Body.ExceptionHandlers.Clear();
            method.Body.Variables.Clear();
            method.Body.Instructions.Clear();
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            Console.WriteLine("Writing patched assembly...");
            assembly.Write(temp);
        }
        File.Copy(temp, path, true);
        File.Delete(temp);
        Console.WriteLine("Disabled Il2CppInterop XRef scan for x86 compatibility: " + path);
        return 0;
    }
}
