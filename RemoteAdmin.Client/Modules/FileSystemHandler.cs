using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Modules
{
    internal class FileSystemHandler
    {
        public static Dictionary<string, string> activeUploads = new Dictionary<string, string>();
        public static async Task HandleDirectoryListRequest(Stream stream, DirectoryListRequestMessage request)
        {
            try
            {
                var items = new List<FileSystemItem>();
                var dirInfo = new DirectoryInfo(request.Path);

                foreach (var dir in dirInfo.GetDirectories())
                {
                    try
                    {
                        items.Add(new FileSystemItem
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            IsDirectory = true,
                            Size = 0,
                            LastModified = dir.LastWriteTime
                        });
                    }
                    catch { }
                }

                foreach (var file in dirInfo.GetFiles())
                {
                    try
                    {
                        items.Add(new FileSystemItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            IsDirectory = false,
                            Size = file.Length,
                            LastModified = file.LastWriteTime
                        });
                    }
                    catch { }
                }

                var response = new DirectoryListResponseMessage
                {
                    Path = request.Path,
                    Items = items,
                    Success = true
                };

                await NetworkHelper.SendMessageAsync(stream, response);
                Console.WriteLine($"Sent {items.Count} items for directory: {request.Path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing directory: {ex.Message}");

                var response = new DirectoryListResponseMessage
                {
                    Path = request.Path,
                    Items = new List<FileSystemItem>(),
                    Success = false,
                    Error = ex.Message
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async Task HandleDownloadRequest(Stream stream, DownloadFileRequestMessage request)
        {
            try
            {
                var fileInfo = new FileInfo(request.FilePath);
                int chunkNumber = 0;
                long totalChunks = (fileInfo.Length / FileSplitHelper.MaxChunkSize) + 1;

                foreach (var chunk in FileSplitHelper.ReadFileChunks(request.FilePath))
                {
                    chunkNumber++;
                    bool isLast = chunkNumber >= totalChunks;

                    var chunkMsg = new FileChunkMessage
                    {
                        TransferId = request.TransferId,
                        FileName = fileInfo.Name,
                        FileSize = fileInfo.Length,
                        Offset = chunk.Offset,
                        Data = chunk.Data,
                        IsLastChunk = isLast
                    };

                    await NetworkHelper.SendMessageAsync(stream, chunkMsg);
                }

                Console.WriteLine($"Sent {chunkNumber} chunks for file: {request.FilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file: {ex.Message}");
            }
        }

        public static async Task HandleFileChunk(Stream stream, FileChunkMessage chunk)
        {
            try
            {
                if (!activeUploads.TryGetValue(chunk.TransferId, out string filePath))
                {
                    Console.WriteLine($"Warning: Unknown upload transfer ID: {chunk.TransferId}");
                    return;
                }

                FileSplitHelper.WriteFileChunk(filePath, new FileChunk
                {
                    Data = chunk.Data,
                    Offset = chunk.Offset
                });

                if (chunk.IsLastChunk)
                {
                    activeUploads.Remove(chunk.TransferId);
                    Console.WriteLine($"Upload complete: {filePath}");

                    var response = new OperationResultMessage
                    {
                        Success = true,
                        Message = "File uploaded successfully"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing file chunk: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error uploading file: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async Task HandleDeleteFile(Stream stream, DeleteFileMessage deleteMsg)
        {
            try
            {
                if (deleteMsg.IsDirectory)
                {
                    Directory.Delete(deleteMsg.Path, true);
                }
                else
                {
                    File.Delete(deleteMsg.Path);
                }

                var response = new OperationResultMessage
                {
                    Success = true,
                    Message = "Deleted successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error deleting: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async Task HandleRenameFile(Stream stream, RenameFileMessage renameMsg)
        {
            try
            {
                string directory = Path.GetDirectoryName(renameMsg.OldPath);
                string newPath = Path.Combine(directory, renameMsg.NewName);

                if (Directory.Exists(renameMsg.OldPath))
                {
                    Directory.Move(renameMsg.OldPath, newPath);
                }
                else if (File.Exists(renameMsg.OldPath))
                {
                    File.Move(renameMsg.OldPath, newPath);
                }

                var response = new OperationResultMessage
                {
                    Success = true,
                    Message = "Renamed successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error renaming: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async Task HandleCreateDirectory(Stream stream, CreateDirectoryMessage createMsg)
        {
            try
            {
                Directory.CreateDirectory(createMsg.Path);

                var response = new OperationResultMessage
                {
                    Success = true,
                    Message = "Directory created successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating directory: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error creating directory: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

    }
}
