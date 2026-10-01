using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

class FullPatcher
{
    static int Main(string[] args)
    {
        try
        {
            string binDir = @"C:\Users\jred8\AppData\Local\Temp\cm_inspect";
            string origCmPath = @"C:\Users\jred8\OneDrive\Desktop\Content Manager.original.exe";
            string targetCmPath = @"C:\Users\jred8\OneDrive\Desktop\Content Manager.exe";
            string actoolsCompressedPath = Path.Combine(binDir, "actools_compressed.bin");
            string helperPath = Path.Combine(binDir, "TrackdayHelper.dll");

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(binDir);
            resolver.AddSearchDirectory(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319");
            resolver.AddSearchDirectory(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF");

            var readerParams = new ReaderParameters { AssemblyResolver = resolver };
            var cmAsm = AssemblyDefinition.ReadAssembly(origCmPath, readerParams);
            var helperAsm = AssemblyDefinition.ReadAssembly(helperPath, readerParams);

            var mainMod = cmAsm.MainModule;

            Console.WriteLine("Step 1: Replace costura.actools.dll.compressed...");
            byte[] actoolsBytes = File.ReadAllBytes(actoolsCompressedPath);
            var oldRes = mainMod.Resources.FirstOrDefault(delegate(Resource r) { return r.Name == "costura.actools.dll.compressed"; });
            if (oldRes != null)
            {
                mainMod.Resources.Remove(oldRes);
            }
            var newRes = new EmbeddedResource("costura.actools.dll.compressed", ManifestResourceAttributes.Public, actoolsBytes);
            mainMod.Resources.Add(newRes);
            Console.WriteLine("Resource updated. New size: " + actoolsBytes.Length);

            Console.WriteLine("Step 2: Modify QuickDrive_Trackday.ViewModel...");
            var qdTrackday = mainMod.Types.First(delegate(TypeDefinition t) { return t.Name == "QuickDrive_Trackday"; });
            var vm = qdTrackday.NestedTypes.First(delegate(TypeDefinition t) { return t.Name == "ViewModel"; });
            var saveableData = vm.NestedTypes.First(delegate(TypeDefinition t) { return t.Name == "SaveableData"; });

            // Add SaveableData.TrackdayDuration field
            var durationFieldSaveable = new FieldDefinition("TrackdayDuration", FieldAttributes.Public, mainMod.TypeSystem.Int32);
            saveableData.Fields.Add(durationFieldSaveable);

            // Add ViewModel._trackdayDuration field
            var durationField = new FieldDefinition("_trackdayDuration", FieldAttributes.Private, mainMod.TypeSystem.Int32);
            vm.Fields.Add(durationField);

            // Update ViewModel..ctor: initialize _trackdayDuration = 10
            var ctor = vm.Methods.First(delegate(MethodDefinition m) { return m.Name == ".ctor"; });
            var ctorIl = ctor.Body.GetILProcessor();
            var lastRet = ctor.Body.Instructions.Last(delegate(Instruction i) { return i.OpCode == OpCodes.Ret; });
            ctorIl.InsertBefore(lastRet, ctorIl.Create(OpCodes.Ldarg_0));
            ctorIl.InsertBefore(lastRet, ctorIl.Create(OpCodes.Ldc_I4_S, (sbyte)10));
            ctorIl.InsertBefore(lastRet, ctorIl.Create(OpCodes.Stfld, durationField));

            // Find references for getter / setter
            // MathUtils.Clamp(int, int, int)
            var actoolsAsm = AssemblyDefinition.ReadAssembly(Path.Combine(binDir, "actools_patched.dll"), readerParams);
            var mathUtilsType = actoolsAsm.MainModule.Types.First(delegate(TypeDefinition t) { return t.Name == "MathUtils"; });
            var clampIntMethod = mathUtilsType.Methods.First(delegate(MethodDefinition m) {
                return m.Name == "Clamp" && m.Parameters.Count == 3 && m.Parameters[0].ParameterType.Name == "Int32";
            });
            var clampIntMethodRef = mainMod.ImportReference(clampIntMethod);

            // NotifyPropertyChanged.OnPropertyChanged(string)
            // Look up in existing references or base types
            MethodReference onPropertyChangedRef = null;
            MethodReference saveLaterRef = null;

            // Find base types
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
            Console.WriteLine("Found OnPropertyChanged and SaveLater references.");

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

            // Create PropertyDefinition
            var propDuration = new PropertyDefinition("TrackdayDuration", PropertyAttributes.None, mainMod.TypeSystem.Int32);
            propDuration.GetMethod = getDurationMethod;
            propDuration.SetMethod = setDurationMethod;
            vm.Properties.Add(propDuration);

            // Update Save(SaveableData data)
            var saveMethod = vm.Methods.First(delegate(MethodDefinition m) { return m.Name == "Save" && m.Parameters.Count == 1; });
            {
                var il = saveMethod.Body.GetILProcessor();
                var lastRetSave = saveMethod.Body.Instructions.Last(delegate(Instruction i) { return i.OpCode == OpCodes.Ret; });
                // In Save: before returning data, insert data.TrackdayDuration = this.TrackdayDuration
                // Find instruction that loads data before ret (usually ldarg.1)
                var loadDataBeforeRet = lastRetSave.Previous;
                if (loadDataBeforeRet.OpCode == OpCodes.Ldarg_1)
                {
                    il.InsertBefore(loadDataBeforeRet, il.Create(OpCodes.Ldarg_1));
                    il.InsertBefore(loadDataBeforeRet, il.Create(OpCodes.Ldarg_0));
                    il.InsertBefore(loadDataBeforeRet, il.Create(OpCodes.Call, getDurationMethod));
                    il.InsertBefore(loadDataBeforeRet, il.Create(OpCodes.Stfld, durationFieldSaveable));
                }
                else
                {
                    throw new Exception("Unexpected Save IL structure!");
                }
            }
            Console.WriteLine("Save method updated.");

            // Update Load(SaveableData data)
            var loadMethod = vm.Methods.First(delegate(MethodDefinition m) { return m.Name == "Load" && m.Parameters.Count == 1; });
            {
                var il = loadMethod.Body.GetILProcessor();
                var lastRetLoad = loadMethod.Body.Instructions.Last(delegate(Instruction i) { return i.OpCode == OpCodes.Ret; });
                il.InsertBefore(lastRetLoad, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(lastRetLoad, il.Create(OpCodes.Ldarg_1));
                il.InsertBefore(lastRetLoad, il.Create(OpCodes.Ldfld, durationFieldSaveable));
                il.InsertBefore(lastRetLoad, il.Create(OpCodes.Call, setDurationMethod));
            }
            Console.WriteLine("Load method updated.");

            // Update GetModeProperties(IEnumerable<AiCar> botCars)
            // Duration field is on BaseModeProperties
            var gameType = actoolsAsm.MainModule.Types.First(delegate(TypeDefinition t) { return t.Name == "Game"; });
            var baseModePropsType = gameType.NestedTypes.First(delegate(TypeDefinition t) { return t.Name == "BaseModeProperties"; });
            var durationFieldOnBase = baseModePropsType.Fields.First(delegate(FieldDefinition f) { return f.Name == "Duration"; });
            var durationFieldRef = mainMod.ImportReference(durationFieldOnBase);

            var getModePropsMethod = vm.Methods.First(delegate(MethodDefinition m) { return m.Name == "GetModeProperties"; });
            {
                var il = getModePropsMethod.Body.GetILProcessor();
                var lastRetGmp = getModePropsMethod.Body.Instructions.Last(delegate(Instruction i) { return i.OpCode == OpCodes.Ret; });
                il.InsertBefore(lastRetGmp, il.Create(OpCodes.Dup));
                il.InsertBefore(lastRetGmp, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(lastRetGmp, il.Create(OpCodes.Call, getDurationMethod));
                il.InsertBefore(lastRetGmp, il.Create(OpCodes.Conv_R8));
                il.InsertBefore(lastRetGmp, il.Create(OpCodes.Stfld, durationFieldRef));
            }
            Console.WriteLine("GetModeProperties method updated.");

            Console.WriteLine("Step 3: Clone EnsureDurationSlider into QuickDrive_Trackday...");
            var helperType = helperAsm.MainModule.Types.First(delegate(TypeDefinition t) { return t.Name == "TrackdayDurationHelper"; });
            var srcMethod = helperType.Methods.First(delegate(MethodDefinition m) { return m.Name == "EnsureDurationSlider"; });

            var ensureMethod = new MethodDefinition("EnsureDurationSlider", MethodAttributes.Private | MethodAttributes.HideBySig, mainMod.TypeSystem.Void);
            qdTrackday.Methods.Add(ensureMethod);

            // Map variables
            var varMap = new Dictionary<VariableDefinition, VariableDefinition>();
            foreach (var v in srcMethod.Body.Variables)
            {
                var newVar = new VariableDefinition(mainMod.ImportReference(v.VariableType));
                ensureMethod.Body.Variables.Add(newVar);
                varMap[v] = newVar;
            }
            ensureMethod.Body.InitLocals = srcMethod.Body.InitLocals;

            // Map instructions
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
                    // In helper, p was arg 0 (trackdayControl). In instance method, 'this' is arg 0 (Ldarg_0).
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

            // Fix branch targets
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

            // Clone exception handlers
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
            Console.WriteLine("EnsureDurationSlider cloned successfully!");

            Console.WriteLine("Step 4: Update QuickDrive_Trackday.OnLoaded...");
            var onLoadedMethod = qdTrackday.Methods.First(delegate(MethodDefinition m) { return m.Name == "OnLoaded"; });
            {
                var il = onLoadedMethod.Body.GetILProcessor();
                // Find instruction calling ActualModel.Load()
                // In original:
                // 0009: ldarg.0
                // 000A: ldc.i4.1
                // 000B: stfld _loaded
                // 0010: ldarg.0
                // 0011: call get_ActualModel()
                // 0016: callvirt Load()
                // 001B: ret
                // We insert right before 0010 (ldarg.0 before get_ActualModel):
                // ldarg.0
                // call EnsureDurationSlider()
                var getActualModelInstr = onLoadedMethod.Body.Instructions.First(delegate(Instruction i) {
                    return i.OpCode == OpCodes.Call && i.Operand is MethodReference && ((MethodReference)i.Operand).Name == "get_ActualModel";
                });
                var ldargBeforeActualModel = getActualModelInstr.Previous;

                il.InsertBefore(ldargBeforeActualModel, il.Create(OpCodes.Ldarg_0));
                il.InsertBefore(ldargBeforeActualModel, il.Create(OpCodes.Call, ensureMethod));
            }
            Console.WriteLine("OnLoaded method updated.");

            Console.WriteLine("Step 5: Verifying assembly references...");
            Console.WriteLine("Assembly reference count: " + mainMod.AssemblyReferences.Count);
            foreach (var ar in mainMod.AssemblyReferences)
            {
                if (ar.Name.ToLower().Contains("helper"))
                {
                    throw new Exception("Unexpected external reference found: " + ar.FullName);
                }
            }

            Console.WriteLine("Step 6: Writing patched Content Manager.exe...");
            cmAsm.Write(targetCmPath);
            Console.WriteLine("Successfully wrote to: " + targetCmPath);
            Console.WriteLine("Output size: " + new FileInfo(targetCmPath).Length + " bytes");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex);
            return 1;
        }
    }
}
