using UnityEngine;
using UnityEngine.UI;

namespace CounterBoom.UI
{
    /// <summary>
    /// Dedicated lightweight script for managing player preview character skins in UI panels.
    /// Handles skin sprite assignment and exact 2D sorting layer orders using SpriteRenderer and UI Image GameObjects.
    /// </summary>
    public class PlayerUIPreview : MonoBehaviour
    {
        [Header("Body Renderers")]
        [SerializeField] public SpriteRenderer headRenderer;
        [SerializeField] public SpriteRenderer bodyRenderer;
        [SerializeField] public SpriteRenderer leftArmRenderer;
        [SerializeField] public SpriteRenderer rightArmRenderer;
        [SerializeField] public SpriteRenderer leftLegRenderer;
        [SerializeField] public SpriteRenderer rightLegRenderer;

        [Header("Face Renderers")]
        [SerializeField] public SpriteRenderer leftEyeRenderer;
        [SerializeField] public SpriteRenderer rightEyeRenderer;
        [SerializeField] public SpriteRenderer leftEyebrowRenderer;
        [SerializeField] public SpriteRenderer rightEyebrowRenderer;
        [SerializeField] public SpriteRenderer mouthRenderer;

        [Header("Sorting Config")]
        [SerializeField] public string sortingLayerName = "UI";
        [SerializeField] public int baseSortingOrder = 500;

        [Header("Body Part Layer Orders (Relative to baseSortingOrder)")]
        [SerializeField] public int rightArmLayerOrder = 0;
        [SerializeField] public int leftLegLayerOrder = 0;
        [SerializeField] public int rightLegLayerOrder = 0;
        [SerializeField] public int bodyLayerOrder = 1;
        [SerializeField] public int headLayerOrder = 2;
        [SerializeField] public int mouthLayerOrder = 3;
        [SerializeField] public int leftEyebrowLayerOrder = 4;
        [SerializeField] public int rightEyebrowLayerOrder = 4;
        [SerializeField] public int leftEyeLayerOrder = 5;
        [SerializeField] public int rightEyeLayerOrder = 5;
        [SerializeField] public int leftArmLayerOrder = 6;

        public void EnsureCanvasSorting()
        {
            var cvs = GetComponent<Canvas>();
            if (cvs == null) cvs = gameObject.AddComponent<Canvas>();
            cvs.overrideSorting = true;
            cvs.sortingLayerName = sortingLayerName;
            cvs.sortingOrder = baseSortingOrder;

            var sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sg == null) sg = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            sg.sortingLayerName = sortingLayerName;
            sg.sortingOrder = baseSortingOrder;
        }

        public void ConvertToUIImageComponents()
        {
            SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (sr == null) continue;

                Image img = sr.gameObject.GetComponent<Image>();

                if (sr.sprite == null && (img == null || img.sprite == null))
                {
                    if (img != null) img.enabled = false;
                    sr.enabled = false;
                    sr.gameObject.SetActive(false);
                    continue;
                }

                sr.gameObject.SetActive(true);

                // Ensure RectTransform exists on GameObject
                RectTransform rt = sr.gameObject.GetComponent<RectTransform>();
                if (rt == null) rt = sr.gameObject.AddComponent<RectTransform>();

                // Add native UI Image component
                if (img == null) img = sr.gameObject.AddComponent<Image>();

                if (sr.sprite != null)
                {
                    img.sprite = sr.sprite;
                    img.enabled = true;
                }
                else if (img.sprite != null)
                {
                    img.enabled = true;
                }
                else
                {
                    img.enabled = false;
                    sr.gameObject.SetActive(false);
                }

                img.color = sr.color;
                img.preserveAspect = true;
                img.raycastTarget = false;

                // Disable SpriteRenderer so native UI Image handles rendering cleanly in Canvas
                sr.enabled = false;
            }
        }

        public void SetSkin(CharacterSkinData skin)
        {
            if (skin == null) return;

            AutoFindRenderers();

            // 1. Assign sprites to both SpriteRenderer and UI Image components
            SetRendererSprite(headRenderer, skin.head);
            SetRendererSprite(bodyRenderer, skin.body);
            SetRendererSprite(leftArmRenderer, skin.leftArm);
            SetRendererSprite(rightArmRenderer, skin.rightArm);
            SetRendererSprite(leftLegRenderer, skin.leftLeg);
            SetRendererSprite(rightLegRenderer, skin.rightLeg);

            SetRendererSprite(leftEyeRenderer, skin.leftEye);
            SetRendererSprite(rightEyeRenderer, skin.rightEye);
            SetRendererSprite(leftEyebrowRenderer, skin.leftEyebrow);
            SetRendererSprite(rightEyebrowRenderer, skin.rightEyebrow);
            SetRendererSprite(mouthRenderer, skin.mouth);

            // 2. Reorder sibling indices based on configured layer order values
            ApplyLayerSorting();

            UpdateSortingOrders();
            ConvertToUIImageComponents();
        }

        public void ApplyLayerSorting()
        {
            var parts = new System.Collections.Generic.List<(SpriteRenderer renderer, int order)>
            {
                (rightArmRenderer, rightArmLayerOrder),
                (leftLegRenderer, leftLegLayerOrder),
                (rightLegRenderer, rightLegLayerOrder),
                (bodyRenderer, bodyLayerOrder),
                (headRenderer, headLayerOrder),
                (mouthRenderer, mouthLayerOrder),
                (leftEyebrowRenderer, leftEyebrowLayerOrder),
                (rightEyebrowRenderer, rightEyebrowLayerOrder),
                (leftEyeRenderer, leftEyeLayerOrder),
                (rightEyeRenderer, rightEyeLayerOrder),
                (leftArmRenderer, leftArmLayerOrder)
            };

            // Stable sort by layer order number ascending (lowest layer number drawn first/behind)
            parts.Sort((a, b) => a.order.CompareTo(b.order));

            foreach (var part in parts)
            {
                if (part.renderer != null)
                {
                    part.renderer.transform.SetAsLastSibling();
                }
            }
        }

        private void SetRendererSprite(SpriteRenderer sr, Sprite sprite)
        {
            if (sr == null) return;

            if (sprite == null)
            {
                sr.enabled = false;
                Image existingImg = sr.gameObject.GetComponent<Image>();
                if (existingImg != null)
                {
                    existingImg.sprite = null;
                    existingImg.enabled = false;
                }
                sr.gameObject.SetActive(false);
                return;
            }

            if (sr.transform.parent != null) sr.transform.parent.gameObject.SetActive(true);
            sr.gameObject.SetActive(true);
            sr.sprite = sprite;
            sr.enabled = true;

            Image img = sr.gameObject.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.enabled = true;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
        }

        public void UpdateSortingOrders()
        {
            // Exact layer order per body part (baseSortingOrder + individual layer number):
            SetSorting(rightArmRenderer, baseSortingOrder + rightArmLayerOrder);
            SetSorting(leftLegRenderer, baseSortingOrder + leftLegLayerOrder);
            SetSorting(rightLegRenderer, baseSortingOrder + rightLegLayerOrder);

            SetSorting(bodyRenderer, baseSortingOrder + bodyLayerOrder);
            SetSorting(headRenderer, baseSortingOrder + headLayerOrder);

            SetSorting(mouthRenderer, baseSortingOrder + mouthLayerOrder);
            SetSorting(leftEyebrowRenderer, baseSortingOrder + leftEyebrowLayerOrder);
            SetSorting(rightEyebrowRenderer, baseSortingOrder + rightEyebrowLayerOrder);
            SetSorting(leftEyeRenderer, baseSortingOrder + leftEyeLayerOrder);
            SetSorting(rightEyeRenderer, baseSortingOrder + rightEyeLayerOrder);

            SetSorting(leftArmRenderer, baseSortingOrder + leftArmLayerOrder);
        }

        private void SetSorting(SpriteRenderer sr, int order)
        {
            if (sr == null) return;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = order;
        }

        /// <summary>
        /// Auto-binds child SpriteRenderers if not manually assigned in Inspector.
        /// </summary>
        public void AutoFindRenderers()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                string n = r.name.ToLower();

                bool isEyebrow = n.Contains("eyebrow") || n.Contains("brow");

                if (isEyebrow)
                {
                    if (n.Contains("left") || n.Contains("_l") || n.EndsWith("l") || n.Contains("lefteyebro"))
                        leftEyebrowRenderer = r;
                    else if (n.Contains("right") || n.Contains("_r") || n.EndsWith("r") || n.Contains("righteyebro"))
                        rightEyebrowRenderer = r;
                    else if (leftEyebrowRenderer == null)
                        leftEyebrowRenderer = r;
                    else if (rightEyebrowRenderer == null)
                        rightEyebrowRenderer = r;
                }
                else if (n.Contains("eye"))
                {
                    if (n.Contains("left") || n.Contains("_l") || n.Contains("eye_l") || n.EndsWith("l") || n.Contains("lefteye"))
                        leftEyeRenderer = r;
                    else if (n.Contains("right") || n.Contains("_r") || n.Contains("eye_r") || n.EndsWith("r") || n.Contains("righteye"))
                        rightEyeRenderer = r;
                    else if (leftEyeRenderer == null)
                        leftEyeRenderer = r;
                    else if (rightEyeRenderer == null)
                        rightEyeRenderer = r;
                }
                else if (n.Contains("mouth") || n.Contains("lip"))
                {
                    mouthRenderer = r;
                }
                else if (n.Contains("leftarm") || n.Contains("left_arm") || n.Contains("arm_l"))
                {
                    leftArmRenderer = r;
                }
                else if (n.Contains("rightarm") || n.Contains("right_arm") || n.Contains("arm_r"))
                {
                    rightArmRenderer = r;
                }
                else if (n.Contains("leftleg") || n.Contains("left_leg") || n.Contains("leg_l"))
                {
                    leftLegRenderer = r;
                }
                else if (n.Contains("rightleg") || n.Contains("right_leg") || n.Contains("leg_r"))
                {
                    rightLegRenderer = r;
                }
                else if (n.Contains("body") || n.Contains("torso"))
                {
                    bodyRenderer = r;
                }
                else if (n.Contains("head"))
                {
                    headRenderer = r;
                }
            }
        }
    }
}
