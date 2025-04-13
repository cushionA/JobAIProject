using UnityEngine;

public class IdTestObject : MonoBehaviour
{

    /// <summary>
    /// インスタンスIDのキャッシュ
    /// </summary>
    [HideInInspector]
    public int objectId;

    public int objectType;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        objectId = this.gameObject.GetInstanceID();
    }




}

