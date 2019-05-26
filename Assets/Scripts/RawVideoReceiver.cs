// proof of concept, ffmpeg raw video into unity texture 2D using UDP streaming
// https://unitycoder.com/blog/2019/05/26/ffmpeg-stream-raw-video-into-unity-texture2d/
// > ffmpeg -f gdigrab -i desktop -pixel_format rgb8 -video_size 256x256 -vf scale=256:256 -framerate 5 -r 5 -f rawvideo udp://127.0.0.1:8888

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityCoder.RawVideoUDP
{
    public class RawVideoReceiver : MonoBehaviour
    {
        public Material targetMat;

        UdpClient client;
        int port = 8888;
        int receiveBufferSize = 1472 * 1000;
        IPEndPoint ipEndPoint;
        private object obj = null;
        private AsyncCallback AC;
        byte[] receivedBytes;

        Texture2D tex;
        public int size = 256;

        int imageSize = 0;

        byte[] dump;
        int bufferSize = 0;
        int bufferIndex = 0;
        int bufferFrameStart = 0;
        byte[] temp;
        bool frameReady = false;

        void Start()
        {
            //tex = new Texture2D(size, size, TextureFormat.RGB24, false, false);
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            imageSize = size * size * 4;
            temp = new byte[imageSize];

            // init pixels wit bright color
            for (int i = 0; i < imageSize; i += 4)
            {
                temp[i] = 255;
                temp[i + 1] = 0;
                temp[i + 2] = 255;
            }
            tex.LoadRawTextureData(temp);
            tex.Apply(false);

            bufferSize = imageSize * 100;
            dump = new byte[bufferSize];

            targetMat.mainTexture = tex;

            InitializeUDPClient();
        }

        Queue<int> frameIndex = new Queue<int>();
        int frameBufferCount = 0;

        void FixedUpdate()
        {
            // if we have frames, draw them to texture
            if (frameBufferCount > 0)
            {
                Buffer.BlockCopy(dump, frameIndex.Dequeue(), temp, 0, imageSize);
                frameBufferCount--;
                tex.LoadRawTextureData(temp);
                tex.Apply(false);
            }
        }

        void ReceivedUDPPacket(IAsyncResult result)
        {
            try
            {
                receivedBytes = client.EndReceive(result, ref ipEndPoint);
                var len = receivedBytes.Length;

                // we only use the buffer until the end, should wrap around
                if (bufferIndex + len > bufferSize)
                {
                    Debug.LogError("Buffer finished, should fix this..");
                    return;
                }

                Buffer.BlockCopy(receivedBytes, 0, dump, bufferIndex, len);
                bufferIndex += len;
                if (bufferIndex - bufferFrameStart >= imageSize)
                {
                    frameIndex.Enqueue(bufferFrameStart);
                    frameBufferCount++;
                    bufferFrameStart += imageSize;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            client.BeginReceive(AC, obj);
        }

        public void InitializeUDPClient()
        {
            ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            client = new UdpClient();
            client.Client.ReceiveBufferSize = receiveBufferSize;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
            client.ExclusiveAddressUse = false;
            client.EnableBroadcast = true;
            client.Client.Bind(ipEndPoint);
            client.DontFragment = true;
            client.Client.ReceiveBufferSize = 1472 * 100000;
            AC = new AsyncCallback(ReceivedUDPPacket);
            client.BeginReceive(AC, obj);
            Debug.Log("Started UDP listener..");
        }

        private void OnDestroy()
        {
            if (client != null)
            {
                client.Close();
            }
        }
    }
}