using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Reflection;
using UnityEngine;

public class SendCollision : MonoBehaviour
{
    [Header("Network Settings")]
    public string connectionIP = "127.0.0.1";
    public int connectionPort = 5002;
    public int index;

    [Header("Detection Options")]
    public bool useCheckSphereInstead = false;
    public LayerMask interactableLayerMask;

    private Thread mThread;
    private UdpClient client;
    private IPEndPoint ep;

    private bool collisionUpdated;
    private bool sendClear;

    private float worldRadius;
    private bool running;
    private ManualResetEventSlim signalNewData = new(false);

    private readonly LinkedList<CollisionInfo> collisionStack = new();
    private readonly HashSet<Collider> activeColliders = new();

    private struct CollisionInfo
    {
        public Collider collider;
        public Color32 color;
    }

    void Start()
    {
        mThread = new Thread(SendLoop);
        mThread.Start();

        var sphere = GetComponent<SphereCollider>();
        if (sphere != null)
            worldRadius = sphere.radius * transform.lossyScale.x;
    }

    void Update()
    {
        if (!useCheckSphereInstead) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, worldRadius, interactableLayerMask);
        HashSet<Collider> currentFrameColliders = new(hits);

        // Handle new entries
        foreach (var col in hits)
        {
            if (!activeColliders.Contains(col))
            {
                Color32 color = ExtractColor(col.gameObject);
                collisionStack.AddFirst(new CollisionInfo { collider = col, color = color });
                activeColliders.Add(col);
                ApplyColor(color);
                collisionUpdated = true;
                signalNewData.Set();
            }
        }

        // Handle exits
        foreach (var col in new List<Collider>(activeColliders))
        {
            if (!currentFrameColliders.Contains(col))
            {
                activeColliders.Remove(col);
                RemoveFromStack(col);
                if (collisionStack.Count > 0)
                {
                    ApplyColor(collisionStack.First.Value.color);
                    collisionUpdated = true;
                    signalNewData.Set();
                }
                else
                {
                    sendClear = true;
                    ApplyColor(Color.white);
                    signalNewData.Set();
                }
            }
        }
    }

    private void SendLoop()
    {
        client = new UdpClient();
        ep = new IPEndPoint(IPAddress.Parse(connectionIP), connectionPort);
        running = true;

        while (true)
        {
            if (!running) break;

            if (signalNewData.Wait(300))
            {
                signalNewData.Reset();

                if (!running) break;

                if (collisionUpdated && !sendClear)
                {
                    Color32 color = collisionStack.Count > 0 ? collisionStack.First.Value.color : Color.white;
                    SendColor(color);
                    collisionUpdated = false;
                }
                else if (sendClear)
                {
                    SendColor(new Color32(0, 0, 0, 0));
                    sendClear = false;
                }
            }
        }

        client?.Close();
    }

    private void SendColor(Color32 color)
    {
        byte[] buffer = Encoding.ASCII.GetBytes($"{index}|{color.r}|{color.g}|{color.b}");
        client.Send(buffer, buffer.Length, ep);
    }

    private void RemoveFromStack(Collider col)
    {
        var node = collisionStack.First;
        while (node != null)
        {
            if (node.Value.collider == col)
            {
                collisionStack.Remove(node);
                break;
            }
            node = node.Next;
        }
    }

    private void ApplyColor(Color32 color)
    {
        GetComponent<Renderer>().material.color = color;
    }

    private Color32 ExtractColor(GameObject obj)
    {
        if (obj.TryGetComponent<MeshRenderer>(out var renderer))
        {
            return renderer.material.color;
        }

        Transform current = obj.transform;
        for (int i = 0; i < 10 && current != null; i++)
        {
            foreach (var comp in current.GetComponents<MonoBehaviour>())
            {
                var field = comp.GetType().GetField("targetColor", BindingFlags.Instance | BindingFlags.Public);
                if (field is { FieldType: { } type } && type == typeof(Color32))
                {
                    return (Color32)field.GetValue(comp);
                }
            }
            current = current.parent;
        }

        return new Color32(0, 0, 0, 0);
    }

    private void OnTriggerEnter(Collider col)
    {
        if (!useCheckSphereInstead && col.CompareTag("LED") == false)
        {
            Color32 color = ExtractColor(col.gameObject);
            collisionStack.AddFirst(new CollisionInfo { collider = col, color = color });
            activeColliders.Add(col);
            ApplyColor(color);
            collisionUpdated = true;
            signalNewData.Set();
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (!useCheckSphereInstead && activeColliders.Contains(col))
        {
            activeColliders.Remove(col);
            RemoveFromStack(col);
            if (collisionStack.Count > 0)
            {
                ApplyColor(collisionStack.First.Value.color);
                collisionUpdated = true;
                signalNewData.Set();
            }
            else
            {
                sendClear = true;
                ApplyColor(Color.white);
                signalNewData.Set();
            }
        }
    }

    private void OnDisable()
    {
        running = false;
        signalNewData.Set();
        if (mThread != null && mThread.IsAlive)
        {
            mThread.Join(); // Wait for clean shutdown
        }
        client?.Close();
    }
}
