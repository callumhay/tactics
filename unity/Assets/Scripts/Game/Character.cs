using UnityEngine;

#pragma warning disable 649

public class Character : MonoBehaviour {

  [SerializeField] private CharacterData characterData;

  [SerializeField] private MeshRenderer bodyRenderer;


  public CharacterData CharacterData { get { return characterData; } }

  public void Init(CharacterData c) {
    characterData = c;
    bodyRenderer.material.SetColor("_BaseColor", c.Colour);

    // Temporary: Change the colour of the body of the character so we can distiguish one from another
    //bodyRenderer.material.SetColor("_BaseColor")

  }


  private void Awake() {}

}
