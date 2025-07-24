using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class SendCollision : MonoBehaviour
{
    [Header("ID")]
    public int index;

    [Header("Detection Options")]
    public bool useCheckSphereInstead = false;
    public LayerMask interactableLayerMask;

    [Header("Collision State")]
    public bool sendClear;
    public bool clearLoop;
    public Color32? brushColor = null;

    private float worldRadius;
    private SphereCollider sphere;

    private readonly LinkedList<CollisionInfo> collisionStack = new();
    private readonly HashSet<Collider> activeColliders = new();

    public struct CollisionInfo
    {
        public Collider collider;
        public Color32 color;
    }

    void Start()
    {
        sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            worldRadius = sphere.radius * transform.lossyScale.x;
        }
    }

    void Update()
    {
        if (useCheckSphereInstead)
        {
            CheckSphereCollisions();
        }

        if (clearLoop)
        {
            clearLoop = false;
            SendManager.Instance.SendClear();
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if (useCheckSphereInstead || col.CompareTag("LED")) return;

        HandleCollisionEnter(col);
    }

    void OnTriggerExit(Collider col)
    {
        if (useCheckSphereInstead || !activeColliders.Contains(col)) return;

        HandleCollisionExit(col);
    }

    private void CheckSphereCollisions()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, worldRadius, interactableLayerMask);
        HashSet<Collider> currentFrame = new(hits);

        foreach (var col in hits)
        {
            if (!activeColliders.Contains(col))
            {
                Color32 color = ExtractColor(col.gameObject);
                AddCollision(col, color);
                if (brushColor == null)
                    ApplyColor(color);
            }
        }

        foreach (var col in new List<Collider>(activeColliders))
        {
            if (!currentFrame.Contains(col))
            {
                HandleCollisionExit(col);
            }
        }

        if (clearLoop)
        {
            clearLoop = false;
            SendManager.Instance.SendClear();
        }
    }

    private void HandleCollisionEnter(Collider col)
    {
        if (col.CompareTag("Eraser"))
        {
            Color32 color = ExtractColor(col.gameObject);
            AddCollision(col, color);
            brushColor = null;
            ApplyColor(color);
        }
        else if (col.CompareTag("Brush"))
        {
            brushColor = ExtractColor(col.gameObject);
            ApplyColor(brushColor.Value);
        }
        else
        {
            Color32 color = ExtractColor(col.gameObject);
            AddCollision(col, color);
            if (brushColor == null)
                ApplyColor(color);
        }
    }

    // Hand Demo specific, aka remove me
    public void ClearSingleLED() {
        activeColliders.Clear();
        ApplyColor(Color.white);
        QueueClear();
    }

    private void HandleCollisionExit(Collider col)
    {
        activeColliders.Remove(col);
        RemoveFromStack(col);

        if (col == null) {
            ApplyColor(Color.white);
            QueueClear();
            return;
        }

        if (col.CompareTag("Eraser"))
        {
            ApplyColor(Color.white);
            QueueClear();
        }
        else if (col.CompareTag("Brush"))
        {
            brushColor = null;
            if (collisionStack.Count > 0)
                ApplyColor(collisionStack.First.Value.color);
            else
                GetComponent<Renderer>().material.color = Color.white;
                QueueClear();
        }
        else if (brushColor == null)
        {
            if (collisionStack.Count > 0)
                ApplyColor(collisionStack.First.Value.color);
            else
                GetComponent<Renderer>().material.color = Color.white;
                QueueClear();
        }
    }

    private void AddCollision(Collider col, Color32 color)
    {
        collisionStack.AddFirst(new CollisionInfo { collider = col, color = color });
        activeColliders.Add(col);
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

    public void ApplyColor(Color32 color)
    {
        GetComponent<Renderer>().material.color = color;
        SendColor(color);
    }

    private void SendColor(Color32 color)
    {
        string message = $"{index}|{color.r}|{color.g}|{color.b}";
        SendManager.Instance.QueueMessage(message);
    }

    public void QueueClear()
    {
        SendColor(new Color32(0, 0, 0, 0));
    }

    public void UpdateState() {
        if (GetComponent<Renderer>().material.color == Color.white) {
            SendColor(new Color32(0, 0, 0, 0));
        } else {
            SendColor(GetComponent<Renderer>().material.color);
        }
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
}