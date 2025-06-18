using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System;
using System.Reflection;

public class SendCollision : MonoBehaviour
{
    Thread mThread;
    public string connectionIP = "127.0.0.1";
    public int connectionPort = 5002;
    public int index;
    [Tooltip("Uses Physics.checkSphere instead of collision/trigger enter.")]
    public bool useCheckSphereInstead = false;

    IPAddress localAdd;
    private bool collision = false; // Triggers sending the buffer and resetting to false. Set by ColorEnter and Exit
    private bool colActive = false; // Used to track if there is already a collision active when using useCheckSphereInstead
    private bool exit;
    private int activeR;
    private int activeG;
    private int activeB;
    private int collCount;
    private float worldRadius;
    private Color32 firstColor;

    // TcpListener listener;
    UdpClient client;
    IPEndPoint ep;

    bool running;

    void Start()
    {
        ThreadStart ts = new ThreadStart(GetInfo);
        mThread = new Thread(ts);
        mThread.Start();
        var rend = GetComponent<Renderer>();

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        worldRadius = sphereCollider.radius * transform.lossyScale.x;
    }

    void Update()
    {
        if (useCheckSphereInstead)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, worldRadius);

            List<Collider> filtered = new();

            foreach (Collider col in hits)
            {
                if (!col.CompareTag("LED"))
                    filtered.Add(col);
            }

            Collider[] filteredHits = filtered.ToArray();

            // TODO: Manage multiple colliders somehow
            if (filteredHits.Length > 0 && !colActive)
            {
                colActive = true;
                ColorEnterEvent(filteredHits[0].gameObject);
                Debug.Log("Got collision");
            }
            else if (colActive)
            {
                colActive = false;
                ColorExitEvent();
                Debug.Log("Exiting collision");
            }
        }
    }

    void GetInfo()
    {
        localAdd = IPAddress.Parse(connectionIP);
        client = new UdpClient();
        ep = new IPEndPoint(localAdd, connectionPort);

        running = true;
        while (running)
        {
            Thread.Sleep(50); // Prevent high CPU usage
            SendAndReceiveData();
        }
    }

    void SendAndReceiveData()
    {
        if (collision)
        {
            byte[] myWriteBuffer = Encoding.ASCII.GetBytes(index + "|" + activeR + "|" + activeG + "|" + activeB);
            client.Send(myWriteBuffer, myWriteBuffer.Length, ep);
            collision = false;
        }
        else if (exit)
        {
            byte[] myWriteBuffer = Encoding.ASCII.GetBytes(index + "|0|0|0");
            client.Send(myWriteBuffer, myWriteBuffer.Length, ep);
            exit = false;
        }
    }

    private void ColorEnterEvent(GameObject col)
    {
        if (col.tag != "LED")
        {
            collCount += 1;

            Color32 collisionColor = new Color32(0, 0, 0, 255);
            bool found = false;

            var meshRenderer = col.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.material != null)
            {
                collisionColor = meshRenderer.material.color;
                found = true;
                Debug.Log("Found meshRenderer");
            }
            else
            {
                Transform current = col.transform.parent;

                for (int i = 0; i < 10 && current != null; i++)
                {
                    Component[] components = current.GetComponents<MonoBehaviour>();

                    foreach (var comp in components)
                    {
                        var field = comp.GetType().GetField("targetColor", BindingFlags.Instance | BindingFlags.Public);
                        if (field != null && field.FieldType == typeof(Color32))
                        {
                            collisionColor = (Color32)field.GetValue(comp);
                            found = true;
                            break;
                        }
                    }

                    if (found)
                        break;

                    current = current.parent;
                }
            }

            if (found)
            {
                if (collCount == 1)
                {
                    firstColor = collisionColor;
                }

                Color32 objColor = collisionColor;
                activeR = objColor.r;
                activeG = objColor.g;
                activeB = objColor.b;

                var rend = GetComponent<Renderer>();
                rend.material.SetColor("_Color", objColor);
                collision = true;
            }
            else
            {
            }
        }

    }

    private void ColorExitEvent()
    {
        collCount -= 1;
        if (collCount == 1)
        {
            activeR = firstColor.r;
            activeG = firstColor.g;
            activeB = firstColor.b;
            var rend = GetComponent<Renderer>();
            rend.material.SetColor("_Color", firstColor);
            collision = true;
        }
        else
        {
            Color32 objColor = new Color32(255, 255, 255, 255);
            var rend = GetComponent<Renderer>();
            rend.material.SetColor("_Color", objColor);
            exit = true;
        }
    }

    private void OnTriggerEnter(Collider col)
    {
        if (!useCheckSphereInstead)
        {
            ColorEnterEvent(col.gameObject);
        }
    }


    private void OnCollisionEnter(Collision col)
    {
        if (!useCheckSphereInstead)
        {
            ColorEnterEvent(col.gameObject);
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (!useCheckSphereInstead)
        {
            ColorExitEvent();
        }
    }

    private void OnCollisionExit(Collision col)
    {
        if (!useCheckSphereInstead)
        {
            ColorExitEvent();
        }
    }

    void OnDisable()
    {
        running = false;
    }
}