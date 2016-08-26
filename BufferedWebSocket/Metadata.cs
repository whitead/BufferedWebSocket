using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace BufferedWebSockets
{
    public class Metadata
    {
        public uint key_bytes { get; set; } //number of bytes for a key
        public uint len_bytes { get; set; } //number of bytes for length
        public string ws_url { get; set; } //place to get websocket
        public uint max_buffer_size { get; set; }//how big the buffers will need to be (in bytes!)        

        public virtual void Parse(string json)
        {
            JsonObject root = JsonValue.Parse(json).GetObject();
            key_bytes = (uint)root.GetNamedNumber("key_bytes");
            len_bytes = (uint)root.GetNamedNumber("len_bytes");
            max_buffer_size = (uint)root.GetNamedNumber("max_buffer_size");
            ws_url = root.GetNamedString("ws_url");
        }
    }
}
