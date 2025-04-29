using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

public class HashAndIDTest
{
    private GameObject[] gameObjects;
    private DicTestData[] components;

    // �e�X�g�ݒ�
    private int objectCount = 5000;  // �e�X�g�p�I�u�W�F�N�g��
    private int warmupCount = 3;      // �E�H�[���A�b�v��
    private int measurementCount = 10; // �e�X�g�v����

    // �œK���h�~�p�̕ϐ�
    private int dataSum;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // �e�X�g�I�u�W�F�N�g�̃��[�h�Ɛ���
        this.gameObjects = new GameObject[this.objectCount];
        this.components = new DicTestData[this.objectCount];

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(this.objectCount);

        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            // Addressables���g�p���ăC���X�^���X��
            var task = Addressables.InstantiateAsync("Assets/Prefab/TestPrefab/MyDicTest.prefab");
            tasks.Add(task);
        }

        // ���ׂẴI�u�W�F�N�g�����������̂�҂�
        foreach ( var task in tasks )
        {
            yield return task;
        }

        // �������ꂽ�I�u�W�F�N�g�ƕK�v�ȃR���|�[�l���g���擾
        for ( int i = 0; i < this.gameObjects.Length; i++ )
        {
            this.gameObjects[i] = tasks[i].Result;
            this.gameObjects[i].name = $"TestObject_{i}";
            this.components[i] = this.gameObjects[i].GetComponent<DicTestData>();

            // GetComponent����null�ł��邱�Ƃ��m�F
            Assert.IsNotNull(this.components[i], $"�I�u�W�F�N�g {i} �� DicTestData ��������܂���");
        }
    }

    [TearDown]
    public void TearDown()
    {

        // �Ō�ɏo�͂��邱�Ƃŉߏ�œK����h��
        UnityEngine.Debug.Log($"�e�X�g���ʊm�F�p�f�[�^���v: {this.dataSum}");
    }

    //
    // �����A�N�Z�X�e�X�g
    //

    [Test, Performance]
    public void Test_01_GetInstansID_Test()
    {
        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                localSum += this.gameObjects[i].GetInstanceID();
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_02_GetHashCode_Test()
    {

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                localSum += this.gameObjects[i].GetHashCode();
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_03_GetComponent_Test()
    {

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < this.gameObjects.Length; i++ )
            {
                localSum += this.gameObjects[i].GetComponent<DicTestData>().TestValue;
            }

            this.dataSum += localSum;
        })
        .WarmupCount(this.warmupCount)
        .MeasurementCount(this.measurementCount)
        .Run();
    }

}