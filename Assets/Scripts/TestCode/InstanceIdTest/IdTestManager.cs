using Cysharp.Threading.Tasks;
using System.Security.Cryptography;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
//using UnityEngine.Rendering.Universal;
using System.IO;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.VisualScripting;
//using UnityEditor.U2D.Aseprite;

public class IdTestManager : MonoBehaviour
{

    public static IdTestManager instance;

    /// <summary>
    /// テストごとに生成するオブジェクト
    /// </summary>
    [SerializeField]
    private AssetReference[] objReference;

    public string[] resultPath = new string[3];

    public int objectCount;

    /// <summary>
    /// 起動時にシングルトンのインスタンス作成。
    /// </summary>
    private void Awake()
    {
        if ( instance == null )
        {
            instance = this; // this を代入
            DontDestroyOnLoad(this); // シーン遷移時に破棄されないようにする（必要であれば）
                                     // Application.targetFrameRate = 60; // 固定フレームで
        }
        else
        {
            Destroy(this);
        }
    }

    /// <summary>
    /// オブジェクト生成
    /// </summary>
    private void Start()
    {

        for ( int i = 0; i < objReference.Length; i++ )
        {
            for ( int j = 0; j < objectCount; j++ )
            {
                //              Addressables.InstantiateAsync(objReference[i]);
            }
        }

        TestEnd().Forget();
    }

    async UniTaskVoid TestEnd()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(10));

        IdTestObject[] objects = FindObjectsByType<IdTestObject>(sortMode: FindObjectsSortMode.None);


        List<string>[] ids = new List<string>[3];
        for ( int i = 0; i < ids.Length; i++ )
        {
            ids[i] = new List<string>();
        }

        for ( int i = 0; i < objects.Length; i++ )
        {
            ids[objects[i].objectType - 1].Add(objects[i].objectId.ToString());
        }

        for ( int i = 0; i < resultPath.Length; i++ )
        {

            string absolutePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    resultPath[i]);

            // ファイルに新しい行を追加する
            using ( StreamWriter sw = new StreamWriter(absolutePath, true) )
            {
                sw.WriteLine($"オブジェクトタイプ：{i + 1}_{ids[i].Count}個");
                for ( int j = 0; j < ids[i].Count; j++ )
                {
                    sw.WriteLine(ids[i][j]);
                }
                sw.WriteLine(string.Empty);// 空行
            }

        }

        // テスト終了。
        Application.Quit();
    }

}
