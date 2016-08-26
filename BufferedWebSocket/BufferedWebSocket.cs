using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BufferedWebSockets
{
    /*
     * This class provides pre-fetching buffering from a websocket. There needs to be a particular format
     * for the remote server as it prepares packets. The data usually represented are frames from a time
     * dependent process, however it may be spatial as well. Each buffer coming from the server has an 8 (can change) byte
     * key associated with it. This could be time, for example, or xyz coordinates. 
     * The buffer itself contains, optionally, a variable amount of data. The data will always 
     * be transferred as uncompressed binary, to reduce load on the client.
     * 
     * The pre-fetching is specified by the user of this class. For example, if the user is moving forwards in time the
     * next keys would be sequential. 
     * 
     * Setting this up requires some communication with the server, which specifies the key size and sets-up the socket. 
     * It is possible to use this communication to get additional data from the server, which you have implemented. For example,
     * the number of keys possible or any other static data. This can be done by using optional template parameter with your 
     * own class the derives from BufferedWebSocket.Metadata.
     * 
     * 
     */
    public class BufferedWebSocket<T> where T : Metadata, new()
    {
        private uint bufferNumber;
        private byte[][] buffers;
        private uint[] sizes;
        private ulong[] keys;
        private int[] keyLives;
        private bool[] loaded;
        private bool[] read;
        private T info;
        private MessageWebSocket ws;
        private Uri initiateURI;
        public event EventHandler<ulong> KeyLoadedEvent;

        public int ReadyUnreadBuffers
        {
            get
            {
                int sum = 0;
                for (uint i = 0; i < bufferNumber; i++)
                    sum += !read[i] && loaded[i] ? 1 : 0;
                return sum;
            }
        }

        public BufferedWebSocket(string uri, uint bufferNumber) {

            if (bufferNumber == 0)
                throw new Exception("Must have at least one buffer");

            initiateURI = new Uri(uri);
            this.bufferNumber = bufferNumber;
            buffers = new byte[bufferNumber][];
            loaded = new bool[bufferNumber];
            read = new bool[bufferNumber];
            keyLives = new int[bufferNumber];
            keys = new ulong[bufferNumber];
            sizes = new uint[bufferNumber];
        }


        /*
         * Returns a buffer or null if it's not ready. Frees it if return succeeds.
         * 
         */
        public uint Get(ulong key)
        {
            byte[] b;
            return Get(key, out b);
        }

        public uint Get(uint key, out byte[] b)
        {
            return Get((ulong) key, out b);
        }


        public uint Get(ulong key, out byte[] b)
        {
            b = null;
            for (uint i = 0; i < keys.Length; i++)
            {
                if (key == keys[i] && loaded[i])
                {
                    read[i] = true;
                    b = buffers[i];
                    return sizes[i];

                }
            }

            return 0;
        }

        public uint GetFloats(uint key, ref float[] buffer) {
            return GetFloats((ulong) key, ref buffer);
        }

        public uint GetFloats(ulong key, ref float[] buffer)
        {
            byte[] b;
            uint N = Get(key, out b);
            if (b != null)
            {
                System.Buffer.BlockCopy(b, 0, buffer, 0, (int)N);
            }
            return N;
        }


        public void Free(ulong key)
        {
            for (uint i = 0; i < keys.Length; i++)
            {
                if (key == keys[i])
                {
                    read[i] = true;
                }
            }
        }

        public async Task Request(uint key)
        {
            await Request((ulong) key);
        }
            

        public async Task Request(ulong key)
        {            
            //3 possibilities: we have it, we don't but there is a read buffer, or we need to replace an old key
            //find the oldest key and remove it.            
            uint imax = 0;
            int max = keyLives[imax];
            for (uint i = 0; i < keys.Length; i++) {                
                if(keys[i] == key)
                {
                    //highest priority outcome. Need to finish iterating though to add to lives.
                    imax = i;
                    max = -1;
                }
                else if(max >= 0 && read[i])
                {
                    //We have an open spot
                    imax = i;
                    max = -1;
                }
                else if (max >= 0 && keyLives[i] > max) {
                    //find the oldest key
                    imax = i;
                    max = keyLives[i];
                 }
                keyLives[i]++;
            }
            keys[imax] = key;
            keyLives[imax] = 0; //note this will reset the lives if we had a reset count
            //since it was requested, mark as unread
            read[imax] = false;

            //reset indicators if we have a new key
            if (max >= 0)
                loaded[imax] = false;

            //dispatch the message            
            byte[] msg = long2Bytes(info.key_bytes, key);
            await ws.OutputStream.WriteAsync(msg.AsBuffer());
            
        }

        /*
         * This will fetch information about the connection from the server and initiate the websocket.
         * It will return the data about the connection, or null if it fails.
         * 
         */
        public async Task<T> Connect()
        {
            T result = await fetchMetadata();
            ws = new MessageWebSocket();
            ws.Control.MessageType = SocketMessageType.Binary;
            ws.MessageReceived += MessageReceived;
            ws.Closed += OnClosed;

            try
            {
                Uri server = new Uri(info.ws_url);
                await ws.ConnectAsync(server);
            }
            catch (Exception ex) // For debugging
            {
                ws.Dispose();
                throw ex;
            }

            return result;
        }

        private async Task<T> fetchMetadata()
        {
            var client = new HttpClient();
            string response = await client.GetStringAsync(initiateURI);
            info = new T();
            info.Parse(response);
            if (info == null)
            {
                throw new Exception("Unable to process server response: " + response);
            }
            return info;
        }

        private void OnClosed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            return;
        }

        private async void MessageReceived(IWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            using (var stream = args.GetDataStream())
            {
                //read in info about the frame
                byte[] header = new byte[info.key_bytes + info.len_bytes];
                await stream.ReadAsync(header.AsBuffer(), (uint) header.Length, InputStreamOptions.Partial);
                //this fancy slice stuff is because we may have to reverse the buffer due to endianness
                ulong key = bytes2Long(info.key_bytes, header.Take((int)info.key_bytes).ToArray<byte>());
                ulong N = bytes2Long(info.len_bytes, header.Skip((int) info.key_bytes).ToArray<byte>());

                //find the index of the key we're setting
                uint i;
                for (i = 0; i < bufferNumber; i++)
                {
                    if (keys[i] == key)
                        break;
                }
                if (i == bufferNumber)
                    return; //this message must be no longer needed
                
                //lazily make our buffer.
                if (buffers[i] == null)
                    buffers[i] = new byte[info.max_buffer_size];

                sizes[i] = (uint) N;
                await stream.ReadAsync(buffers[i].AsBuffer(), (uint) N, InputStreamOptions.Partial);
                //mark it as loaded
                loaded[i] = true;
                //fire
                if(KeyLoadedEvent != null)
                    KeyLoadedEvent.Invoke(this, key);
            }
        }

        private static ulong bytes2Long(uint n, byte[] v)
        {
            ulong result = 0;
            if (BitConverter.IsLittleEndian)
                Array.Reverse(v);
            switch (n)
            {
                case 2:
                    result = (ulong) BitConverter.ToUInt16(v, 0);
                    break;
                case 4:
                    result = (ulong)BitConverter.ToUInt32(v, 0);
                    break;
                case 8:
                    result = (ulong)BitConverter.ToUInt64(v, 0);
                    break;
                default:
                    throw new Exception("The number of bytes specified is invalid ");
            }
            return result;
        }

        private static byte[] long2Bytes(uint n, ulong v)
        {
            byte[] result;
            switch (n)
            {
                case 2:                    
                    result = BitConverter.GetBytes((short) v);
                    break;
                case 4:
                    result = BitConverter.GetBytes((uint) v);
                    break;
                case 8:
                    result = BitConverter.GetBytes((ulong) v);
                    break;
                default:
                    throw new Exception("The number of bytes specified is invalid ");
            }
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }
    }

    
}
