using System;
using System.Collections.Generic;

namespace BufferedWebSockets
{
    public class Siminfo : Metadata
    {
        public IList<string> Elements { get; set; }
        public ulong FrameNumber { get; set;}
    }

    public class SimWebSocket : BufferedWebSocket<Siminfo>
    {
        public SimWebSocket(string uri, uint bufferNumber) : base(uri, bufferNumber)
        {
        }
    }
}
