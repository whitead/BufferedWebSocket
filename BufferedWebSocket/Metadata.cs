using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BufferedWebSockets
{
    public class Metadata
    {
        public uint key_bytes { get; set; } //number of bytes for a key
        public uint len_bytes { get; set; } //number of bytes for length
        public string ws_url { get; set; } //place to get websocket
        public uint max_buffer_size { get; set; }//how big the buffers will need to be (in bytes!)        
    }
}
