#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using System.IO;

[ExecuteAlways] // Runs in both Editor and Play Mode
public class SpawnLed : MonoBehaviour
{
    Thread mThread;
    public string connectionIP = "127.0.0.1";
    public int connectionPort = 25001;
    public GameObject led;
    public GameObject ledHolder;
    private GameObject newLed;
    IPAddress localAdd;
    private bool newPos = false;
    private int index = 0;
    private bool endReceive = false;
    private bool restart = false;
    TcpListener listener;
    TcpClient client;
    Vector3 receivedPos = Vector3.zero;
    bool running;

    private void OnEnable()
    {
#if UNITY_EDITOR
        // Register update method when in edit mode
        if (!Application.isPlaying)
            EditorApplication.update += EditorUpdate;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.update -= EditorUpdate;
#endif
        StopThread();
    }

    void Start()
    {
        if (Application.isPlaying || Application.isEditor)
        {
            if (mThread == null || !mThread.IsAlive)
            {
                mThread = new Thread(GetInfo);
                mThread.IsBackground = true;
                mThread.Start();
            }
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        HandleSpawnAndRestart();
    }

#if UNITY_EDITOR
    void EditorUpdate()
    {
        if (Application.isPlaying) return;
        HandleSpawnAndRestart();
    }
#endif

    void HandleSpawnAndRestart()
    {
        if (newPos)
        {
            newLed = Instantiate(led);
            newLed.gameObject.GetComponent<SendCollision>().index = index;
            newLed.transform.SetParent(ledHolder.transform);
            newLed.transform.localPosition = receivedPos;
            index++;
            Debug.Log("Instantiate new object!");
            newPos = false;
        }

        if (restart)
        {
            Debug.Log("Got restart signal, deleting children");
            int i = 0;
            index = 0;
            foreach (Transform child in ledHolder.transform)
            {
                if (i > 0) DestroyImmediate(child.gameObject);
                i++;
            }

            restart = false;
            endReceive = true;
        }
    }

    void GetInfo()
    {
        localAdd = IPAddress.Parse(connectionIP);
        listener = new TcpListener(IPAddress.Any, connectionPort);
        listener.Start();
        running = true;

        while (running)
        {
            if (endReceive)
            {
                client?.Close();
                client = null;
                listener?.Stop();
                listener = new TcpListener(IPAddress.Any, connectionPort);
                listener.Start();
                endReceive = false;
            }

            if (client == null || !client.Connected)
            {
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException) { continue; }
            }

            SendAndReceiveData();
        }
    }

    void StopThread()
    {
        running = false;
        try
        {
            client?.Close();
            listener?.Stop();
            mThread?.Join(100); // Allow 100ms for graceful exit
        }
        catch { }
    }

    void SendAndReceiveData()
    {
        if (client == null || !client.Connected) return;

        try
        {
            NetworkStream nwStream = client.GetStream();
            byte[] buffer = new byte[client.ReceiveBufferSize];
            int bytesRead = nwStream.Read(buffer, 0, buffer.Length);

            if (bytesRead == 0)
            {
                endReceive = true;
                return;
            }

            string dataReceived = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (dataReceived == "END")
                endReceive = true;
            else if (dataReceived == "RESTART")
                restart = true;
            else if (!string.IsNullOrEmpty(dataReceived))
            {
                receivedPos = StringToVector3(dataReceived);
                newPos = true;

                while (newPos) { Thread.Sleep(1); }

                byte[] ack = Encoding.ASCII.GetBytes("ack");
                if (client.Connected && nwStream.CanWrite)
                    nwStream.Write(ack, 0, ack.Length);
            }
        }
        catch (Exception)
        {
            endReceive = true;
        }
    }

    public static Vector3 StringToVector3(string sVector)
    {
        try
        {
            sVector = sVector.Trim('(', ')');
            string[] sArray = sVector.Split(',');

            if (sArray.Length != 3)
                throw new FormatException("Invalid format");

            return new Vector3(float.Parse(sArray[0]), float.Parse(sArray[1]), float.Parse(sArray[2]));
        }
        catch
        {
            return Vector3.zero;
        }
    }
}
