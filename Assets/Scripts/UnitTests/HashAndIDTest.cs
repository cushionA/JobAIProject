using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Unity.Collections;
using Unity.PerformanceTesting;
using System;

public class HashAndIDTest
{
    private GameObject[] gameObjects;
    private DicTestData[] components;

    // �e�X�g�ݒ�
    private int objectCount = 50000;  // �e�X�g�p�I�u�W�F�N�g��
    private int warmupCount = 3;      // �E�H�[���A�b�v��
    private int measurementCount = 10; // �e�X�g�v����


    // �œK���h�~�p�̕ϐ�
    private int dataSum;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // �e�X�g�I�u�W�F�N�g�̃��[�h�Ɛ���
        gameObjects = new GameObject[objectCount];
        components = new DicTestData[objectCount];

        // �����̃I�u�W�F�N�g�����ŃC���X�^���X��
        var tasks = new List<AsyncOperationHandle<GameObject>>(objectCount);

        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            // Addressables���g�p���ăC���X�^���X��
            var task = Addressables.InstantiateAsync("MyDicTest");
            tasks.Add(task);
        }

        // ���ׂẴI�u�W�F�N�g�����������̂�҂�
        foreach ( var task in tasks )
        {
            yield return task;
        }

        // �������ꂽ�I�u�W�F�N�g�ƕK�v�ȃR���|�[�l���g���擾
        for ( int i = 0; i < gameObjects.Length; i++ )
        {
            gameObjects[i] = tasks[i].Result;
            gameObjects[i].name = $"TestObject_{i}";
            components[i] = gameObjects[i].GetComponent<DicTestData>();

            // GetComponent����null�ł��邱�Ƃ��m�F
            Assert.IsNotNull(components[i], $"�I�u�W�F�N�g {i} �� DicTestData ��������܂���");
        }
    }

    [TearDown]
    public void TearDown()
    {

        // �Ō�ɏo�͂��邱�Ƃŉߏ�œK����h��
        UnityEngine.Debug.Log($"�e�X�g���ʊm�F�p�f�[�^���v: {dataSum}");
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
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                localSum += gameObjects[i].GetInstanceID();
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

    [Test, Performance]
    public void Test_01_GetHashCode_Test()
    {

        Measure.Method(() =>
        {
            int localSum = 0;
            for ( int i = 0; i < gameObjects.Length; i++ )
            {
                localSum += gameObjects[i].GetHashCode();
            }
            dataSum += localSum;
        })
        .WarmupCount(warmupCount)
        .MeasurementCount(measurementCount)
        .Run();
    }

}