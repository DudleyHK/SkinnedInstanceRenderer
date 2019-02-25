using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ViewSettings : ScriptableObject
{
	public GameObject MeleePrefab;
	public GameObject SkeletonPrefab;

    public const string FolderPath = "Assets/Resources";


    private static ViewSettings instance;

	public static ViewSettings Instance
	{
		get
		{
            if(instance == null)
            {
                instance = Resources.Load<ViewSettings>(typeof(ViewSettings).Name);

#if UNITY_EDITOR
                if(instance == null)
                {
                    ViewSettings asset = CreateInstance<ViewSettings>();
                    AssetDatabase.CreateAsset(asset, "Assets/Resources/" + "ViewSettings" + ".asset");
                    AssetDatabase.SaveAssets();
                }
#endif
            }

            return instance;
        }
	}
}
