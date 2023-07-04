using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TradeDataCore.Importing;
public class WebDownloader
{
    public async Task Download(string url, string filePath)
    {
        HttpClient client = new HttpClient();
        client.BaseAddress = new Uri(url);
        using var stream = await client.GetStreamAsync(url).ConfigureAwait(false);
        using var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

        byte[] buffer = new byte[1024];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            fileStream.Write(buffer, 0, bytesRead);
    }
}
