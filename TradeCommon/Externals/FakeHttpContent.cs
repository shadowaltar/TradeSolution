using System.Net;
using static OfficeOpenXml.ExcelErrorValue;
using System.Text.Json;
using System.Xml;
using System.Text;

namespace TradeCommon.Externals;

public class FakeHttpContent : HttpContent
{
    public string HardcodeContent { get; set; } = "{}";

    private readonly MemoryStream _Stream = new MemoryStream();

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        if (HardcodeContent == null)
            return;
        byte[] byteArray = Encoding.UTF8.GetBytes(HardcodeContent);
        stream = new MemoryStream(byteArray);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        if (HardcodeContent == null) return false;
        length = HardcodeContent.Length;
        return true;
    }

    public async Task<string> ReadAsStringAsync()
    {
        return HardcodeContent;
    }
}