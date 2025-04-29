using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Profiling;

public class GameManager : MonoBehaviour
{

    private enum TestType
    {
        ����,
        �񓯊�,
        �ʏ����񓯊�,
        �}���`�X���b�h
    }

    /// <summary>
    /// �e�X�g���I������܂ł̎��ԁB
    /// </summary>
    [SerializeField]
    private float endTime;

    /// <summary>
    /// ��������L�����̐��B
    /// </summary>
    [SerializeField]
    private int genNumber;

    /// <summary>
    /// �񓯊��e�X�g�����邩�B
    /// </summary>
    [SerializeField]
    private TestType caseType;

    /// <summary>
    /// �e�X�g���Ƃɐ�������I�u�W�F�N�g
    /// </summary>
    [SerializeField]
    private AssetReference[] objReference;

    /// <summary>
    /// �L������z�u����ʒu�B
    /// </summary>
    [SerializeField]
    private Vector3[] spawnPositions = new Vector3[3];

    /// <summary>
    /// ���ʂ��������ރt�@�C���p�X�B
    /// </summary>
    [SerializeField]
    private string resultPath;

    /// <summary>
    /// �V���O���g���̃C���X�^���X
    /// </summary>
    [HideInInspector]
    public static GameManager instance;

    /// <summary>
    /// �^�ł���΂܂��e�X�g�������Ă���B
    /// </summary>
    [HideInInspector]
    public bool isTest = false;

    /// <summary>
    /// �ǂݎ���p�v���p�e�B�B
    /// </summary>
    [HideInInspector]
    public float NowTime => this.nowTime;

    /// <summary>
    /// �L�����Z���g�[�N���B
    /// </summary>
    [HideInInspector]
    public CancellationTokenSource cToken;

    /// <summary>
    /// ���݂̎���
    /// ���t���[���擾�B
    /// </summary>
    private float nowTime;

    /// <summary>
    /// �e�X�g�J�n���ԁB<br></br>
    /// �I�����Ԃ��͂��邽�߂ɕK�v�B
    /// </summary>
    private float startTime;

    // �������Ԍv��
    private Recorder rec;

    // �������Ԃ̃J�E���g�p
    private long totalTime;

    /// <summary>
    /// �v���t�@�C������������炤�B
    /// </summary>
   // ProfilerProperty profiler = new UnityEditorInternal.ProfilerProperty();

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
    /// �����������B
    /// </summary>
    private void Start()
    {
        // �񓯊����A�������Ő�������I�u�W�F�N�g��ύX�B
        AssetReference reference = this.objReference[(int)this.caseType];

        int posCount = this.spawnPositions.Length;

        for ( int i = 0; i < this.genNumber; i++ )
        {
            // �z�u�ʒu�ɏ��Ԃɕ��ׂĂ����B
            _ = reference.InstantiateAsync(this.spawnPositions[i % posCount], Quaternion.identity);

            if ( i % 100 == 0 )
            {
                //Debug.Log($"{i / 100}00 �I�u�W�F�N�g����");
            }
        }

        // �L�����Z���g�[�N�����쐬�B
        this.cToken = new CancellationTokenSource();

        // ���݂̎��Ԃ��擾�B
        this.nowTime = Time.time; // ���݂̃Q�[��������

        this.startTime = Time.time; // �J�n����

        // �e�X�g�J�n�B
        this.isTest = true;

        //Debug.Log($"{genNumber}�̃I�u�W�F�N�g�𐶐����A{caseType.ToString()}�e�X�g�J�n�B");

        // �I�u�W�F�N�g������Ɋϑ���L����
        this.rec = Recorder.Get("PlayerLoop");
        this.rec.enabled = true;
    }

    /// <summary>
    /// ���t���[�����݂̎��Ԃ��X�V����B
    /// </summary>
    private void Update()
    {
        //   //Debug.Log($"�e�X�g���F{GameManager.instance.isTest}");

        this.nowTime = Time.time;

        this.totalTime += this.rec.elapsedNanoseconds * 100;

        // �I�u�W�F�N�g�������Ԃ̕������I�����Ԃɉ��Z���ďI�����Ԃ��͂���B
        if ( this.nowTime - this.startTime >= this.endTime )
        {
            // �I�������J�n�B
            this.isTest = false;
            this.cToken.Cancel();

            AsyncTestBase[] objects = FindObjectsByType<AsyncTestBase>(sortMode: FindObjectsSortMode.None);

            // ��ƂȂ锻�f�񐔂̊��Ғl���擾�B�I�����Ԃ𔻒f�Ԋu�Ŋ���B
            int baseCount = (int)Mathf.Floor(this.endTime / objects[0].JudgeInterval);

            // ���Ғl�Ǝ��ۂ̔��f�񐔂Ƃ̊Ԃ̍��ق��i�[����B
            long divide = 0;

            for ( int i = 0; i < objects.Length; i++ )
            {
                // ���Ғl - �����f�� �����Z��������B
                divide += baseCount - objects[i].judgeCount;
            }

            // �t�@�C���ɐV�����s��ǉ�����
            //using ( StreamWriter sw = new StreamWriter(resultPath, true) )
            //{
            //    sw.WriteLine($"�e�X�g�敪�F{caseType.ToString()}");
            //    sw.WriteLine($"���s�����F{DateTime.Now.ToString()}");
            //    sw.WriteLine($"���Ғl�F{baseCount * genNumber}�� �ɑ΂��Ď����l�F{divide}�� �̍��فB");
            //    sw.WriteLine($"���ό덷�F{divide / genNumber} �I�u�W�F�N�g���F{genNumber}��");
            //    sw.WriteLine($"���������ԁF{totalTime}");
            //    sw.WriteLine(string.Empty);// ��s
            //}

            UnityEngine.Debug.Log($"�e�X�g�敪�F{this.caseType.ToString()} ���s�����F{DateTime.Now.ToString()}\n" +
            $"���Ғl�F{baseCount * this.genNumber}�� �ɑ΂��Ď����l�F{divide}�� �̍��فB\n" +
            $"���ό덷�F{divide / this.genNumber} �I�u�W�F�N�g���F{this.genNumber}�� ���������ԁF{this.totalTime}");// ��s

            // �e�X�g�I���B
            //EditorApplication.ExitPlaymode();
        }

    }
}
