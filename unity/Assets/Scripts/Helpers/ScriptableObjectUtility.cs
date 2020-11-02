using UnityEngine;
using UnityEditor;
using System.IO;
 
public static class ScriptableObjectUtility {

	/// <summary>
	//	This makes it easy to create, name and place unique new ScriptableObject asset files.
	/// </summary>
	public static T CreateAssetFromSelection<T>() where T : ScriptableObject {
		string path = AssetDatabase.GetAssetPath(Selection.activeObject);
		if (path == "") {
			path = "Assets";
		} 
		else if (Path.GetExtension (path) != "") {
      path = path.Replace (Path.GetFileName (AssetDatabase.GetAssetPath (Selection.activeObject)), "");
		}
    string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/New " + typeof(T).ToString() + ".asset");
    var asset = ScriptableObjectUtility.CreateAssetFromPath<T>(assetPathAndName);
    EditorUtility.FocusProjectWindow();
  	Selection.activeObject = asset;
    return asset;
	}

  public static T CreateAssetFromPath<T>(in string filePath) where T : ScriptableObject {
    T asset = ScriptableObject.CreateInstance<T>();
		AssetDatabase.CreateAsset(asset, filePath);
		AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
		return asset;
  }

  public static T LoadOrCreateAssetFromPath<T>(in string filepath) where T: ScriptableObject {
    var asset = AssetDatabase.LoadAssetAtPath<T>(filepath);
    return asset ?? CreateAssetFromPath<T>(filepath);
  }

}