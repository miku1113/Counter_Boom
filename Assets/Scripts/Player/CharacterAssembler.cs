using UnityEngine;
using Unity.Netcode;

public class CharacterAssembler : NetworkBehaviour
{
    [Header("Body Parts")]
    [SerializeField] private SpriteRenderer headRenderer;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer leftArmRenderer;
    [SerializeField] private SpriteRenderer rightArmRenderer;
    [SerializeField] private SpriteRenderer leftLegRenderer;
    [SerializeField] private SpriteRenderer rightLegRenderer;
    
    [Header("Face Parts")]
    [SerializeField] private SpriteRenderer leftEyeRenderer;
    [SerializeField] private SpriteRenderer rightEyeRenderer;
    [SerializeField] private SpriteRenderer leftEyebrowRenderer;
    [SerializeField] private SpriteRenderer rightEyebrowRenderer;
    [SerializeField] private SpriteRenderer mouthRenderer;
    
    [Header("Weapon")]
    [SerializeField] private Transform weaponAttachPoint;
    
    [Header("Sprite Sorting")]
    [SerializeField] public int baseSortingOrder = 0;

    [Header("Skins Customization")]
    [SerializeField] public CharacterSkinData[] availableSkins;
    
    private bool isFacingRight = false;

    // Network variable to sync custom skin index
    private readonly NetworkVariable<int> equippedSkinIndex = new NetworkVariable<int>(
        0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner
    );

    private void Awake()
    {
        equippedSkinIndex.OnValueChanged += OnSkinIndexChanged;
    }

    private void OnSkinIndexChanged(int oldVal, int newVal)
    {
        ApplySkinByIndex(newVal);
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        UpdateSortingLayers();

        if (IsOwner)
        {
            int equippedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
            string equippedName = PlayerPrefs.GetString("EquippedSkinName", "");

            // If name exists, match index by name in availableSkins
            if (availableSkins != null && availableSkins.Length > 0 && !string.IsNullOrEmpty(equippedName))
            {
                int matchedIndex = System.Array.FindIndex(availableSkins, s => s != null && s.skinName == equippedName);
                if (matchedIndex >= 0) equippedIndex = matchedIndex;
            }

            equippedSkinIndex.Value = equippedIndex;
            ApplySkinByIndex(equippedIndex);
        }
        else
        {
            ApplySkinByIndex(equippedSkinIndex.Value);
        }
    }

    private void Start()
    {
        UpdateSortingLayers();
        if (IsOwner || !IsSpawned)
        {
            LoadEquippedSkin();
        }
        else
        {
            ApplySkinByIndex(equippedSkinIndex.Value);
        }
    }

    /// <summary>
    /// Reads the equipped skin index and name from PlayerPrefs and applies it.
    /// Only called for the local player or non-networked preview objects.
    /// </summary>
    public void LoadEquippedSkin()
    {
        if (availableSkins == null || availableSkins.Length == 0) return;

        int equippedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
        string equippedName = PlayerPrefs.GetString("EquippedSkinName", "");

        CharacterSkinData targetSkin = null;

        if (!string.IsNullOrEmpty(equippedName))
        {
            targetSkin = System.Array.Find(availableSkins, s => s != null && s.skinName == equippedName);
        }

        if (targetSkin == null && equippedIndex >= 0 && equippedIndex < availableSkins.Length)
        {
            targetSkin = availableSkins[equippedIndex];
        }

        if (targetSkin != null)
        {
            SetCharacterSkin(targetSkin);
        }
    }

    public void ApplySkinByIndex(int index)
    {
        if (availableSkins != null && index >= 0 && index < availableSkins.Length && availableSkins[index] != null)
        {
            SetCharacterSkin(availableSkins[index]);
        }
        else if (IsOwner || !IsSpawned)
        {
            LoadEquippedSkin();
        }
    }

    public int GetEquippedSkinIndexNetworkValue() => equippedSkinIndex.Value;

#if UNITY_EDITOR
    [ContextMenu("Auto-Populate Skins")]
    public void AutoPopulateSkins()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CharacterSkinData");
        System.Collections.Generic.List<CharacterSkinData> skinList = new System.Collections.Generic.List<CharacterSkinData>();
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterSkinData>(path);
            if (asset != null) skinList.Add(asset);
        }

        skinList.Sort((a, b) => {
            bool aDefault = a.skinName != null && a.skinName.ToLower().Contains("default");
            bool bDefault = b.skinName != null && b.skinName.ToLower().Contains("default");
            if (aDefault && !bDefault) return -1;
            if (!aDefault && bDefault) return 1;
            return string.Compare(a.skinName ?? "", b.skinName ?? "", System.StringComparison.Ordinal);
        });

        availableSkins = skinList.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[CharacterAssembler] Auto-populated {availableSkins.Length} skins in sorted order.");
    }

    private void OnValidate()
    {
        AutoPopulateSkins();
    }
#endif

    
    /// <summary>
    /// Sets all body part sprites from a CharacterSkinData
    /// </summary>
    public void SetCharacterSkin(CharacterSkinData skin)
    {
        if (skin == null) return;
        
        // Body parts
        if (headRenderer != null) headRenderer.sprite = skin.head;
        if (bodyRenderer != null) bodyRenderer.sprite = skin.body;
        if (leftArmRenderer != null) leftArmRenderer.sprite = skin.leftArm;
        if (rightArmRenderer != null) rightArmRenderer.sprite = skin.rightArm;
        if (leftLegRenderer != null) leftLegRenderer.sprite = skin.leftLeg;
        if (rightLegRenderer != null) rightLegRenderer.sprite = skin.rightLeg;
        
        // Face parts
        if (leftEyeRenderer != null) leftEyeRenderer.sprite = skin.leftEye;
        if (rightEyeRenderer != null) rightEyeRenderer.sprite = skin.rightEye;
        if (leftEyebrowRenderer != null) leftEyebrowRenderer.sprite = skin.leftEyebrow;
        if (rightEyebrowRenderer != null) rightEyebrowRenderer.sprite = skin.rightEyebrow;
        if (mouthRenderer != null) mouthRenderer.sprite = skin.mouth;
    }
    
    // Removed obsolete EquipWeapon and weaponRenderer logic 
    // Weapon visuals are now handled by instantiated prefabs managed by WeaponController
    
    public void UnequipWeapon()
    {
        // Optional logic if needed
    }
    
    /// <summary>
    /// Flips character sprites (not transform) based on facing direction
    /// </summary>
    public void SetFacingDirection(bool facingRight)
    {
        if (isFacingRight == facingRight) return;
        
        isFacingRight = facingRight;
        
        // Character faces LEFT by default in sprite
        float scaleX = facingRight ? -1f : 1f;

        if (headRenderer != null)
        {
            headRenderer.transform.localScale = new Vector3(scaleX, 1f, 1f);
        }
        if (bodyRenderer != null)
        {
            bodyRenderer.transform.localScale = new Vector3(scaleX, 1f, 1f);
        }

        // Left and Right Arms don't have attachments, so standard flipX is fine
        if (leftArmRenderer != null) leftArmRenderer.flipX = facingRight;
        if (rightArmRenderer != null) rightArmRenderer.flipX = facingRight;

        // Parent container of legs contains LeftLeg and RightLeg. Scale-flip the container!
        if (leftLegRenderer != null && leftLegRenderer.transform.parent != null && leftLegRenderer.transform.parent != transform)
        {
            leftLegRenderer.transform.parent.localScale = new Vector3(scaleX, 1f, 1f);
        }
        else
        {
            if (leftLegRenderer != null) leftLegRenderer.flipX = facingRight;
            if (rightLegRenderer != null) rightLegRenderer.flipX = facingRight;
        }

        // Parent container of face parts contains eyes, mouth, eyebrows (FaceSprite GameObject). Scale-flip the container!
        Transform faceParent = null;
        if (leftEyeRenderer != null) faceParent = leftEyeRenderer.transform.parent;
        else if (rightEyeRenderer != null) faceParent = rightEyeRenderer.transform.parent;
        else if (mouthRenderer != null) faceParent = mouthRenderer.transform.parent;

        if (faceParent != null && faceParent != transform)
        {
            faceParent.localScale = new Vector3(scaleX, 1f, 1f);
        }

        // Don't flip weapon - it will rotate independently
    }
    
    /// <summary>
    /// Sets sprite sorting layers to ensure correct rendering order (back to front)
    /// Call this manually if sorting layers aren't correct
    /// </summary>
    public void UpdateSortingLayers()
    {
        int baseOrder = baseSortingOrder;

        // Behind Body
        if (rightArmRenderer != null) SetSorting(rightArmRenderer, baseOrder - 1);
        if (leftLegRenderer != null) SetSorting(leftLegRenderer, baseOrder - 1);
        if (rightLegRenderer != null) SetSorting(rightLegRenderer, baseOrder - 1);
        
        // Body Plane
        if (bodyRenderer != null) SetSorting(bodyRenderer, baseOrder + 0);
        
        // Head
        if (headRenderer != null) SetSorting(headRenderer, baseOrder + 1);
        
        // Face
        int faceOrder = baseOrder + 2;
        if (leftEyeRenderer != null) SetSorting(leftEyeRenderer, faceOrder);
        if (rightEyeRenderer != null) SetSorting(rightEyeRenderer, faceOrder);
        if (mouthRenderer != null) SetSorting(mouthRenderer, faceOrder);
        if (leftEyebrowRenderer != null) SetSorting(leftEyebrowRenderer, faceOrder);
        if (rightEyebrowRenderer != null) SetSorting(rightEyebrowRenderer, faceOrder);

        // Front Holding Arm
        if (leftArmRenderer != null) SetSorting(leftArmRenderer, baseOrder + 4);
        
        Debug.Log("[CharacterAssembler] Sorting layers updated!");
    }
    
    private void SetSorting(SpriteRenderer r, int order)
    {
        r.sortingLayerName = "Default";
        r.sortingOrder = order;
    }
    
    public Transform GetWeaponAttachPoint() => weaponAttachPoint;
    public bool IsFacingRight() => isFacingRight;
    
    public Transform GetHeadTransform() => headRenderer != null ? headRenderer.transform : null;
    public Transform GetLeftArmTransform() => leftArmRenderer != null ? leftArmRenderer.transform : null;
    public Transform GetRightArmTransform() => rightArmRenderer != null ? rightArmRenderer.transform : null;

    /// <summary>
    /// Adjusts the transparency of the entire character (including children renderers like equipped weapons).
    /// Used to hide players when they enter stealth zones (like smoke).
    /// </summary>
    public void SetVisibility(float alpha)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            Color c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }

    public CharacterSkinData[] GetAvailableSkins() => availableSkins;
}

