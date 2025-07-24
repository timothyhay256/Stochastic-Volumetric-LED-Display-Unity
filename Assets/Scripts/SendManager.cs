using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class SendManager : MonoBehaviour
{
    public static SendManager Instance;

    private UdpClient client;
    private IPEndPoint ep;

    private Thread sendThread;
    private ConcurrentQueue<string> messageQueue = new();
    private ManualResetEventSlim signalNewData = new(false);

    private bool running = true;

    [Header("Network Settings")]
    public string connectionIP = "127.0.0.1";
    public int connectionPort = 5001;

    private void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        client = new UdpClient();
        ep = new IPEndPoint(IPAddress.Parse(connectionIP), connectionPort);

        sendThread = new Thread(SendLoop);
        sendThread.Start();
    }

    private void SendLoop()
    {
        while (running)
        {
            if (!signalNewData.Wait(600)) continue;
            signalNewData.Reset();

            while (messageQueue.TryDequeue(out string message))
            {
                // Debug.Log(message);
                byte[] buffer = Encoding.ASCII.GetBytes(message);
                client.Send(buffer, buffer.Length, ep);
            }
        }
    }

    public void QueueMessage(string message)
    {
        messageQueue.Enqueue(message);
        signalNewData.Set();
    }

    public void SendClear()
    {
        Debug.Log("Clear command queued");
        QueueMessage("CLEAR");
    }

    void OnDestroy()
    {
        running = false;
        signalNewData.Set();
        sendThread.Join();
        client?.Close();
    }
}