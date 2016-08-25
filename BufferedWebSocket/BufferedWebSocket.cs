using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BufferedWebSocket
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
    public class BufferedWebSocket<T> where T : Metadata
    {
        private uint bufferNumber;
        private IBuffer[] buffers;
        private ulong[] keys;
        private bool[] free;
        private T info;
        private MessageWebSocket ws;
        private Uri initiateURI;


        public async Task<IBuffer> get(ulong key)
        {

        }

        /*
         * This will fetch information about the connection from the server and initiate the websocket.
         * It will return the data about the connection, or null if it fails.
         * 
         */
        public async Task<T> connect()
        {
            T result = await fetchMetadata();
            ws = new MessageWebSocket();
            ws.Control.MessageType = SocketMessageType.Utf8;
            ws.MessageReceived += MessageReceived;
            ws.Closed += OnClosed;

            try
            {
                Uri server = new Uri(info.WS_URI);
                await ws.ConnectAsync(server);
            }
            catch (Exception ex) // For debugging
            {
                ws.Dispose();
                return null;
            }

            return result;
        }

        private async Task<T> fetchMetadata()
        {
            var client = new HttpClient();
            string response = await client.GetStringAsync(initiateURI);
            info = JsonConvert.DeserializeObject<T>(response);
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
            using (DataReader reader = args.GetDataReader())
            {
                //read in info about the frame
                reader.InputStreamOptions = InputStreamOptions.Partial;
                reader.ByteOrder = ByteOrder.BigEndian;
                await reader.LoadAsync(info.KEY_BYTES + info.LEN_BYTES);
                ulong key = bytes2Long(info.KEY_BYTES, reader);
                ulong N = bytes2Long(info.LEN_BYTES, reader);

                //find an unused buffer
                uint i;
                for (i = 0; i < bufferNumber; i++)
                {
                    if (free[i])
                        break;
                }
                if (i == bufferNumber)
                    return; //this message must be no longer needed
                //lazily make our buffer.
                if (buffers[i] == null)
                    buffers[i] = new Windows.Storage.Streams.Buffer(info.MAX_BUFFER_SIZE);
                //use the stream directly so we can use our buffer
                using (var stream = reader.DetachStream())
                {
                    await stream.ReadAsync(buffers[i], (uint)N, InputStreamOptions.None);
                }                    
            }
        }

        private static ulong bytes2Long(uint n, DataReader reader)
        {
            ulong result = 0;
            switch (n)
            {
                case 2:
                    result = (ulong)reader.ReadInt16();
                    break;
                case 4:
                    result = (ulong)reader.ReadUInt32();
                    break;
                case 8:
                    result = (ulong)reader.ReadUInt64();
                    break;
                default:
                    throw new Exception("The number of bytes specified is invalid ");
            }
            return result;
        }
    }

    
}
