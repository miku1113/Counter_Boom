using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterSkin", menuName = "Game/Character Skin")]
public class CharacterSkinData : ScriptableObject
{
    [Header("Body Parts")]
    public Sprite head;
    public Sprite body;
    public Sprite leftArm;
    public Sprite rightArm;
    public Sprite leftLeg;
    public Sprite rightLeg;
    
    [Header("Face Parts")]
    public Sprite leftEye;
    public Sprite rightEye;
    public Sprite leftEyebrow;
    public Sprite rightEyebrow;
    public Sprite mouth;

    [Header("Shop & Display")]
    public string skinName = "Custom Skin";
    public int price = 150;
}

