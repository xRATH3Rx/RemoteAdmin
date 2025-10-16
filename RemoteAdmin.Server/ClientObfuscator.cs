using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RemoteAdmin.Server.Build
{
    /// <summary>
    /// Simple obfuscator that renames types, methods, and fields to make reverse engineering harder.
    /// </summary>
    public class ClientObfuscator
    {
        
        private readonly AssemblyDefinition _assembly;
        private readonly Random _random;
        private readonly HashSet<string> _usedNames;

        public ClientObfuscator(AssemblyDefinition assembly)
        {
            _assembly = assembly;
            _random = new Random();
            _usedNames = new HashSet<string>();
        }

        public void Obfuscate()
        {
            foreach (var module in _assembly.Modules)
            {
                foreach (var type in module.Types.ToList())
                {
                    ObfuscateType(type);
                }
            }
        }

        private void ObfuscateType(TypeDefinition type)
        {
            // Don't obfuscate:
            // - Shared message classes (needed for communication)
            // - Entry point class
            // - Classes with special attributes
            if (ShouldSkipType(type))
                return;

            // Rename the type
            if (!type.IsNested)
            {
                type.Namespace = "";
                type.Name = GenerateRandomName();
            }

            // Obfuscate methods
            foreach (var method in type.Methods.ToList())
            {
                if (ShouldObfuscateMethod(method))
                {
                    method.Name = GenerateRandomName();
                }
            }

            // Obfuscate fields
            foreach (var field in type.Fields.ToList())
            {
                if (ShouldObfuscateField(field))
                {
                    field.Name = GenerateRandomName();
                }
            }

            // Obfuscate nested types
            foreach (var nestedType in type.NestedTypes.ToList())
            {
                ObfuscateType(nestedType);
            }
        }

        private bool ShouldSkipType(TypeDefinition type)
        {
            // Skip if namespace contains "Shared" (message classes)
            if (type.Namespace != null && type.Namespace.Contains("Shared"))
                return true;

            // Skip enums
            if (type.IsEnum)
                return true;

            // Skip if it has interfaces (might be required)
            if (type.HasInterfaces)
                return true;

            return false;
        }

        private bool ShouldObfuscateMethod(MethodDefinition method)
        {
            // Don't obfuscate constructors
            if (method.IsConstructor)
                return false;

            // Don't obfuscate entry point
            if (method.Name == "Main")
                return false;

            // Don't obfuscate special methods
            if (method.IsSpecialName)
                return false;

            // Don't obfuscate virtual or abstract methods
            if (method.IsVirtual || method.IsAbstract)
                return false;

            return true;
        }

        private bool ShouldObfuscateField(FieldDefinition field)
        {
            // Don't obfuscate special fields
            if (field.IsSpecialName)
                return false;

            return true;
        }

        private string GenerateRandomName()
        {
            string name;
            do
            {
                // Generate random string using invisible/confusing characters
                var sb = new StringBuilder();
                int length = _random.Next(10, 20);

                for (int i = 0; i < length; i++)
                {
                    // Use letters that look similar or are confusing
                    char[] confusingChars = {  'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                                               'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
                    sb.Append(confusingChars[_random.Next(confusingChars.Length)]);
                }

                name = sb.ToString();
            }
            while (_usedNames.Contains(name));

            _usedNames.Add(name);
            return name;
        }
    }
}