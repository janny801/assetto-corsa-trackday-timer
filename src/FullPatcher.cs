using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Microsoft.Win32;

class CosturaAssemblyResolver : DefaultAssemblyResolver
{
    private ModuleDefinition _module;
    private Dictionary<string, AssemblyDefinition> _cache = new Dictionary<string, AssemblyDefinition>(StringComparer.OrdinalIgnoreCase);

    public void SetModule(ModuleDefinition module)
    {
        _module = module;
    }

    public override AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        AssemblyDefinition cached;
        if (_cache.TryGetValue(name.Name, out cached)) return cached;

        try
        {
            return base.Resolve(name);
        }
        catch (AssemblyResolutionException)
        {
            if (_module != null)
            {
                string resNameCompressed = "costura." + name.Name.ToLowerInvariant() + ".dll.compressed";
                string resNameRaw = "costura." + name.Name.ToLowerInvariant() + ".dll";

                var res = _module.Resources.OfType<EmbeddedResource>().FirstOrDefault(delegate (EmbeddedResource r) {
                    return r.Name.Equals(resNameCompressed, StringComparison.OrdinalIgnoreCase) ||
                           r.Name.Equals(resNameRaw, StringComparison.OrdinalIgnoreCase);
                });

                if (res != null)
                {
                    byte[] data = res.GetResourceData();
                    Stream asmStream;
                    if (res.Name.EndsWith(".compressed", StringComparison.OrdinalIgnoreCase))
                    {
                        var mem = new MemoryStream(data, 0, data.Length - 4);
                        var deflate = new DeflateStream(mem, CompressionMode.Decompress);
                        var outMem = new MemoryStream();
                        deflate.CopyTo(outMem);
                        outMem.Position = 0;
                        asmStream = outMem;
                    }
                    else
                    {
                        asmStream = new MemoryStream(data);
                    }

                    var asm = AssemblyDefinition.ReadAssembly(asmStream, new ReaderParameters { AssemblyResolver = this });
                    _cache[name.Name] = asm;
                    return asm;
                }
            }
            throw;
        }
    }
}

class FullPatcher
{
    static int Main(string[] args)
    {
        try
        {
            Console.WriteLine("========================================================");
            Console.WriteLine("  Assetto Corsa Track Day Timer & Session End Patcher   ");
            Console.WriteLine("========================================================");
            Console.WriteLine();

            string repoRoot = FindRepoRoot();
            string srcDir = Path.Combine(repoRoot, "src");
            string libDir = Path.Combine(srcDir, "lib");
            string actoolsCompressedPath = Path.Combine(srcDir, "actools_compressed.bin");
            string actoolsPatchedPath = Path.Combine(srcDir, "actools_patched.dll");
            string helperPath = Path.Combine(libDir, "TrackdayHelper.dll");

            // 1. Locate Content Manager.exe
            string cmPath = FindContentManager(args.Length > 0 ? args[0] : null);
            if (string.IsNullOrEmpty(cmPath) || !File.Exists(cmPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] Content Manager.exe not found automatically.");
                Console.ResetColor();
                Console.Write("Please enter full path to Content Manager.exe: ");
                cmPath = Console.ReadLine().Trim('"', ' ');
            }

            if (!File.Exists(cmPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Could not find Content Manager.exe. Skipping CM patch.");
                Console.ResetColor();
            }
            else
            {
                while (System.Diagnostics.Process.GetProcessesByName("Content Manager").Length > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[NOTICE] Content Manager is currently running.");
                    Console.WriteLine("Please close Content Manager and press ENTER to continue...");
                    Console.ResetColor();
                    Console.ReadLine();
                }

                PatchContentManager(cmPath, actoolsCompressedPath, actoolsPatchedPath, helperPath, libDir);
            }

            // 2. The game executable is intentionally left untouched. Track Day already
            // owns the countdown and session transition; changing acs.exe can interfere
            // with CSP behavior, including AI Flood.
            string acDir = FindAssettoCorsa(args.Length > 1 ? args[1] : null);
            if (string.IsNullOrEmpty(acDir) || !Directory.Exists(acDir))
                Console.WriteLine("[NOTICE] Assetto Corsa directory not found; no game files need changing.");
            else
                Console.WriteLine("[NOTICE] Assetto Corsa executable left unchanged.");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("========================================================");
            Console.WriteLine("  All Done! Mod installed successfully.                ");
            Console.WriteLine("========================================================");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FATAL ERROR: " + ex);
            Console.ResetColor();
            return 1;
        }
    }

    static string FindRepoRoot()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, "src", "lib")) && Directory.Exists(Path.Combine(dir, "apps")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    static string FindContentManager(string explicitPath)
    {
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string[] candidates = new string[]
        {
            Path.Combine(desktop, "Content Manager.exe"),
            Path.Combine(Environment.CurrentDirectory, "Content Manager.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content Manager.exe")
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string oneDriveDesktop = Path.Combine(userProfile, "OneDrive", "Desktop", "Content Manager.exe");
        if (File.Exists(oneDriveDesktop)) return oneDriveDesktop;

        return null;
    }

    static string FindAssettoCorsa(string explicitPath)
    {
        if (!string.IsNullOrEmpty(explicitPath) && Directory.Exists(explicitPath))
            return explicitPath;

        try
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 244210"))
            {
                if (key != null)
                {
                    var val = key.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(val) && Directory.Exists(val))
                        return val;
                }
            }
        }
        catch { }

        string[] commonPaths = new string[]
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\assettocorsa",
            @"C:\Program Files\Steam\steamapps\common\assettocorsa",
            @"D:\SteamLibrary\steamapps\common\assettocorsa",
            @"D:\Steam\steamapps\common\assettocorsa",
            @"E:\SteamLibrary\steamapps\common\assettocorsa",
            @"E:\Steam\steamapps\common\assettocorsa"
        };

        foreach (var p in commonPaths)
        {
            if (Directory.Exists(p)) return p;
        }

        return null;
    }

    static void PatchContentManager(string targetCmPath, string actoolsCompressedPath, string actoolsPatchedPath, string helperPath, string libDir)
    {
        Console.WriteLine("\n[1/2] Patching Content Manager: " + targetCmPath);

        string cmDir = Path.GetDirectoryName(targetCmPath);
        string origCmPath = Path.Combine(cmDir, "Content Manager.original.exe");

        if (!File.Exists(origCmPath))
        {
            Console.WriteLine("Creating original backup: " + origCmPath);
            File.Copy(targetCmPath, origCmPath);
        }

        var resolver = new CosturaAssemblyResolver();
        resolver.AddSearchDirectory(libDir);
        resolver.AddSearchDirectory(cmDir);
        resolver.AddSearchDirectory(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319");
        resolver.AddSearchDirectory(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF");
        resolver.AddSearchDirectory(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319");
        resolver.AddSearchDirectory(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\WPF");
        resolver.AddSearchDirectory(Path.GetTempPath());

        var readerParams = new ReaderParameters { AssemblyResolver = resolver };
        var cmAsm = AssemblyDefinition.ReadAssembly(origCmPath, readerParams);
        var mainMod = cmAsm.MainModule;
        resolver.SetModule(mainMod);
        var helperAsm = AssemblyDefinition.ReadAssembly(helperPath, readerParams);

        var oldRes = mainMod.Resources.FirstOrDefault(delegate (Resource r) { return r.Name == "costura.actools.dll.compressed"; });
        if (!(oldRes is EmbeddedResource))
            throw new Exception("Original Content Manager actools resource was not found.");

        Console.WriteLine("  Step 1: Patch only TrackdayProperties.SetSessions in the original actools resource...");
        byte[] originalResource = ((EmbeddedResource)oldRes).GetResourceData();
        byte[] actoolsBytes;
        string minimalActoolsPath = Path.Combine(Path.GetTempPath(), "trackday-timer-actools.dll");
        using (var input = new MemoryStream(originalResource, 0, originalResource.Length - 4))
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            deflate.CopyTo(output);
            output.Position = 0;
            var originalActools = AssemblyDefinition.ReadAssembly(output, readerParams);
            PatchTrackdaySetSessions(originalActools);
            originalActools.Write(minimalActoolsPath);
        }
        actoolsBytes = CompressActools(minimalActoolsPath, originalResource);
        mainMod.Resources.Remove(oldRes);
        mainMod.Resources.Add(new EmbeddedResource("costura.actools.dll.compressed", ManifestResourceAttributes.Public, actoolsBytes));
        Console.WriteLine("  Resource updated from original assembly. New size: " + actoolsBytes.Length + " bytes");

        Console.WriteLine("  Step 2: Modify QuickDrive_Trackday.ViewModel...");
        var qdTrackday = mainMod.Types.First(delegate (TypeDefinition t) { return t.Name == "QuickDrive_Trackday"; });
        var vm = qdTrackday.NestedTypes.First(delegate (TypeDefinition t) { return t.Name == "ViewModel"; });
        var saveableData = vm.NestedTypes.First(delegate (TypeDefinition t) { return t.Name == "SaveableData"; });

        // Add SaveableData.TrackdayDuration field
        var durationFieldSaveable = new FieldDefinition("TrackdayDuration", FieldAttributes.Public, mainMod.TypeSystem.Int32);
        saveableData.Fields.Add(durationFieldSaveable);

        // Add ViewModel._trackdayDuration field
        var durationField = new FieldDefinition("_trackdayDuration", FieldAttributes.Private, mainMod.TypeSystem.Int32);
        vm.Fields.Add(durationField);

        // Update ViewModel..ctor: initialize _trackdayDuration = 10
        var ctor = vm.Methods.First(delegate (MethodDefinition m) { return m.Name == ".ctor"; });
        var ctorIl = ctor.Body.GetILProcessor();
        var lastRet = ctor.Body.Instructions.Last(delegate (Instruction i) { return i.OpCode == OpCodes.Ret; });
        ctorIl.InsertBefore(lastRet, ctorIl.Create(OpCodes.Ldarg_0));
        ctorIl.InsertBefore(lastRet, ctorIl.Create(OpCodes.Ldc_I4_S, (sbyte)10));
        ctorIl.InsertBefore(lastRet, ctorIl.Create(OpCodes.Stfld, durationField));

        // Find references for getter / setter
        var actoolsAsm = AssemblyDefinition.ReadAssembly(minimalActoolsPath, readerParams);
        var mathUtilsType = actoolsAsm.MainModule.Types.First(delegate (TypeDefinition t) { return t.Name == "MathUtils"; });
        var clampIntMethod = mathUtilsType.Methods.First(delegate (MethodDefinition m) {
            return m.Name == "Clamp" && m.Parameters.Count == 3 && m.Parameters[0].ParameterType.Name == "Int32";
        });
        var clampIntMethodRef = mainMod.ImportReference(clampIntMethod);

        MethodReference onPropertyChangedRef = null;
        MethodReference saveLaterRef = null;

        TypeDefinition cur = vm;
        while (cur != null && cur.BaseType != null)
        {
            TypeDefinition resolved = cur.BaseType.Resolve();
            if (resolved != null)
            {
                foreach (var m in resolved.Methods)
                {
                    if (m.Name == "OnPropertyChanged" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "String")
                    {
                        onPropertyChangedRef = mainMod.ImportReference(m);
                    }
                    if (m.Name == "SaveLater" && m.Parameters.Count == 0)
                    {
                        saveLaterRef = mainMod.ImportReference(m);
                    }
                }
            }
            cur = resolved;
        }

        if (onPropertyChangedRef == null) throw new Exception("Could not find OnPropertyChanged method!");
        if (saveLaterRef == null) throw new Exception("Could not find SaveLater method!");

        // Create getter: get_TrackdayDuration()
        var getDurationMethod = new MethodDefinition("get_TrackdayDuration",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            mainMod.TypeSystem.Int32);
        {
            var il = getDurationMethod.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, durationField);
            il.Emit(OpCodes.Ret);
        }
        vm.Methods.Add(getDurationMethod);

        // Create setter: set_TrackdayDuration(int value)
        var setDurationMethod = new MethodDefinition("set_TrackdayDuration",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            mainMod.TypeSystem.Void);
        setDurationMethod.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, mainMod.TypeSystem.Int32));
        {
            var il = setDurationMethod.Body.GetILProcessor();
            var retTarget = il.Create(OpCodes.Ret);

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldc_I4, 180);
            il.Emit(OpCodes.Call, clampIntMethodRef);
            il.Emit(OpCodes.Starg_S, setDurationMethod.Parameters[0]);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, durationField);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Beq_S, retTarget);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, durationField);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, "TrackdayDuration");
            il.Emit(OpCodes.Callvirt, onPropertyChangedRef);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, saveLaterRef);

            il.Append(retTarget);
        }
        vm.Methods.Add(setDurationMethod);

        var propDuration = new PropertyDefinition("TrackdayDuration", PropertyAttributes.None, mainMod.TypeSystem.Int32);
        propDuration.GetMethod = getDurationMethod;
        propDuration.SetMethod = setDurationMethod;
        vm.Properties.Add(propDuration);

        // Update Save(SaveableData data)
        var saveMethod = vm.Methods.First(delegate (MethodDefinition m) { return m.Name == "Save" && m.Parameters.Count == 1; });
        {
            var il = saveMethod.Body.GetILProcessor();
            var lastRetSave = saveMethod.Body.Instructions.Last(delegate (Instruction i) { return i.OpCode == OpCodes.Ret; });
            var loadDataBeforeRet = lastRetSave.Previous;
            if (loadDataBeforeRet.OpCode == OpCodes.Ldarg_1)
            {
                il.InsertBefore(loadDataBeforeRet, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(loadDataBeforeRet, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(loadDataBeforeRet, il.Create(OpCodes.Call, getDurationMethod));
                il.InsertBefore(loadDataBeforeRet, il.Create(OpCodes.Stfld, durationFieldSaveable));
            }
        }

        // Update Load(SaveableData data)
        var loadMethod = vm.Methods.First(delegate (MethodDefinition m) { return m.Name == "Load" && m.Parameters.Count == 1; });
        {
            var il = loadMethod.Body.GetILProcessor();
            var lastRetLoad = loadMethod.Body.Instructions.Last(delegate (Instruction i) { return i.OpCode == OpCodes.Ret; });
            il.InsertBefore(lastRetLoad, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(lastRetLoad, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(lastRetLoad, il.Create(OpCodes.Ldfld, durationFieldSaveable));
            il.InsertBefore(lastRetLoad, il.Create(OpCodes.Call, setDurationMethod));
        }

        // Update GetModeProperties(IEnumerable<AiCar> botCars)
        var gameType = actoolsAsm.MainModule.Types.First(delegate (TypeDefinition t) { return t.Name == "Game"; });
        var baseModePropsType = gameType.NestedTypes.First(delegate (TypeDefinition t) { return t.Name == "BaseModeProperties"; });
        var durationFieldOnBase = baseModePropsType.Fields.First(delegate (FieldDefinition f) { return f.Name == "Duration"; });
        var durationFieldRef = mainMod.ImportReference(durationFieldOnBase);
        var getModePropsMethod = vm.Methods.First(delegate (MethodDefinition m) { return m.Name == "GetModeProperties"; });
        {
            var il = getModePropsMethod.Body.GetILProcessor();
            var lastRetGmp = getModePropsMethod.Body.Instructions.Last(delegate (Instruction i) { return i.OpCode == OpCodes.Ret; });
            il.InsertBefore(lastRetGmp, il.Create(OpCodes.Dup));
            il.InsertBefore(lastRetGmp, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(lastRetGmp, il.Create(OpCodes.Call, getDurationMethod));
            il.InsertBefore(lastRetGmp, il.Create(OpCodes.Conv_R8));
            il.InsertBefore(lastRetGmp, il.Create(OpCodes.Stfld, durationFieldRef));
        }

        Console.WriteLine("  Step 3: Clone EnsureDurationSlider into QuickDrive_Trackday...");
        var helperType = helperAsm.MainModule.Types.First(delegate (TypeDefinition t) { return t.Name == "TrackdayDurationHelper"; });
        var srcMethod = helperType.Methods.First(delegate (MethodDefinition m) { return m.Name == "EnsureDurationSlider"; });

        var ensureMethod = new MethodDefinition("EnsureDurationSlider", MethodAttributes.Private | MethodAttributes.HideBySig, mainMod.TypeSystem.Void);
        qdTrackday.Methods.Add(ensureMethod);

        var varMap = new Dictionary<VariableDefinition, VariableDefinition>();
        foreach (var v in srcMethod.Body.Variables)
        {
            var newVar = new VariableDefinition(mainMod.ImportReference(v.VariableType));
            ensureMethod.Body.Variables.Add(newVar);
            varMap[v] = newVar;
        }
        ensureMethod.Body.InitLocals = srcMethod.Body.InitLocals;

        var ensureIl = ensureMethod.Body.GetILProcessor();
        var instrMap = new Dictionary<Instruction, Instruction>();

        foreach (var instr in srcMethod.Body.Instructions)
        {
            Instruction newInstr = null;
            object op = instr.Operand;

            if (op == null)
            {
                newInstr = ensureIl.Create(instr.OpCode);
            }
            else if (op is ParameterDefinition)
            {
                newInstr = ensureIl.Create(OpCodes.Ldarg_0);
            }
            else if (op is VariableDefinition)
            {
                newInstr = ensureIl.Create(instr.OpCode, varMap[(VariableDefinition)op]);
            }
            else if (op is TypeReference)
            {
                newInstr = ensureIl.Create(instr.OpCode, mainMod.ImportReference((TypeReference)op));
            }
            else if (op is MethodReference)
            {
                newInstr = ensureIl.Create(instr.OpCode, mainMod.ImportReference((MethodReference)op));
            }
            else if (op is FieldReference)
            {
                newInstr = ensureIl.Create(instr.OpCode, mainMod.ImportReference((FieldReference)op));
            }
            else if (op is string)
            {
                newInstr = ensureIl.Create(instr.OpCode, (string)op);
            }
            else if (op is sbyte)
            {
                newInstr = ensureIl.Create(instr.OpCode, (sbyte)op);
            }
            else if (op is byte)
            {
                newInstr = ensureIl.Create(instr.OpCode, (byte)op);
            }
            else if (op is int)
            {
                newInstr = ensureIl.Create(instr.OpCode, (int)op);
            }
            else if (op is long)
            {
                newInstr = ensureIl.Create(instr.OpCode, (long)op);
            }
            else if (op is float)
            {
                newInstr = ensureIl.Create(instr.OpCode, (float)op);
            }
            else if (op is double)
            {
                newInstr = ensureIl.Create(instr.OpCode, (double)op);
            }
            else if (op is Instruction)
            {
                newInstr = ensureIl.Create(instr.OpCode, (Instruction)op);
            }
            else if (op is Instruction[])
            {
                newInstr = ensureIl.Create(instr.OpCode, (Instruction[])op);
            }
            else
            {
                throw new NotSupportedException("Unknown operand type: " + op.GetType());
            }

            instrMap[instr] = newInstr;
            ensureMethod.Body.Instructions.Add(newInstr);
        }

        foreach (var instr in srcMethod.Body.Instructions)
        {
            var newInstr = instrMap[instr];
            if (instr.Operand is Instruction)
            {
                newInstr.Operand = instrMap[(Instruction)instr.Operand];
            }
            else if (instr.Operand is Instruction[])
            {
                Instruction[] oldTargets = (Instruction[])instr.Operand;
                Instruction[] newTargets = new Instruction[oldTargets.Length];
                for (int ti = 0; ti < oldTargets.Length; ti++)
                {
                    newTargets[ti] = instrMap[oldTargets[ti]];
                }
                newInstr.Operand = newTargets;
            }
        }

        foreach (var eh in srcMethod.Body.ExceptionHandlers)
        {
            var newEh = new ExceptionHandler(eh.HandlerType);
            newEh.TryStart = instrMap[eh.TryStart];
            newEh.TryEnd = instrMap[eh.TryEnd];
            newEh.HandlerStart = instrMap[eh.HandlerStart];
            newEh.HandlerEnd = instrMap[eh.HandlerEnd];
            newEh.CatchType = eh.CatchType != null ? mainMod.ImportReference(eh.CatchType) : null;
            ensureMethod.Body.ExceptionHandlers.Add(newEh);
        }

        Console.WriteLine("  Step 4: Update QuickDrive_Trackday.OnLoaded...");
        var onLoadedMethod = qdTrackday.Methods.First(delegate (MethodDefinition m) { return m.Name == "OnLoaded"; });
        {
            var il = onLoadedMethod.Body.GetILProcessor();
            var getActualModelInstr = onLoadedMethod.Body.Instructions.First(delegate (Instruction i) {
                return i.OpCode == OpCodes.Call && i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "get_ActualModel";
            });
            var ldargBeforeActualModel = getActualModelInstr.Previous;
            il.InsertBefore(ldargBeforeActualModel, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ldargBeforeActualModel, il.Create(OpCodes.Call, ensureMethod));
        }

        // Clean out any accidental helper references
        var toRemove = mainMod.AssemblyReferences.Where(r => r.Name.ToLower().Contains("helper")).ToList();
        foreach (var r in toRemove) mainMod.AssemblyReferences.Remove(r);

        Console.WriteLine("  Step 5: Writing patched Content Manager.exe...");
        cmAsm.Write(targetCmPath);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  [SUCCESS] Content Manager successfully patched!");
        Console.ResetColor();
    }

    static void PatchTrackdaySetSessions(AssemblyDefinition actoolsAsm)
    {
        TypeDefinition trackday = null;
        foreach (var type in actoolsAsm.MainModule.Types)
        {
            trackday = type.NestedTypes.FirstOrDefault(delegate (TypeDefinition nested) {
                return nested.Name == "TrackdayProperties";
            });
            if (trackday != null) break;
        }
        if (trackday == null) throw new Exception("TrackdayProperties type was not found.");

        var method = trackday.Methods.FirstOrDefault(delegate (MethodDefinition m) {
            return m.Name == "SetSessions" && m.Parameters.Count == 1;
        });
        if (method == null) throw new Exception("TrackdayProperties.SetSessions was not found.");

        var durationInstruction = method.Body.Instructions.FirstOrDefault(delegate (Instruction i) {
            return i.OpCode == OpCodes.Ldc_I4 && i.Operand is int && (int)i.Operand == 720;
        });
        if (durationInstruction == null)
            throw new Exception("The native 720-minute Track Day duration was not found.");

        FieldDefinition durationField = null;
        TypeDefinition baseType = trackday.BaseType.Resolve();
        while (baseType != null && durationField == null)
        {
            durationField = baseType.Fields.FirstOrDefault(delegate (FieldDefinition f) {
                return f.Name == "Duration";
            });
            baseType = baseType.BaseType == null ? null : baseType.BaseType.Resolve();
        }
        if (durationField == null)
            throw new Exception("The native Track Day duration field was not found.");

        var il = method.Body.GetILProcessor();
        var boxInstruction = durationInstruction.Next;
        var fallback = il.Create(OpCodes.Ldc_I4, 720);
        var after = boxInstruction;
        var useDuration = il.Create(OpCodes.Ldarg_0);
        il.Replace(durationInstruction, useDuration);
        il.InsertAfter(useDuration, il.Create(OpCodes.Ldfld, durationField));
        il.InsertAfter(useDuration.Next, il.Create(OpCodes.Ldc_R8, 0.0));
        il.InsertAfter(useDuration.Next.Next, il.Create(OpCodes.Ble_Un_S, fallback));
        il.InsertAfter(useDuration.Next.Next.Next, il.Create(OpCodes.Ldarg_0));
        il.InsertAfter(useDuration.Next.Next.Next.Next, il.Create(OpCodes.Ldfld, durationField));
        il.InsertAfter(useDuration.Next.Next.Next.Next.Next, il.Create(OpCodes.Conv_I4));
        il.InsertAfter(useDuration.Next.Next.Next.Next.Next.Next, il.Create(OpCodes.Br_S, after));
        il.InsertBefore(boxInstruction, fallback);
    }

    static byte[] CompressActools(string path, byte[] originalResource)
    {
        byte[] assemblyBytes = File.ReadAllBytes(path);
        using (var output = new MemoryStream())
        {
            using (var deflate = new DeflateStream(output, CompressionMode.Compress, true))
            {
                deflate.Write(assemblyBytes, 0, assemblyBytes.Length);
            }
            byte[] compressed = output.ToArray();
            byte[] result = new byte[compressed.Length + 4];
            Buffer.BlockCopy(compressed, 0, result, 0, compressed.Length);
            Buffer.BlockCopy(originalResource, originalResource.Length - 4, result, compressed.Length, 4);
            return result;
        }
    }

    static void PatchAssettoCorsa(string acDir, string repoRoot)
    {
        Console.WriteLine("\n[2/2] Patching Assetto Corsa: " + acDir);

        string acsExePath = Path.Combine(acDir, "acs.exe");
        if (File.Exists(acsExePath))
        {
            string acsBackup = Path.Combine(acDir, "acs.original.exe");
            if (!File.Exists(acsBackup))
            {
                Console.WriteLine("Creating original backup: " + acsBackup);
                File.Copy(acsExePath, acsBackup);
            }

            Console.WriteLine("  - Patching acs.exe session-over string to 'TRACK DAY OVER'...");
            using (var fs = File.Open(acsExePath, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Seek(0x13826F, SeekOrigin.Begin);
                fs.WriteByte(14);

                fs.Seek(0x1383EA, SeekOrigin.Begin);
                fs.WriteByte(14);

                string newStr = "TRACK DAY OVER" + '\0' + '\0';
                byte[] newBytes = System.Text.Encoding.Unicode.GetBytes(newStr);
                fs.Seek(0x4CA5F0, SeekOrigin.Begin);
                fs.Write(newBytes, 0, newBytes.Length);
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [SUCCESS] acs.exe patched successfully!");
            Console.ResetColor();
        }

        string sourceLuaApp = Path.Combine(repoRoot, "apps", "lua", "TrackdayTimer");
        if (Directory.Exists(sourceLuaApp))
        {
            string targetLuaApp = Path.Combine(acDir, "apps", "lua", "TrackdayTimer");
            Console.WriteLine("  - Installing TrackdayTimer Lua app to: " + targetLuaApp);
            if (!Directory.Exists(targetLuaApp))
            {
                Directory.CreateDirectory(targetLuaApp);
            }

            foreach (var file in Directory.GetFiles(sourceLuaApp))
            {
                string targetFile = Path.Combine(targetLuaApp, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  [SUCCESS] TrackdayTimer Lua app installed successfully!");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  [WARNING] TrackdayTimer folder not found at " + sourceLuaApp);
            Console.ResetColor();
        }
    }
}
