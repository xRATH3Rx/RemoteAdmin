using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Threading.Tasks;
using Microsoft.Win32;
using RemoteAdmin.Client.Networking;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Client.Handlers
{
    public class RegistryEditorHandler
    {
        private const int MAX_SUBKEYS_LIMIT = 5000; // Safety limit to prevent memory issues

        public static async Task HandleOpenRegistryEditor(SslStream stream)
        {
            // Send initial registry data with root keys
            var rootKeys = new List<RegistryKeyInfo>
            {
                new RegistryKeyInfo { Name = "HKEY_CLASSES_ROOT", SubKeyCount = 0, ValueCount = 0 },
                new RegistryKeyInfo { Name = "HKEY_CURRENT_USER", SubKeyCount = 0, ValueCount = 0 },
                new RegistryKeyInfo { Name = "HKEY_LOCAL_MACHINE", SubKeyCount = 0, ValueCount = 0 },
                new RegistryKeyInfo { Name = "HKEY_USERS", SubKeyCount = 0, ValueCount = 0 },
                new RegistryKeyInfo { Name = "HKEY_CURRENT_CONFIG", SubKeyCount = 0, ValueCount = 0 }
            };

            var response = new RegistryDataMessage
            {
                CurrentPath = "",
                SubKeys = rootKeys,
                Values = new List<RegistryValueInfo>(),
                Success = true
            };

            await NetworkHelper.SendMessageAsync(stream, response);
        }

        public static async Task HandleEnumerateRegistry(SslStream stream, RegistryEnumerateMessage message)
        {
            try
            {
                // Run registry enumeration in a background task to prevent blocking
                var result = await Task.Run(() => EnumerateRegistryKey(message.KeyPath));

                await NetworkHelper.SendMessageAsync(stream, result);
            }
            catch (Exception ex)
            {
                await SendError(stream, message.KeyPath, ex.Message);
            }
        }

        private static RegistryDataMessage EnumerateRegistryKey(string keyPath)
        {
            try
            {
                RegistryKey key = GetRegistryKey(keyPath);

                if (key == null)
                {
                    return new RegistryDataMessage
                    {
                        CurrentPath = keyPath,
                        SubKeys = new List<RegistryKeyInfo>(),
                        Values = new List<RegistryValueInfo>(),
                        Success = false,
                        ErrorMessage = "Unable to open registry key"
                    };
                }

                using (key)
                {
                    var subKeys = new List<RegistryKeyInfo>();
                    var values = new List<RegistryValueInfo>();

                    // Get subkeys with safety limit
                    try
                    {
                        string[] subKeyNames = key.GetSubKeyNames();

                        // Limit number of subkeys to prevent memory issues
                        int count = Math.Min(subKeyNames.Length, MAX_SUBKEYS_LIMIT);

                        for (int i = 0; i < count; i++)
                        {
                            string subKeyName = subKeyNames[i];
                            try
                            {
                                using (var subKey = key.OpenSubKey(subKeyName, false))
                                {
                                    if (subKey != null)
                                    {
                                        subKeys.Add(new RegistryKeyInfo
                                        {
                                            Name = subKeyName,
                                            SubKeyCount = SafeGetSubKeyCount(subKey),
                                            ValueCount = SafeGetValueCount(subKey)
                                        });
                                    }
                                    else
                                    {
                                        // Access denied, but still show the key
                                        subKeys.Add(new RegistryKeyInfo
                                        {
                                            Name = subKeyName,
                                            SubKeyCount = -1,
                                            ValueCount = -1
                                        });
                                    }
                                }
                            }
                            catch
                            {
                                // Access denied to this subkey, add it anyway
                                subKeys.Add(new RegistryKeyInfo
                                {
                                    Name = subKeyName,
                                    SubKeyCount = -1,
                                    ValueCount = -1
                                });
                            }
                        }

                        // If we hit the limit, add a warning
                        if (subKeyNames.Length > MAX_SUBKEYS_LIMIT)
                        {
                            Console.WriteLine($"Warning: Key has {subKeyNames.Length} subkeys, showing first {MAX_SUBKEYS_LIMIT}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error enumerating subkeys: {ex.Message}");
                    }

                    // Get values
                    try
                    {
                        foreach (string valueName in key.GetValueNames())
                        {
                            try
                            {
                                var valueKind = key.GetValueKind(valueName);
                                var valueData = key.GetValue(valueName);

                                values.Add(new RegistryValueInfo
                                {
                                    Name = string.IsNullOrEmpty(valueName) ? "(Default)" : valueName,
                                    ValueType = GetValueType(valueKind),
                                    Data = FormatValueData(valueData, valueKind)
                                });
                            }
                            catch
                            {
                                values.Add(new RegistryValueInfo
                                {
                                    Name = string.IsNullOrEmpty(valueName) ? "(Default)" : valueName,
                                    ValueType = RegistryValueType.String,
                                    Data = "(Error reading value)"
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error enumerating values: {ex.Message}");
                    }

                    return new RegistryDataMessage
                    {
                        CurrentPath = keyPath,
                        SubKeys = subKeys,
                        Values = values,
                        Success = true
                    };
                }
            }
            catch (Exception ex)
            {
                return new RegistryDataMessage
                {
                    CurrentPath = keyPath,
                    SubKeys = new List<RegistryKeyInfo>(),
                    Values = new List<RegistryValueInfo>(),
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static int SafeGetSubKeyCount(RegistryKey key)
        {
            try
            {
                return key.SubKeyCount;
            }
            catch
            {
                return -1;
            }
        }

        private static int SafeGetValueCount(RegistryKey key)
        {
            try
            {
                return key.ValueCount;
            }
            catch
            {
                return -1;
            }
        }

        public static async Task HandleCreateKey(SslStream stream, RegistryCreateKeyMessage message)
        {
            try
            {
                RegistryKey parentKey = GetRegistryKey(message.ParentPath, true);

                if (parentKey == null)
                {
                    await SendOperationResult(stream, false, RegistryOperation.CreateKey, "Unable to open parent key");
                    return;
                }

                using (parentKey)
                {
                    parentKey.CreateSubKey(message.KeyName);
                    await SendOperationResult(stream, true, RegistryOperation.CreateKey);
                }
            }
            catch (Exception ex)
            {
                await SendOperationResult(stream, false, RegistryOperation.CreateKey, ex.Message);
            }
        }

        public static async Task HandleDeleteKey(SslStream stream, RegistryDeleteKeyMessage message)
        {
            try
            {
                string parentPath = GetParentPath(message.KeyPath);
                string keyName = GetKeyName(message.KeyPath);

                RegistryKey parentKey = GetRegistryKey(parentPath, true);

                if (parentKey == null)
                {
                    await SendOperationResult(stream, false, RegistryOperation.DeleteKey, "Unable to open parent key");
                    return;
                }

                using (parentKey)
                {
                    parentKey.DeleteSubKeyTree(keyName);
                    await SendOperationResult(stream, true, RegistryOperation.DeleteKey);
                }
            }
            catch (Exception ex)
            {
                await SendOperationResult(stream, false, RegistryOperation.DeleteKey, ex.Message);
            }
        }

        public static async Task HandleSetValue(SslStream stream, RegistrySetValueMessage message)
        {
            try
            {
                RegistryKey key = GetRegistryKey(message.KeyPath, true);

                if (key == null)
                {
                    await SendOperationResult(stream, false, RegistryOperation.SetValue, "Unable to open registry key");
                    return;
                }

                using (key)
                {
                    RegistryValueKind valueKind = GetValueKind(message.ValueType);
                    object convertedData = ConvertValueData(message.ValueData, valueKind);

                    key.SetValue(message.ValueName, convertedData, valueKind);
                    await SendOperationResult(stream, true, RegistryOperation.SetValue);
                }
            }
            catch (Exception ex)
            {
                await SendOperationResult(stream, false, RegistryOperation.SetValue, ex.Message);
            }
        }

        public static async Task HandleDeleteValue(SslStream stream, RegistryDeleteValueMessage message)
        {
            try
            {
                RegistryKey key = GetRegistryKey(message.KeyPath, true);

                if (key == null)
                {
                    await SendOperationResult(stream, false, RegistryOperation.DeleteValue, "Unable to open registry key");
                    return;
                }

                using (key)
                {
                    key.DeleteValue(message.ValueName);
                    await SendOperationResult(stream, true, RegistryOperation.DeleteValue);
                }
            }
            catch (Exception ex)
            {
                await SendOperationResult(stream, false, RegistryOperation.DeleteValue, ex.Message);
            }
        }

        private static RegistryKey GetRegistryKey(string path, bool writable = false)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string[] parts = path.Split(new[] { '\\' }, 2);
            string hiveName = parts[0];
            string subKeyPath = parts.Length > 1 ? parts[1] : "";

            RegistryKey hive = GetHive(hiveName);
            if (hive == null)
                return null;

            if (string.IsNullOrEmpty(subKeyPath))
                return hive;

            try
            {
                // Use Registry64 view for 64-bit registry access
                var baseKey = RegistryKey.OpenBaseKey(GetRegistryHive(hiveName), RegistryView.Registry64);
                return baseKey.OpenSubKey(subKeyPath, writable);
            }
            catch
            {
                return null;
            }
        }

        private static RegistryKey GetHive(string hiveName)
        {
            switch (hiveName.ToUpper())
            {
                case "HKEY_CLASSES_ROOT":
                case "HKCR":
                    return Registry.ClassesRoot;
                case "HKEY_CURRENT_USER":
                case "HKCU":
                    return Registry.CurrentUser;
                case "HKEY_LOCAL_MACHINE":
                case "HKLM":
                    return Registry.LocalMachine;
                case "HKEY_USERS":
                case "HKU":
                    return Registry.Users;
                case "HKEY_CURRENT_CONFIG":
                case "HKCC":
                    return Registry.CurrentConfig;
                default:
                    return null;
            }
        }

        private static RegistryHive GetRegistryHive(string hiveName)
        {
            switch (hiveName.ToUpper())
            {
                case "HKEY_CLASSES_ROOT":
                case "HKCR":
                    return RegistryHive.ClassesRoot;
                case "HKEY_CURRENT_USER":
                case "HKCU":
                    return RegistryHive.CurrentUser;
                case "HKEY_LOCAL_MACHINE":
                case "HKLM":
                    return RegistryHive.LocalMachine;
                case "HKEY_USERS":
                case "HKU":
                    return RegistryHive.Users;
                case "HKEY_CURRENT_CONFIG":
                case "HKCC":
                    return RegistryHive.CurrentConfig;
                default:
                    return RegistryHive.CurrentUser;
            }
        }

        private static RegistryValueType GetValueType(RegistryValueKind kind)
        {
            switch (kind)
            {
                case RegistryValueKind.String:
                    return RegistryValueType.String;
                case RegistryValueKind.ExpandString:
                    return RegistryValueType.ExpandString;
                case RegistryValueKind.Binary:
                    return RegistryValueType.Binary;
                case RegistryValueKind.DWord:
                    return RegistryValueType.DWord;
                case RegistryValueKind.MultiString:
                    return RegistryValueType.MultiString;
                case RegistryValueKind.QWord:
                    return RegistryValueType.QWord;
                default:
                    return RegistryValueType.String;
            }
        }

        private static RegistryValueKind GetValueKind(RegistryValueType valueType)
        {
            switch (valueType)
            {
                case RegistryValueType.String:
                    return RegistryValueKind.String;
                case RegistryValueType.ExpandString:
                    return RegistryValueKind.ExpandString;
                case RegistryValueType.Binary:
                    return RegistryValueKind.Binary;
                case RegistryValueType.DWord:
                    return RegistryValueKind.DWord;
                case RegistryValueType.MultiString:
                    return RegistryValueKind.MultiString;
                case RegistryValueType.QWord:
                    return RegistryValueKind.QWord;
                default:
                    return RegistryValueKind.String;
            }
        }

        private static object FormatValueData(object data, RegistryValueKind kind)
        {
            if (data == null)
                return "(value not set)";

            switch (kind)
            {
                case RegistryValueKind.Binary:
                    byte[] bytes = data as byte[];
                    if (bytes != null && bytes.Length > 0)
                        return BitConverter.ToString(bytes).Replace("-", " ");
                    return "(zero-length binary value)";

                case RegistryValueKind.MultiString:
                    string[] strings = data as string[];
                    if (strings != null)
                        return string.Join("\n", strings);
                    return data.ToString();

                case RegistryValueKind.DWord:
                    uint dwordValue = (uint)(int)data;
                    return $"0x{dwordValue:x8} ({dwordValue})";

                case RegistryValueKind.QWord:
                    ulong qwordValue = (ulong)(long)data;
                    return $"0x{qwordValue:x16} ({qwordValue})";

                default:
                    return data;
            }
        }

        private static object ConvertValueData(object data, RegistryValueKind kind)
        {
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    return Convert.ToInt32(data);

                case RegistryValueKind.QWord:
                    return Convert.ToInt64(data);

                case RegistryValueKind.Binary:
                    if (data is string str)
                    {
                        // Convert space-separated hex string to byte array
                        string[] hexValues = str.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        return hexValues.Select(h => Convert.ToByte(h, 16)).ToArray();
                    }
                    return data;

                case RegistryValueKind.MultiString:
                    if (data is string multiStr)
                        return multiStr.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    return data;

                default:
                    return data?.ToString() ?? "";
            }
        }

        private static string GetParentPath(string fullPath)
        {
            int lastSeparator = fullPath.LastIndexOf('\\');
            return lastSeparator > 0 ? fullPath.Substring(0, lastSeparator) : "";
        }

        private static string GetKeyName(string fullPath)
        {
            int lastSeparator = fullPath.LastIndexOf('\\');
            return lastSeparator >= 0 ? fullPath.Substring(lastSeparator + 1) : fullPath;
        }

        private static async Task SendError(SslStream stream, string path, string errorMessage)
        {
            var response = new RegistryDataMessage
            {
                CurrentPath = path,
                SubKeys = new List<RegistryKeyInfo>(),
                Values = new List<RegistryValueInfo>(),
                Success = false,
                ErrorMessage = errorMessage
            };

            await NetworkHelper.SendMessageAsync(stream, response);
        }

        private static async Task SendOperationResult(SslStream stream, bool success, RegistryOperation operation, string errorMessage = null)
        {
            var response = new RegistryOperationResultMessage
            {
                Success = success,
                Operation = operation,
                ErrorMessage = errorMessage
            };

            await NetworkHelper.SendMessageAsync(stream, response);
        }
    }
}