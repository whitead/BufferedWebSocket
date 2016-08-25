using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BufferedWebSocket
{
    public class Metadata
    {
        public uint KEY_BYTES { get; set; } //number of bytes for a key
        public uint LEN_BYTES { get; set; } //number of bytes for length
        public string WS_URI { get; set; } //place to get websocket
        public uint MAX_BUFFER_SIZE { get; set; }//how big the buffers will need to be 
    }
}
