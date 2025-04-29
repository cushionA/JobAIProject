using UnityEngine;

public class IdTestObject : MonoBehaviour
{

    /// <summary>
    /// インスタンスIDのキャッシュ
    /// </summary>
    [HideInInspector]
    public int objectId;

    public int objectHash;

    public int objectType;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        this.objectId = this.gameObject.GetInstanceID();
        this.objectHash = this.gameObject.GetHashCode();
    }

}

