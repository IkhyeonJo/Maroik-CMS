using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebSite.Contracts;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace Maroik.WebSite.Services
{
    public class ClamavRepository : IClamavRepository
    {
        private readonly string _clamavStorageDomain = "Maroik.Clamav";

        public async Task<bool> ScanWithClamavAsync(Stream fileStream)
        {
            try
            {
                fileStream.Position = 0;

                using var client = new TcpClient(_clamavStorageDomain, 3310);
                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync("nINSTREAM");

                byte[] buffer = new byte[2048];
                int bytesRead;
                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    byte[] lengthPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytesRead));
                    await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
                    await stream.WriteAsync(buffer, 0, bytesRead);
                }

                byte[] zero = BitConverter.GetBytes(0);
                await stream.WriteAsync(zero, 0, zero.Length);

                using var reader = new StreamReader(stream);
                string response = await reader.ReadLineAsync() ?? string.Empty;

                return response.Contains("OK");
            }
            catch
            {
                return false;
            }
        }
    }
}