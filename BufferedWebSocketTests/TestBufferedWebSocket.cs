using BufferedWebSockets;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using System;
using System.Threading.Tasks;

namespace BufferedWebSocketTests
{
    [TestClass]
    public class TestBufferedWebSocket
    {
        private string testUri = "http://localhost:8888/sim/test";

        [TestMethod]
        public async Task TestConnect()
        {
            var ws = new BufferedWebSocket<Metadata>(testUri, 1);
            var info = await ws.Connect();
        }

        [TestMethod]
        public async Task TestSimConnect()
        {
            var ws = new BufferedWebSocket<Siminfo>(testUri, 1);
            var info = await ws.Connect();
            Assert.IsNotNull(info.Elements);
        }

        [TestMethod]
        public async Task TestSimpleRequest()
        {
            var ws = new BufferedWebSocket<Metadata>(testUri, 1);
            var info = await ws.Connect();
            int start = ws.ReadyUnreadBuffers;
            Assert.AreEqual(start, 0, 0);

            //attach our event handler
            ulong key = 0;
            ws.KeyLoadedEvent += delegate (Object sender, ulong e)
            {
                key = e;
            };

            await ws.Request(1);
            //wait for message to come back
            for (int i = 0; i < 10; i++)
            {
                Task.Delay(50).Wait();
                if (key == 1)
                    break;
            }
            Assert.AreEqual(start + 1, ws.ReadyUnreadBuffers);
        }

        /*
         * Here we make sure things work as expected with just one buffer.
         */
        [TestMethod]
        public async Task TestRequestKeyManagement1()
        {

            var ws = new BufferedWebSocket<Metadata>(testUri, 1);
            var info = await ws.Connect();
            await ws.Request(0);
            await ws.Request(1);
            Assert.AreEqual(ws.Get(0), 0, 0); //add third argument to force typecast

            //all this work to wait for buffer
            //attach our event handler
            ulong key = 0;
            ws.KeyLoadedEvent += delegate (Object sender, ulong e)
            {
                key = e;
            };
            //wait for message to come back
            for (int i = 0; i < 10; i++)
            {
                Task.Delay(50).Wait();
                if (key == 1)
                    break;
            }
            Assert.AreNotEqual(ws.Get(1), 0);
        }

        /*
        * Here we make sure things work as expected with just one buffer.
        */
        [TestMethod]
        public async Task TestRequestKeyManagementLives()
        {

            var ws = new BufferedWebSocket<Metadata>(testUri, 5);
            var info = await ws.Connect();
            for (uint i = 0; i < 5; i++)
                await ws.Request(i);
            //now request others and  make sure the rest are null   
            for (uint i = 5; i < 10; i++)
                await ws.Request(i);

            //all this work to wait for buffer
            //attach our event handler
            bool ready = false;
            ws.KeyLoadedEvent += delegate (Object sender, ulong e)
            {
                if (!ready && e == 9)
                    ready = true;
            };
            //wait for messages to come back
            for (int i = 0; i < 10; i++)
            {
                Task.Delay(50).Wait();
                if (ready)
                    break;
            }
            for (uint i = 0; i < 5; i++)
                Assert.AreEqual(ws.Get(i), 0, 0);
            //now request others and  make sure the rest have data
            for (uint i = 5; i < 10; i++)
            {
                //need to check that array is null because we may have exceeded number of frames
                byte[] b;
                ws.Get(i, out b);
                Assert.IsNotNull(b);                
            }
        }

        /*
        * Test the normal usage -> forard iteration
        */
        [TestMethod]
        public async Task TestRequestUsualPattern()
        {

            var ws = new BufferedWebSocket<Metadata>(testUri, 3);
            var info = await ws.Connect();
            for (uint i = 0; i < 3; i++)
                await ws.Request(i);            

            //all this work to wait for buffer
            //attach our event handler
            bool ready = false;
            bool ready2 = false;
            ws.KeyLoadedEvent += delegate (Object sender, ulong e)
            {
                if (!ready && e == 2)
                    ready = true;
                if (!ready2 && e == 5)
                    ready2 = true;
            };
            //wait for messages to come back
            for (int i = 0; i < 10; i++)
            {
                Task.Delay(50).Wait();
                if (ready)
                    break;
            }
            Assert.IsTrue(ready);

            //fetch and request
            for (uint i = 0; i < 3; i++)
            {
                Assert.AreNotEqual(ws.Get(i), 0, 0);
                await ws.Request(i + 3);
            }

            //wait for messages to come back
            for (int i = 0; i < 10; i++)
            {
                Task.Delay(50).Wait();
                if (ready2)
                    break;
            }
            Assert.IsTrue(ready2);
            //make sure we were able to get past our initial batch
            for (uint i = 3; i < 6; i++)
            {
                //need to check that array is null because we may have exceeded number of frames
                byte[] b;
                ws.Get(i, out b);
                Assert.IsNotNull(b);
            }
        }

        [TestMethod]
        public async Task TestFloats()
        {
            var ws = new BufferedWebSocket<Metadata>(testUri, 2);
            var info = await ws.Connect();            
            await ws.Request(15);

            //all this work to wait for buffer
            //attach our event handler
            bool ready = false;
            ws.KeyLoadedEvent += delegate (Object sender, ulong e)
            {
                ready = true;
            };
            //wait for messages to come back
            for (int i = 0; i < 10; i++)
            {
                Task.Delay(50).Wait();
                if (ready)
                    break;
            }

            float[] result = new float[1000];
            result[0] = 0f;
            uint size = ws.GetFloats(15, ref result);
            Assert.AreNotEqual(size, 0, 0);
            Assert.AreNotEqual(result[0], 0f, 0f);
        }

    }
}

