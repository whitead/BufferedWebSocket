using System;
using System.Collections.Generic;
using Windows.Data.Json;

namespace BufferedWebSockets
{
    public class Siminfo : Metadata
    {
        //use naming convention of C# instead of 
        //underlying JSON since these are meant to be accessed
        public List<string> Elements { get; set; }
        public ulong FrameNumber { get; set;}
        public ulong AtomNumber { get; set; }

        public override void Parse(string json)
        {
            base.Parse(json);
            JsonObject root = JsonValue.Parse(json).GetObject();
            FrameNumber = (uint)root.GetNamedNumber("frame_number");
            AtomNumber = (uint)root.GetNamedNumber("atom_number");
            JsonArray array = root.GetNamedArray("elements");
            Elements = new List<string>();
            foreach (var v in array)
            {
                Elements.Add(v.GetString());
            }
        }

    }

}
