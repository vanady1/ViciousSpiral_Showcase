using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LineManager : MonoBehaviour
{
    public CircleLayout circleLayout;
    public GameObject linePrefab;
    public SpellSlotSystem spellSlotSystem;

    // Kept for inspector compatibility
    public Sprite defaultCircleSprite;
    public Sprite celectedCircleSprite;

    public float lineThickness = 5f;

    private RectTransform startElement;
    private RectTransform currentElement;

    private GameObject currentLineObject;
    private RectTransform currentLineRectTransform;
    private bool isDrawing = false;

    private RectTransform canvasRect;
    private RectTransform containerRect;

    private readonly List<GameObject> allLines = new List<GameObject>();
    private readonly HashSet<string> connectedEdges = new HashSet<string>();   // edge keys "i-j"
    private readonly List<int> connectionOrder = new List<int>();

    private readonly HashSet<RectTransform> activePoints = new HashSet<RectTransform>();
    private readonly Dictionary<RectTransform, int> pointDegree = new Dictionary<RectTransform, int>();

    // Current drag tracking
    private RectTransform pressedStart;          // where current drag started
    private bool madeConnectionThisDrag = false; // did we create an edge during this drag

    private void OnEnable() => ClearAllLines();
    private void OnDisable() => ClearAllLines();

    private void Start()
    {
        if (circleLayout == null) return;

        canvasRect = GetComponentInParent<Canvas>()?.transform as RectTransform;
        containerRect = circleLayout.transform as RectTransform;
        AssignEventHandlers();
    }

    private void AssignEventHandlers()
    {
        foreach (RectTransform element in circleLayout.elements)
        {
            if (element == null) continue;

            EventTrigger trigger = element.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = element.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();

            AddOrReplace(trigger, EventTriggerType.PointerEnter, (e) => OnElementHover(element));
            AddOrReplace(trigger, EventTriggerType.PointerExit, (e) => OnElementExit(element));
        }
    }

    private void AddOrReplace(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        trigger.triggers.RemoveAll(t => t.eventID == type);

        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(new UnityEngine.Events.UnityAction<BaseEventData>(action));
        trigger.triggers.Add(entry);
    }

    public void OnElementHover(RectTransform hoveredElement)
    {
        if (isDrawing && hoveredElement != currentElement)
        {
            AttachLineToCurrentElement(hoveredElement);
        }

        // Temporary highlight on hover
        LightUpCircle(hoveredElement, true);
        RefreshActiveHighlights();
    }

    public void OnElementExit(RectTransform exitedElement)
    {
        // If drag is in progress and this is the starting point – keep it highlighted
        if (isDrawing && exitedElement == pressedStart)
        {
            LightUpCircle(exitedElement, true);
            return;
        }

        // If point is active (has connections) it must remain highlighted
        if (GetDegree(exitedElement) > 0)
        {
            LightUpCircle(exitedElement, true);
        }
        else
        {
            LightUpCircle(exitedElement, false);
        }
    }

    private void StartDrawing(RectTransform start)
    {
        startElement = start;
        currentElement = start;

        // Keep start highlighted while dragging
        LightUpCircle(startElement, true);

        pressedStart = startElement;
        madeConnectionThisDrag = false;

        // Create "floating" line inside the circle container (shared local coordinates)
        currentLineObject = Instantiate(linePrefab, containerRect);
        currentLineRectTransform = currentLineObject.GetComponent<RectTransform>();
        var img = currentLineRectTransform.GetComponent<RawImage>();
        if (img != null) img.raycastTarget = false;

        Vector2 p = startElement.anchoredPosition;
        currentLineRectTransform.anchoredPosition = p;
        UpdateLine(p, p);

        allLines.Add(currentLineObject);
        isDrawing = true;

        int idx = System.Array.IndexOf(circleLayout.elements, startElement);
        if (connectionOrder.Count == 0 || connectionOrder[connectionOrder.Count - 1] != idx)
            connectionOrder.Add(idx);
    }

    private void AttachLineToCurrentElement(RectTransform newElement)
    {
        string edgeKey = GetConnectionKey(startElement, newElement);
        if (currentLineRectTransform == null || connectedEdges.Contains(edgeKey)) return;

        Vector2 endPos = newElement.anchoredPosition;
        UpdateLine(startElement.anchoredPosition, endPos);

        // Fix edge
        connectedEdges.Add(edgeKey);
        madeConnectionThisDrag = true;

        // Both points become active
        IncDegree(startElement);
        IncDegree(newElement);
        activePoints.Add(startElement);
        activePoints.Add(newElement);

        LightUpCircle(startElement, true);
        LightUpCircle(newElement, true);

        int idx = System.Array.IndexOf(circleLayout.elements, newElement);
        if (connectionOrder.Count == 0 || connectionOrder[connectionOrder.Count - 1] != idx)
            connectionOrder.Add(idx);

        // Prepare next floating line from the new point
        startElement = newElement;
        currentLineObject = Instantiate(linePrefab, containerRect);
        currentLineRectTransform = currentLineObject.GetComponent<RectTransform>();
        var img = currentLineRectTransform.GetComponent<RawImage>();
        if (img != null) img.raycastTarget = false;

        RefreshActiveHighlights();
        allLines.Add(currentLineObject);
    }

    private void UpdateLine(Vector2 startPos, Vector2 endPos)
    {
        Vector2 d = endPos - startPos;
        currentLineRectTransform.pivot = new Vector2(0, 0.5f);
        currentLineRectTransform.anchoredPosition = startPos;
        currentLineRectTransform.sizeDelta = new Vector2(d.magnitude, lineThickness);
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        currentLineRectTransform.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RectTransform hovered = GetHoveredElement();
            if (hovered != null) StartDrawing(hovered);
        }

        if (Input.GetMouseButton(0))
        {
            if (!isDrawing)
            {
                RectTransform hovered = GetHoveredElement();
                if (hovered != null) StartDrawing(hovered);
            }
            else if (currentLineRectTransform != null)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    containerRect,
                    Input.mousePosition,
                    canvasRect != null
                        ? canvasRect.GetComponentInParent<Canvas>()?.worldCamera
                        : null,
                    out localPoint);

                UpdateLine(startElement.anchoredPosition, localPoint);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDrawing && currentLineObject != null)
            {
                Destroy(currentLineObject);
                currentLineObject = null;
                currentLineRectTransform = null;

                // If no edge was created – user "cancelled" the draw
                if (!madeConnectionThisDrag &&
                    pressedStart != null &&
                    GetDegree(pressedStart) == 0)
                {
                    LightUpCircle(pressedStart, false);
                }

                pressedStart = null;
                madeConnectionThisDrag = false;
                isDrawing = false;

                // Active points stay highlighted
                RefreshActiveHighlights();
            }
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            ClearAllLines();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            List<int> drawnPattern = GetActivePoints();
            Spell matchedSpell = spellSlotSystem.CheckPatternMatch(drawnPattern);

            if (matchedSpell != null)
            {
                if (matchedSpell.TryStoreSpell())
                {
                    Debug.Log("Spell Stored!");
                    ClearAllLines();
                }
                else
                {
                    Debug.Log("Not Enough Mana!");
                }
            }
            else
            {
                Debug.Log("No matching spell found for the drawn pattern.");
            }
        }
    }

    private RectTransform GetHoveredElement()
    {
        PointerEventData pd = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pd, results);

        foreach (var r in results)
        {
            RectTransform el = r.gameObject.GetComponent<RectTransform>();
            if (el != null && System.Array.IndexOf(circleLayout.elements, el) >= 0)
                return el;
        }
        return null;
    }

    private void ClearAllLines()
    {
        foreach (var line in allLines) Destroy(line);
        allLines.Clear();
        connectedEdges.Clear();
        connectionOrder.Clear();

        foreach (RectTransform element in circleLayout.elements)
            LightUpCircle(element, false);

        activePoints.Clear();
        pointDegree.Clear();

        isDrawing = false;
        pressedStart = null;
        madeConnectionThisDrag = false;

        RefreshActiveHighlights();
    }

    // --- highlighting / activity ---

    private void LightUpCircle(RectTransform element, bool on)
    {
        if (element == null) return;
        Point p = element.GetComponent<Point>();
        if (p != null) p.SetImageHighlighted(on);
    }

    private void RefreshActiveHighlights()
    {
        foreach (RectTransform el in circleLayout.elements)
        {
            if (el == null) continue;
            bool active = GetDegree(el) > 0;
            if (active) LightUpCircle(el, true);
        }

        spellSlotSystem.UpdateActivePoints(GetActivePoints());
    }

    private int GetDegree(RectTransform rt)
    {
        if (rt == null) return 0;
        return pointDegree.TryGetValue(rt, out int d) ? d : 0;
    }

    private void IncDegree(RectTransform rt)
    {
        if (rt == null) return;
        if (!pointDegree.ContainsKey(rt)) pointDegree[rt] = 0;
        pointDegree[rt]++;
    }

    private string GetConnectionKey(RectTransform a, RectTransform b)
    {
        int ia = System.Array.IndexOf(circleLayout.elements, a);
        int ib = System.Array.IndexOf(circleLayout.elements, b);
        if (ia < 0 || ib < 0) return string.Empty;
        return ia < ib ? $"{ia}-{ib}" : $"{ib}-{ia}";
    }

    private List<int> GetActivePoints() => new List<int>(connectionOrder);
}
