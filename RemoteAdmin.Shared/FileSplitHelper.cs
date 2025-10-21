using System;
using System.Collections.Generic;
using System.IO;

namespace RemoteAdmin.Shared
{
    public class FileSplitHelper
    {
        public const int MaxChunkSize = 65535; // 64KB chunks

        public static IEnumerable<FileChunk> ReadFileChunks(string filePath)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                long totalSize = fileStream.Length;
                long currentOffset = 0;

                while (currentOffset < totalSize)
                {
                    long remainingSize = totalSize - currentOffset;
                    int chunkSize = (int)Math.Min(remainingSize, MaxChunkSize);

                    byte[] buffer = new byte[chunkSize];
                    int bytesRead = fileStream.Read(buffer, 0, chunkSize);

                    if (bytesRead > 0)
                    {
                        yield return new FileChunk
                        {
                            Data = buffer,
                            Offset = currentOffset
                        };

                        currentOffset += bytesRead;
                    }
                }
            }
        }

        public static void WriteFileChunk(string filePath, FileChunk chunk)
        {
            using (var fileStream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                fileStream.Seek(chunk.Offset, SeekOrigin.Begin);
                fileStream.Write(chunk.Data, 0, chunk.Data.Length);
            }
        }
    }
}