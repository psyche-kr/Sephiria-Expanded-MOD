using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaExpanded
{
    internal sealed class HiddenRoomFinder : MonoBehaviour
    {
        private const string Message = "이 층에는 숨겨진 방이 있습니다.";
        private const string ArrowName = "SephiriaExpanded_HiddenRoomArrow";
        private const string BoxName = "SephiriaExpanded_Drag_HiddenRoom";
        private const string ArrowRootName = "SephiriaExpanded_HiddenRoomArrows";
        private const string ArrowTopName = "SephiriaExpanded_HiddenRoomArrowTop";
        private const float ReorderInterval = 1f;
        private const string LabelName = "SephiriaExpanded_Text_HiddenRoomDistance";
        private const float ScanInterval = 1f;
        private const float BoxScale = 1.15f;
        private const float MinBoxSize = 14f;
        private const float MaxRange = 200f;
        private const float ArrowMargin = 0.3125f;
        private const float LabelOffset = 14f;
        private const float LabelFontSize = 7f;

        private static readonly Vector3 PortalExtents = new Vector3(0.5f, 0.5f, 0f);
        private static readonly Vector2 LabelAnchor = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 LabelSize = new Vector2(40f, 10f);
        private static readonly HashSet<int> NotifiedWalls = new HashSet<int>();
        private static readonly HashSet<string> NotifiedFloors = new HashSet<string>();

        private static HiddenRoomFinder instance;

        private sealed class Arrow
        {
            internal UI_EnemyViewerElement Element;
            internal TMP_Text Label;
            internal float Scale = 1f;
        }

        private sealed class Box
        {
            internal GameObject Root;
            internal Image Image;
        }

        private readonly List<Vector3> portals = new List<Vector3>();
        private readonly List<Arrow> arrows = new List<Arrow>();
        private readonly List<Box> boxes = new List<Box>();
        private HiddenRoomTriggerCollider[] walls = new HiddenRoomTriggerCollider[0];
        private RectTransform arrowRoot;
        private RectTransform arrowTop;
        private UIRoot hudRoot;
        private float labelRatio = 1f;
        private float arrowPixelHeight;
        private float nextScan;
        private float nextReorder;

        internal static void Sync(GameObject host)
        {
            bool wanted = ModSettings.HiddenRoomFinderEnabled;
            if (wanted && instance == null)
            {
                instance = host.AddComponent<HiddenRoomFinder>();
            }
            else if (!wanted && instance != null)
            {
                Destroy(instance);
                instance = null;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;

            nextScan = Time.unscaledTime + ScanInterval;
            Scan();
            Notify();
        }

        private void LateUpdate()
        {
            Camera camera = GameCamera.Instance == null ? null : GameCamera.Instance.Camera;
            UI_EnemyViewer viewer = UIManager.Instance == null ? null : UIManager.Instance.GetElement<UI_EnemyViewer>();
            UI_Drag drag = UIManager.Instance == null ? null : UIManager.Instance.GetElement<UI_Drag>();
            PlayerAvatar player = LocalPlayer.Find();

            int usedArrows = 0;
            int usedBoxes = 0;
            if (camera != null && player != null)
            {
                RectTransform root = walls.Length > 0 || portals.Count > 0 ? GetArrowRoot() : null;
                if (root != null) KeepOnTop();

                foreach (HiddenRoomTriggerCollider wall in walls)
                {
                    if (wall == null || wall.boxCollider == null) continue;

                    Bounds bounds = wall.boxCollider.bounds;
                    Place(camera, viewer, drag, player, bounds.center, bounds.extents * BoxScale, ref usedArrows, ref usedBoxes);
                }

                foreach (Vector3 portal in portals)
                    Place(camera, viewer, drag, player, portal, PortalExtents, ref usedArrows, ref usedBoxes);
            }

            for (int i = usedArrows; i < arrows.Count; i++)
            {
                if (arrows[i].Element != null && arrows[i].Element.gameObject.activeSelf)
                    arrows[i].Element.gameObject.SetActive(false);

                if (arrows[i].Label != null && arrows[i].Label.gameObject.activeSelf)
                    arrows[i].Label.gameObject.SetActive(false);
            }

            for (int i = usedBoxes; i < boxes.Count; i++)
                if (boxes[i].Root != null && boxes[i].Root.activeSelf)
                    boxes[i].Root.SetActive(false);
        }

        private void OnDestroy()
        {
            NotifiedWalls.Clear();
            NotifiedFloors.Clear();

            foreach (Arrow arrow in arrows)
                if (arrow.Element != null) Destroy(arrow.Element.gameObject);

            arrows.Clear();

            foreach (Box box in boxes)
                if (box.Root != null) Destroy(box.Root);

            boxes.Clear();

            if (arrowRoot != null) Destroy(arrowRoot.gameObject);
            if (arrowTop != null) Destroy(arrowTop.gameObject);
        }

        private void Scan()
        {
            walls = FindObjectsByType<HiddenRoomTriggerCollider>(FindObjectsSortMode.None);

            portals.Clear();
            foreach (BreakableProp_HiddenPortal portal in FindObjectsByType<BreakableProp_HiddenPortal>(FindObjectsSortMode.None))
                if (IsTarget(portal)) portals.Add(portal.transform.position + (Vector3)portal.portalOffset);
        }

        private static bool IsTarget(BreakableProp_HiddenPortal portal)
        {
            return portal != null && portal.isConnected && !portal.IsBroken && portal.PassageDir == HiddenPortalSide.Entrance && portal.gameObject.activeInHierarchy;
        }

        private void Notify()
        {
            if (GameLogWriter.Instance == null || LocalPlayer.Find() == null) return;

            foreach (HiddenRoomTriggerCollider wall in walls)
            {
                if (wall == null || !NotifiedWalls.Add(wall.GetInstanceID())) continue;

                FloorGenerator floor = wall.GetComponentInParent<FloorGenerator>();
                string floorGuid = floor == null ? null : floor.guid;
                if (!string.IsNullOrEmpty(floorGuid) && !NotifiedFloors.Add(floorGuid)) continue;

                GameLogWriter.Instance.WriteLog(Message, Color.green);
            }
        }

        private static bool TryGetScreenBox(Camera camera, Vector3 center, Vector3 extents, out Rect box)
        {
            box = default(Rect);
            if (Vector3.Distance(camera.transform.position, center) > MaxRange) return false;

            Vector3 screenCenter = camera.WorldToScreenPoint(center);
            if (screenCenter.z < 0f) return false;

            Vector3 min = camera.WorldToScreenPoint(center - extents);
            Vector3 max = camera.WorldToScreenPoint(center + extents);
            float width = Mathf.Max(Mathf.Abs(max.x - min.x), MinBoxSize);
            float height = Mathf.Max(Mathf.Abs(max.y - min.y), MinBoxSize);
            box = new Rect(screenCenter.x - width * 0.5f, Screen.height - screenCenter.y - height * 0.5f, width, height);
            return true;
        }

        private void Place(Camera camera, UI_EnemyViewer viewer, UI_Drag drag, PlayerAvatar player, Vector3 center, Vector3 extents, ref int usedArrows, ref int usedBoxes)
        {
            Rect box;
            if (!TryGetScreenBox(camera, center, extents, out box)) return;

            if (box.Overlaps(new Rect(0f, 0f, Screen.width, Screen.height)))
                PlaceBox(camera, drag, box, ref usedBoxes);
            else
                PlaceArrow(camera, viewer, player, center, ref usedArrows);
        }

        private void PlaceBox(Camera camera, UI_Drag drag, Rect box, ref int used)
        {
            if (drag == null || arrowRoot == null) return;

            Box target = GetBox(drag, used);
            if (target == null) return;

            used++;

            Rect pixels = camera.pixelRect;
            RectTransform rect = target.Image.rectTransform;
            rect.anchorMin = new Vector2((box.xMin - pixels.x) / pixels.width, (Screen.height - box.yMax - pixels.y) / pixels.height);
            rect.anchorMax = new Vector2((box.xMax - pixels.x) / pixels.width, (Screen.height - box.yMin - pixels.y) / pixels.height);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            if (!target.Root.activeSelf) target.Root.SetActive(true);
        }

        private Box GetBox(UI_Drag template, int index)
        {
            while (boxes.Count <= index)
                boxes.Add(new Box());

            Box box = boxes[index];
            if (box.Root == null) CreateBox(template, box);

            return box.Root == null ? null : box;
        }

        private void CreateBox(UI_Drag template, Box box)
        {
            GameObject clone = Instantiate(template.gameObject, arrowRoot, false);
            UI_Drag copy = clone.GetComponent<UI_Drag>();
            Image image = copy == null ? null : copy.dragImage;
            if (image == null)
            {
                Destroy(clone);
                return;
            }

            Destroy(copy);
            clone.name = BoxName;
            image.raycastTarget = false;
            image.gameObject.SetActive(true);
            box.Root = clone;
            box.Image = image;
        }

        private void PlaceArrow(Camera camera, UI_EnemyViewer viewer, PlayerAvatar player, Vector3 center, ref int used)
        {
            if (viewer == null || viewer.enemyCursorPrefab == null || arrowRoot == null) return;

            Arrow arrow = GetArrow(camera, viewer, arrowTop, used);
            if (arrow == null) return;

            used++;

            Vector3 cameraPosition = camera.transform.position;
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 position = center;
            position.x = Mathf.Clamp(center.x, cameraPosition.x - halfWidth + ArrowMargin, cameraPosition.x + halfWidth - ArrowMargin);
            position.y = Mathf.Clamp(center.y, cameraPosition.y - halfHeight + ArrowMargin, cameraPosition.y + halfHeight - ArrowMargin);

            UI_EnemyViewerElement element = arrow.Element;
            Vector3 viewport = camera.WorldToViewportPoint(position);
            RectTransform rect = element.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(viewport.x, viewport.y);
                rect.anchorMax = rect.anchorMin;
                rect.anchoredPosition = Vector2.zero;
            }

            element.image.transform.eulerAngles = new Vector3(0f, 0f, HorayUtility.GetAngle(center, player.transform.position));
            element.image.color = Color.white;
            if (!element.gameObject.activeSelf) element.gameObject.SetActive(true);

            if (arrow.Label == null) return;

            Vector2 toCenter = new Vector2(cameraPosition.x - position.x, cameraPosition.y - position.y);
            if (toCenter.sqrMagnitude > 0.0001f) toCenter.Normalize();

            RectTransform label = arrow.Label.rectTransform;
            label.anchorMin = new Vector2(viewport.x, viewport.y);
            label.anchorMax = label.anchorMin;
            label.localScale = Vector3.one * arrow.Scale * labelRatio;
            label.anchoredPosition = toCenter * LabelOffset * arrow.Scale * labelRatio;
            arrow.Label.text = Mathf.RoundToInt(Vector3.Distance(cameraPosition, center)) + "m";
            arrow.Label.transform.rotation = Quaternion.identity;
            if (!arrow.Label.gameObject.activeSelf) arrow.Label.gameObject.SetActive(true);
        }

        private RectTransform GetArrowRoot()
        {
            if (arrowRoot != null) return arrowRoot;

            UIRoot hud = UIManager.Instance == null ? null : UIManager.Instance.GetRootFromType(EUIObjectPoolingParent.HUD);
            if (hud == null) return null;

            hudRoot = hud;
            GameObject rootObject = new GameObject(ArrowRootName, typeof(RectTransform));
            arrowRoot = rootObject.GetComponent<RectTransform>();
            arrowRoot.SetParent(hud.transform, false);
            arrowRoot.anchorMin = Vector2.zero;
            arrowRoot.anchorMax = Vector2.one;
            arrowRoot.offsetMin = Vector2.zero;
            arrowRoot.offsetMax = Vector2.zero;
            GameObject topObject = new GameObject(ArrowTopName, typeof(RectTransform));
            arrowTop = topObject.GetComponent<RectTransform>();
            arrowTop.SetParent(hud.transform, false);
            arrowTop.anchorMin = Vector2.zero;
            arrowTop.anchorMax = Vector2.one;
            arrowTop.offsetMin = Vector2.zero;
            arrowTop.offsetMax = Vector2.zero;
            nextReorder = 0f;
            LogHierarchy(hud.transform);
            return arrowRoot;
        }

        private static Camera ScreenCamera(Canvas canvas, Camera fallback)
        {
            if (canvas == null) return fallback;
            if (canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;

            return canvas.rootCanvas.worldCamera != null ? canvas.rootCanvas.worldCamera : fallback;
        }

        private static float PixelHeight(RectTransform rect, Camera camera)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 bottom = RectTransformUtility.WorldToScreenPoint(ScreenCamera(rect.GetComponentInParent<Canvas>(), camera), corners[0]);
            Vector2 top = RectTransformUtility.WorldToScreenPoint(ScreenCamera(rect.GetComponentInParent<Canvas>(), camera), corners[1]);
            return Mathf.Abs(top.y - bottom.y);
        }

        private static void LogHierarchy(Transform parent)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder("[Sephiria Expanded][HiddenRoom] HUD 계층: ");
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            builder.Append(parent.name).Append(canvas == null ? " (캔버스 없음)" : " canvas=" + canvas.name + ", mode=" + canvas.renderMode + ", order=" + canvas.sortingOrder + ", layer=" + canvas.sortingLayerName);
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                builder.Append("\n  [").Append(i).Append("] ").Append(child.name).Append(child.gameObject.activeSelf ? string.Empty : " (비활성)");
            }

            foreach (UIRoot root in FindObjectsByType<UIRoot>(FindObjectsSortMode.None))
            {
                Canvas rootCanvas = root.Canvas;
                builder.Append("\n  UIRoot ").Append(root.name).Append(": ").Append(rootCanvas == null ? "캔버스 없음" : "mode=" + rootCanvas.renderMode + ", order=" + rootCanvas.sortingOrder + ", layer=" + rootCanvas.sortingLayerName);
            }

            Debug.Log(builder.ToString());
        }

        private void KeepOnTop()
        {
            if (arrowRoot == null || arrowTop == null || hudRoot == null || hudRoot.Canvas == null || Time.unscaledTime < nextReorder) return;

            nextReorder = Time.unscaledTime + ReorderInterval;

            List<UIBase> overlays = new List<UIBase>();
            UIRoot host = hudRoot;
            foreach (UIBase element in FindObjectsByType<UIBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsOverlayElement(element)) continue;

                UIRoot owner = element.GetComponentInParent<UIRoot>(true);
                if (owner == null || owner.Canvas == null) continue;

                overlays.Add(element);
                if (owner.Canvas.sortingOrder < host.Canvas.sortingOrder) host = owner;
            }

            Transform parent = host.transform;
            if (arrowRoot.parent != parent)
            {
                arrowRoot.SetParent(parent, false);
                LogOverlays(overlays, host);
            }

            labelRatio = host.Canvas.scaleFactor > 0f ? hudRoot.Canvas.scaleFactor / host.Canvas.scaleFactor : 1f;

            List<Transform> tops = new List<Transform>();
            foreach (UIBase element in overlays)
            {
                if (element.GetComponentInParent<UIRoot>(true) != host) continue;

                Transform top = element.transform;
                while (top.parent != null && top.parent != parent)
                    top = top.parent;

                if (top.parent == parent && top != arrowRoot && top != arrowTop && !tops.Contains(top))
                    tops.Add(top);
            }

            tops.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));

            bool topHere = arrowTop.parent == parent;
            int rootIndex = parent.childCount - tops.Count - (topHere ? 2 : 1);
            bool placed = arrowRoot.GetSiblingIndex() == rootIndex && (!topHere || arrowTop.GetSiblingIndex() == parent.childCount - 1);
            for (int i = 0; placed && i < tops.Count; i++)
                placed = tops[i].GetSiblingIndex() == rootIndex + 1 + i;

            if (!placed)
            {
                arrowRoot.SetAsLastSibling();
                foreach (Transform top in tops)
                    top.SetAsLastSibling();

                if (topHere) arrowTop.SetAsLastSibling();
            }

            if (!topHere && arrowTop.GetSiblingIndex() != arrowTop.parent.childCount - 1) arrowTop.SetAsLastSibling();
        }

        private static void LogOverlays(List<UIBase> overlays, UIRoot host)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder("[Sephiria Expanded][HiddenRoom] 텍스트 컨테이너 위치: " + host.name + " (order=" + host.Canvas.sortingOrder + ")");
            foreach (UIBase element in overlays)
            {
                UIRoot owner = element.GetComponentInParent<UIRoot>(true);
                builder.Append("\n  ").Append(element.GetType().Name).Append(" -> ").Append(owner == null ? "?" : owner.name + " (order=" + (owner.Canvas == null ? "?" : owner.Canvas.sortingOrder.ToString()) + ")");
            }

            Debug.Log(builder.ToString());
        }

        private static bool IsOverlayElement(UIBase element)
        {
            return element is UI_ChatInput ||
                   element is UI_HUDLogViewer ||
                   element is UI_HPBar ||
                   element is UI_HeartBar ||
                   element is UI_PlayerMP ||
                   element is UI_MiniMPBar ||
                   element is UI_DashBar ||
                   element is UI_HUDMapViewer ||
                   element is UI_MapPanel;
        }

        private Arrow GetArrow(Camera camera, UI_EnemyViewer viewer, RectTransform root, int index)
        {
            while (arrows.Count <= index)
                arrows.Add(new Arrow());

            Arrow arrow = arrows[index];
            if (arrow.Element == null) CreateArrow(camera, viewer, root, arrow);

            return arrow.Element == null ? null : arrow;
        }

        private void CreateArrow(Camera camera, UI_EnemyViewer viewer, RectTransform root, Arrow arrow)
        {
            Transform source = viewer.contentParent != null ? (Transform)viewer.contentParent : root;
            UI_EnemyViewerElement element = Instantiate(viewer.enemyCursorPrefab, source);
            if (element == null || element.image == null)
            {
                if (element != null) Destroy(element.gameObject);
                return;
            }

            if (arrowPixelHeight <= 0f) arrowPixelHeight = PixelHeight(element.image.rectTransform, camera);

            element.name = ArrowName;
            RectTransform rect = element.transform as RectTransform;
            element.transform.SetParent(root, false);
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = rect.anchorMin;
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                float current = PixelHeight(element.image.rectTransform, camera);
                if (current > 0f && arrowPixelHeight > 0f) rect.localScale = Vector3.one * (arrowPixelHeight / current);
                arrow.Scale = rect.localScale.x;
            }

            arrow.Element = element;
            arrow.Label = HudTextFactory.Create(LabelName, LabelAnchor, Vector2.zero, LabelSize, LabelFontSize, TextAlignmentOptions.Center);
            if (arrow.Label == null) return;

            arrow.Label.transform.SetParent(arrowRoot, false);
            arrow.Label.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
