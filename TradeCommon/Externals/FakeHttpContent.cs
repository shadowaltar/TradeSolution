﻿using System.Net;
using System.Text;

namespace TradeCommon.Externals;

public class FakeHttpContent : HttpContent
{
    public string HardcodeContent { get; set; } = "{}";

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

    public new async Task<string> ReadAsStringAsync()
    {
        return HardcodeContent;
    }
}