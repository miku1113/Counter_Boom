using UnityEngine;

public class CharacterAssembler : MonoBehaviour
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
    [SerializeField] private int baseSortingOrder = 0;
    
    private bool isFacingRight = true;
    
    private void Start()
    {
        UpdateSortingLayers();
    }
    
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
        // facingLeft = normal (no flip)
        // facingRight = flip sprites
        if (headRenderer != null) headRenderer.flipX = facingRight;
        if (bodyRenderer != null) bodyRenderer.flipX = facingRight;
        if (leftArmRenderer != null) leftArmRenderer.flipX = facingRight;
        if (rightArmRenderer != null) rightArmRenderer.flipX = facingRight;
        if (leftLegRenderer != null) leftLegRenderer.flipX = facingRight;
        if (rightLegRenderer != null) rightLegRenderer.flipX = facingRight;
        
        // Flip face parts
        if (leftEyeRenderer != null) leftEyeRenderer.flipX = facingRight;
        if (rightEyeRenderer != null) rightEyeRenderer.flipX = facingRight;
        if (leftEyebrowRenderer != null) leftEyebrowRenderer.flipX = facingRight;
        if (rightEyebrowRenderer != null) rightEyebrowRenderer.flipX = facingRight;
        if (mouthRenderer != null) mouthRenderer.flipX = facingRight;
        
        // Don't flip weapon - it will rotate independently
    }
    
    /// <summary>
    /// Sets sprite sorting layers to ensure correct rendering order (back to front)
    /// Call this manually if sorting layers aren't correct
    /// </summary>
    public void UpdateSortingLayers()
    {
        // User Requested Orders:
        // Right Arm: -1
        // Body: 0
        // Legs: 0/1? (User said "Legs 1, Left/Right 0" - maybe Container 1? Renderers 0?)
        // Let's assume Renderers should be ordered.
        // Left Arm: 2
        // Head: 3
        // Face: 4
    
        int baseOrder = baseSortingOrder;

        // Behind Body
        if (rightArmRenderer != null) SetSorting(rightArmRenderer, baseOrder - 1);
        
        // Body Plane
        if (leftLegRenderer != null) SetSorting(leftLegRenderer, baseOrder + 0); // Behind body?
        if (rightLegRenderer != null) SetSorting(rightLegRenderer, baseOrder + 0);
        if (bodyRenderer != null) SetSorting(bodyRenderer, baseOrder + 0);
        
        // Front
        if (leftArmRenderer != null) SetSorting(leftArmRenderer, baseOrder + 2);
        
        // Head
        if (headRenderer != null) SetSorting(headRenderer, baseOrder + 3);
        
        // Face
        int faceOrder = baseOrder + 4;
        if (leftEyeRenderer != null) SetSorting(leftEyeRenderer, faceOrder);
        if (rightEyeRenderer != null) SetSorting(rightEyeRenderer, faceOrder);
        if (mouthRenderer != null) SetSorting(mouthRenderer, faceOrder);
        if (leftEyebrowRenderer != null) SetSorting(leftEyebrowRenderer, faceOrder);
        if (rightEyebrowRenderer != null) SetSorting(rightEyebrowRenderer, faceOrder);
        
        Debug.Log("[CharacterAssembler] Sorting layers updated!");
    }
    
    private void SetSorting(SpriteRenderer r, int order)
    {
        r.sortingLayerName = "Default";
        r.sortingOrder = order;
    }
    
    public Transform GetWeaponAttachPoint() => weaponAttachPoint;
    public bool IsFacingRight() => isFacingRight;
    
    public Transform GetLeftArmTransform() => leftArmRenderer != null ? leftArmRenderer.transform : null;
    public Transform GetRightArmTransform() => rightArmRenderer != null ? rightArmRenderer.transform : null;
}
