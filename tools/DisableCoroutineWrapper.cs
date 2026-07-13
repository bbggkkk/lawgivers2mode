using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class DisableCoroutineWrapper
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1 || !File.Exists(args[0]))
            {
                Console.Error.WriteLine("Usage: DisableCoroutineWrapper <SupportModules\\Il2Cpp.dll>");
                return 2;
            }

            string path = Path.GetFullPath(args[0]);
            string backup = path + ".coroutine-enabled.bak";
            string temp = path + ".tmp";
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(path));

            using (var assembly = AssemblyDefinition.ReadAssembly(path,
                new ReaderParameters { InMemory = true, AssemblyResolver = resolver }))
            {
                var type = assembly.MainModule.Types.FirstOrDefault(t =>
                    t.FullName == "MelonLoader.Support.Main");
                var method = type == null ? null : type.Methods.FirstOrDefault(m =>
                    m.Name == "Initialize" && m.HasBody);
                if (method == null)
                    throw new InvalidOperationException("Compatible support module Initialize method was not found.");

                var call = method.Body.Instructions.FirstOrDefault(i =>
                    (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
                    i.Operand is MethodReference &&
                    ((MethodReference)i.Operand).FullName ==
                    "System.Void MelonLoader.Support.MonoEnumeratorWrapper::Register()");
                var componentType = assembly.MainModule.Types.FirstOrDefault(t =>
                    t.FullName == "MelonLoader.Support.SM_Component");
                var create = componentType == null ? null : componentType.Methods.FirstOrDefault(m =>
                    m.Name == "Create" && m.HasBody && m.ReturnType.FullName == "System.Void");
                bool createAlreadyDisabled = create != null &&
                    create.Body.Instructions.Count == 1 &&
                    create.Body.Instructions[0].OpCode == OpCodes.Ret;
                if (call == null && (create == null || createAlreadyDisabled))
                {
                    Console.WriteLine("Support class injection is already disabled or not present.");
                    return 0;
                }

                if (!File.Exists(backup))
                    File.Copy(path, backup);
                if (call != null)
                {
                    call.OpCode = OpCodes.Nop;
                    call.Operand = null;
                }
                if (create != null && !createAlreadyDisabled)
                {
                    create.Body.ExceptionHandlers.Clear();
                    create.Body.Variables.Clear();
                    create.Body.Instructions.Clear();
                    create.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                }
                assembly.Write(temp);
            }

            File.Copy(temp, path, true);
            File.Delete(temp);
            Console.WriteLine("Disabled unsupported IL2CPP support-class injection: " + path);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
