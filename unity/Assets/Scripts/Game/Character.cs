using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour {

  [SerializeField] private CharacterData characterData;

  [SerializeField] private MeshRenderer bodyRenderer;


  public CharacterData CharacterData { get { return characterData; } }

  public void Init(CharacterData c) {
    characterData = c;

    // Temporary: Change the colour of the body of the character so we can distiguish one from another
    //bodyRenderer.material.SetColor("_BaseColor")

  }


  private void Awake() {}

}
