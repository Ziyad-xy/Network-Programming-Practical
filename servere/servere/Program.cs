using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace servere
{
    class Program
    {
        static async Task Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();
            Console.WriteLine("Server listening on 8080");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[8];
                await stream.ReadExactlyAsync(buffer, 0, 8);
                long size = BitConverter.ToInt64(buffer, 0);
                Console.WriteLine($"Expecting file of {size} bytes...");

                byte[] data = new byte[size];
                int received = 0;
                while (received < size)
                {
                    int chunk = await stream.ReadAsync(data, received, (int)size - received);
                    if (chunk == 0) throw new Exception("Connection closed before file was fully received.");
                    received += chunk;
                }
                Console.WriteLine("File received. Compressing...");

                byte[] compressed;
                using (MemoryStream memory = new MemoryStream())
                {
                    using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, leaveOpen: true))
                    {
                        await gzip.WriteAsync(data, 0, data.Length);
                    }
                    compressed = memory.ToArray();
                }
                Console.WriteLine($"Compressed: {size} → {compressed.Length} bytes");

                byte[] prefix = BitConverter.GetBytes((long)compressed.Length);
                await stream.WriteAsync(prefix, 0, 8);

                await stream.WriteAsync(compressed, 0, compressed.Length);
                Console.WriteLine("Compressed file sent to client.");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Error: {exception.Message}");
            }
            finally
            {
                client.Close();
            }
        }
    }
}