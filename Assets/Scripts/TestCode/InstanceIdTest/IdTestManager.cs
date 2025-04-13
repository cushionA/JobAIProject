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
    /// �e�X�g���Ƃɐ�������I�u�W�F�N�g
    /// </summary>
    [SerializeField]
    private AssetReference[] objReference;

    public string[] resultPath = new string[3];

    public int objectCount;

    /// <summary>
    /// �N�����ɃV���O���g���̃C���X�^���X�쐬�B
    /// </summary>
    private void Awake()
    {
        if ( instance == null )
        {
            instance = this; // this ����
            DontDestroyOnLoad(this); // �V�[���J�ڎ��ɔj������Ȃ��悤�ɂ���i�K�v�ł���΁j
                                     // Application.targetFrameRate = 60; // �Œ�t���[����
        }
        else
        {
            Destroy(this);
        }
    }

    /// <summary>
    /// �I�u�W�F�N�g����
    /// </summary>
    private void Start()
    {

        for ( int i = 0; i < objReference.Length; i++ )
        {
            for ( int j = 0; j < objectCount; j++ )
            {
                Addressables.InstantiateAsync(objReference[i]);
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

        int[] unMatchCount = new int[3];

        for ( int i = 0; i < objects.Length; i++ )
        {
            int id = objects[i].objectId;
            int hash = objects[i].objectHash;

            if ( id != hash )
            {
                ids[objects[i].objectType - 1].Add($"���ʁF�s��v (ID�F{id}) Hash�F({hash})");
                unMatchCount[objects[i].objectType - 1]++;
            }
            else
            {
                ids[objects[i].objectType - 1].Add($"���ʁF��v ({id})");
            }
        }

        for ( int i = 0; i < resultPath.Length; i++ )
        {

            string absolutePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    resultPath[i]);

            // �t�@�C���ɐV�����s��ǉ�����
            using ( StreamWriter sw = new StreamWriter(absolutePath, true) )
            {
                sw.WriteLine($"�I�u�W�F�N�g�^�C�v�F{i + 1}_{ids[i].Count}�� �s��v�F{unMatchCount[i]}��");
                for ( int j = 0; j < ids[i].Count; j++ )
                {
                    sw.WriteLine(ids[i][j]);
                }
                sw.WriteLine(string.Empty);// ��s
            }

        }

        // �e�X�g�I���B
        Application.Quit();
    }

}
