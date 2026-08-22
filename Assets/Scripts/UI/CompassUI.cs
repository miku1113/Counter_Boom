using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Tactical Top-HUD Compass Strip (PUBG / Warzone style).
/// Displays a smooth horizontal degree tape with N, NE, E, SE, S, SW, W, NW,
/// live heading readout in gold, center indicator notch, and dynamic objective markers
/// (Safe, Main Gate, Dropped Keys).
/// </summary>
public class CompassUI : MonoBehaviour
{
    public static CompassUI Instance { get; private set; }

    [Header("UI References (Auto-Generated if Unassigned)")]
    public RectTransform compassBarRoot;
    public RectTransform compassTapeContent;
    public TextMeshProUGUI headingText;
    public Image centerPointer;

    [Header("Compass Settings")]
    [Tooltip("Width of the visible compass window in pixels")]
    public float compassWidth = 640f;
    [Tooltip("Pixels per degree of rotation")]
    public float pixelsPerDegree = 3.2f;
    [Tooltip("Smoothing speed for heading transitions")]
    public float smoothSpeed = 12f;

    [Header("Markers")]
    public GameObject markerPrefab;

    private float currentHeading = 0f;
    private float targetHeading = 0f;
    private PlayerController localPlayer;
    private PlayerAiming localAiming;

    // Dynamic marker instances
    private readonly List<CompassMarkerInstance> activeMarkers = new List<CompassMarkerInstance>();

    private class CompassMarkerInstance
    {
        public Transform targetTransform;
        public Vector3 staticPosition;
        public bool isStatic;
        public RectTransform markerRect;
        public TextMeshProUGUI markerLabel;
        public Image markerIcon;
        public Color color;
        public string label;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureCompassStructure();
    }

    private void Start()
    {
        EnsureCompassStructure();
        PopulateCompassTape();
    }

    private void Update()
    {
        UpdatePlayerHeading();
        UpdateCompassTapePosition();
        UpdateObjectiveMarkers();
    }

    public void EnsureCompassStructure()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (transform.parent != canvas.transform && transform.parent == null)
        {
            transform.SetParent(canvas.transform, false);
        }

        if (compassBarRoot == null)
        {
            Transform existing = canvas.transform.Find("TacticalCompassBar");
            if (existing != null)
            {
                compassBarRoot = existing.GetComponent<RectTransform>();
                compassBarRoot.sizeDelta = new Vector2(compassWidth, 38f);
            }
            else
            {
                // Create Modern Tactical Compass Bar Container
                GameObject barGO = new GameObject("TacticalCompassBar", typeof(RectTransform), typeof(Image));
                barGO.transform.SetParent(canvas.transform, false);

                compassBarRoot = barGO.GetComponent<RectTransform>();
                compassBarRoot.anchorMin = new Vector2(0.5f, 1f);
                compassBarRoot.anchorMax = new Vector2(0.5f, 1f);
                compassBarRoot.pivot = new Vector2(0.5f, 1f);
                compassBarRoot.sizeDelta = new Vector2(compassWidth, 38f);
                compassBarRoot.anchoredPosition = new Vector2(0f, -8f);

                // Dark glassmorphic background
                Image bg = barGO.GetComponent<Image>();
                bg.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);

                Outline outline = barGO.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.4f, 0.6f, 0.6f);
                outline.effectDistance = new Vector2(1f, -1f);

                // Top Heading Readout (e.g. "345° NW")
                GameObject headGO = new GameObject("HeadingText", typeof(RectTransform), typeof(TextMeshProUGUI));
                headGO.transform.SetParent(barGO.transform, false);
                RectTransform headRt = headGO.GetComponent<RectTransform>();
                headRt.anchorMin = new Vector2(0.5f, 1f); headRt.anchorMax = new Vector2(0.5f, 1f);
                headRt.pivot = new Vector2(0.5f, 0f);
                headRt.sizeDelta = new Vector2(120f, 18f);
                headRt.anchoredPosition = new Vector2(0f, 2f);

                headingText = headGO.GetComponent<TextMeshProUGUI>();
                headingText.text = "0° N";
                headingText.fontSize = 13;
                headingText.fontStyle = FontStyles.Bold;
                headingText.alignment = TextAlignmentOptions.Center;
                headingText.color = new Color(1f, 0.88f, 0.2f, 1f);

                // Center Pointer Needle (Yellow Caret ▼)
                GameObject pointerGO = new GameObject("CenterPointer", typeof(RectTransform), typeof(TextMeshProUGUI));
                pointerGO.transform.SetParent(barGO.transform, false);
                RectTransform pRt = pointerGO.GetComponent<RectTransform>();
                pRt.anchorMin = new Vector2(0.5f, 1f); pRt.anchorMax = new Vector2(0.5f, 1f);
                pRt.pivot = new Vector2(0.5f, 1f);
                pRt.sizeDelta = new Vector2(16f, 12f);
                pRt.anchoredPosition = new Vector2(0f, 1f);

                TextMeshProUGUI pTmp = pointerGO.GetComponent<TextMeshProUGUI>();
                pTmp.text = "▼";
                pTmp.fontSize = 11;
                pTmp.alignment = TextAlignmentOptions.Center;
                pTmp.color = new Color(1f, 0.85f, 0.2f, 1f);

                // Masked Viewport for scrolling tape
                GameObject viewportGO = new GameObject("CompassViewport", typeof(RectTransform), typeof(RectMask2D));
                viewportGO.transform.SetParent(barGO.transform, false);
                RectTransform vRt = viewportGO.GetComponent<RectTransform>();
                vRt.anchorMin = Vector2.zero; vRt.anchorMax = Vector2.one;
                vRt.offsetMin = new Vector2(4f, 2f); vRt.offsetMax = new Vector2(-4f, -2f);

                // Scrolling Tape Content Container
                GameObject tapeGO = new GameObject("TapeContent", typeof(RectTransform));
                tapeGO.transform.SetParent(viewportGO.transform, false);
                compassTapeContent = tapeGO.GetComponent<RectTransform>();
                compassTapeContent.anchorMin = new Vector2(0.5f, 0f);
                compassTapeContent.anchorMax = new Vector2(0.5f, 1f);
                compassTapeContent.pivot = new Vector2(0.5f, 0.5f);
                compassTapeContent.sizeDelta = new Vector2(360f * pixelsPerDegree * 3f, 0f);
                compassTapeContent.anchoredPosition = Vector2.zero;
            }
        }

        if (headingText == null && compassBarRoot != null)
        {
            headingText = compassBarRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void PopulateCompassTape()
    {
        if (compassTapeContent == null) return;

        // Clear existing children
        foreach (Transform child in compassTapeContent)
            Destroy(child.gameObject);

        // Build 3 sets of 360° degrees (-360 to +720) for seamless wrap-around
        float totalWidth = 360f * pixelsPerDegree;
        compassTapeContent.sizeDelta = new Vector2(totalWidth * 3f, 0f);

        for (int set = -1; set <= 1; set++)
        {
            float setOffset = set * totalWidth;

            for (int deg = 0; deg < 360; deg += 15)
            {
                float xPos = setOffset + (deg * pixelsPerDegree);

                GameObject tickGO = new GameObject($"Tick_{deg}", typeof(RectTransform), typeof(TextMeshProUGUI));
                tickGO.transform.SetParent(compassTapeContent, false);

                RectTransform rt = tickGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(36f, 0f);
                rt.anchoredPosition = new Vector2(xPos, 0f);

                TextMeshProUGUI tmp = tickGO.GetComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;

                string label = GetHeadingLabel(deg);
                if (deg % 90 == 0)
                {
                    // Cardinal: N, E, S, W
                    tmp.text = $"<size=15><b>{label}</b></size>\n<size=9>|</size>";
                    tmp.color = (deg == 0) ? new Color(1f, 0.35f, 0.35f, 1f) : Color.white;
                }
                else if (deg % 45 == 0)
                {
                    // Ordinal: NE, SE, SW, NW
                    tmp.text = $"<size=12><b>{label}</b></size>\n<size=9>|</size>";
                    tmp.color = new Color(0.85f, 0.92f, 1f, 0.9f);
                }
                else
                {
                    // Degree Numbers: 15, 30, 60, 75...
                    tmp.text = $"<size=10>{deg}</size>\n<size=8>.</size>";
                    tmp.color = new Color(0.6f, 0.75f, 0.85f, 0.7f);
                }
            }
        }
    }

    private string GetHeadingLabel(int deg)
    {
        switch (deg)
        {
            case 0:   return "N";
            case 45:  return "NE";
            case 90:  return "E";
            case 135: return "SE";
            case 180: return "S";
            case 225: return "SW";
            case 270: return "W";
            case 315: return "NW";
            default:  return deg.ToString();
        }
    }

    private void UpdatePlayerHeading()
    {
        if (localPlayer == null || !localPlayer.gameObject.activeInHierarchy)
        {
            localPlayer = PlayerController.LocalPlayer;
            if (localPlayer == null)
            {
                var allPlayers = FindObjectsOfType<PlayerController>();
                foreach (var p in allPlayers)
                {
                    if (p != null && (p.IsLocal || p.IsOwner))
                    {
                        localPlayer = p;
                        break;
                    }
                }
            }

            if (localPlayer != null)
            {
                localAiming = localPlayer.GetComponent<PlayerAiming>();
            }
        }

        if (localPlayer != null)
        {
            Vector2 forwardDir = Vector2.up;

            // Check aiming direction first
            if (localAiming != null && localAiming.GetAimDirection().sqrMagnitude > 0.01f)
            {
                forwardDir = localAiming.GetAimDirection();
            }
            else if (localPlayer.GetMoveDirection().sqrMagnitude > 0.01f)
            {
                forwardDir = localPlayer.GetMoveDirection();
            }

            // Top-down 2D: Up (0,1) is North (0°), Right (1,0) is East (90°), Down (0,-1) is South (180°), Left (-1,0) is West (270°)
            float angle = Mathf.Atan2(forwardDir.x, forwardDir.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            targetHeading = angle;
        }

        // Smoothly interpolate current heading
        currentHeading = Mathf.LerpAngle(currentHeading, targetHeading, Time.deltaTime * smoothSpeed);
        if (currentHeading < 0f) currentHeading += 360f;
        if (currentHeading >= 360f) currentHeading -= 360f;

        // Update Heading Text Readout
        if (headingText != null)
        {
            int intHead = Mathf.RoundToInt(currentHeading);
            if (intHead == 360) intHead = 0;
            string cardinal = GetCardinalShort(intHead);
            headingText.text = $"{intHead}° {cardinal}";
        }
    }

    private string GetCardinalShort(int deg)
    {
        if (deg >= 338 || deg < 23)   return "N";
        if (deg >= 23  && deg < 68)   return "NE";
        if (deg >= 68  && deg < 113)  return "E";
        if (deg >= 113 && deg < 158)  return "SE";
        if (deg >= 158 && deg < 203)  return "S";
        if (deg >= 203 && deg < 248)  return "SW";
        if (deg >= 248 && deg < 293)  return "W";
        return "NW";
    }

    private void UpdateCompassTapePosition()
    {
        if (compassTapeContent == null) return;

        float totalWidth = 360f * pixelsPerDegree;
        float normalizedHeading = currentHeading;
        float xOffset = -normalizedHeading * pixelsPerDegree;

        // Keep position within continuous loop range
        compassTapeContent.anchoredPosition = new Vector2(xOffset, 0f);
    }

    private void UpdateObjectiveMarkers()
    {
        if (localPlayer == null || compassBarRoot == null) return;

        Vector3 playerPos = localPlayer.transform.position;

        // Track Safe
        SafeController safe = SafeController.Instance ?? FindObjectOfType<SafeController>();
        if (safe != null && safe.gameObject.activeInHierarchy)
        {
            EnsureMarker("SafeMarker", safe.transform.position, "🔒 SAFE", new Color(1f, 0.8f, 0.1f, 1f));
        }

        // Track Main Gate
        MainGateController gate = MainGateController.Instance ?? FindObjectOfType<MainGateController>();
        if (gate != null && gate.gameObject.activeInHierarchy)
        {
            EnsureMarker("GateMarker", gate.transform.position, "🚪 EXIT", new Color(0.2f, 0.9f, 0.5f, 1f));
        }

        // Position all markers on the compass bar relative to player heading
        for (int i = activeMarkers.Count - 1; i >= 0; i--)
        {
            var m = activeMarkers[i];
            if (m.markerRect == null)
            {
                activeMarkers.RemoveAt(i);
                continue;
            }

            Vector3 targetPos = m.isStatic ? m.staticPosition : (m.targetTransform != null ? m.targetTransform.position : Vector3.zero);
            Vector2 toTarget = new Vector2(targetPos.x - playerPos.x, targetPos.y - playerPos.y);

            if (toTarget.sqrMagnitude < 0.1f)
            {
                m.markerRect.gameObject.SetActive(false);
                continue;
            }

            float targetAngle = Mathf.Atan2(toTarget.x, toTarget.y) * Mathf.Rad2Deg;
            if (targetAngle < 0f) targetAngle += 360f;

            float angleDiff = Mathf.DeltaAngle(currentHeading, targetAngle);
            float xPos = angleDiff * pixelsPerDegree;

            float halfWidth = (compassWidth / 2f) - 16f;
            if (Mathf.Abs(xPos) <= halfWidth)
            {
                m.markerRect.gameObject.SetActive(true);
                m.markerRect.anchoredPosition = new Vector2(xPos, 2f);
            }
            else
            {
                // Off-screen clamp or hide
                m.markerRect.gameObject.SetActive(false);
            }
        }
    }

    private void EnsureMarker(string id, Vector3 worldPos, string label, Color color)
    {
        CompassMarkerInstance inst = activeMarkers.Find(m => m.label == label);
        if (inst == null)
        {
            GameObject mGO = new GameObject($"Marker_{id}", typeof(RectTransform), typeof(TextMeshProUGUI));
            mGO.transform.SetParent(compassBarRoot.Find("CompassViewport") ?? compassBarRoot, false);

            RectTransform mRt = mGO.GetComponent<RectTransform>();
            mRt.anchorMin = new Vector2(0.5f, 0.5f);
            mRt.anchorMax = new Vector2(0.5f, 0.5f);
            mRt.pivot = new Vector2(0.5f, 0.5f);
            mRt.sizeDelta = new Vector2(60f, 26f);

            TextMeshProUGUI tmp = mGO.GetComponent<TextMeshProUGUI>();
            tmp.text = $"<size=9>{label}</size>\n<size=8>▼</size>";
            tmp.fontSize = 9;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;

            inst = new CompassMarkerInstance
            {
                staticPosition = worldPos,
                isStatic = true,
                markerRect = mRt,
                markerLabel = tmp,
                color = color,
                label = label
            };
            activeMarkers.Add(inst);
        }
        else
        {
            inst.staticPosition = worldPos;
        }
    }
}
