using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

/// <summary>
/// �e�L�����N�^�[�̓�����ʂȂǂ̌v�����Ԃ��ꊇ�Ŕ��f����B
/// 
/// �ȉ��\��d�l
/// UnsafeList�Ɏ��ԏ������āA���Ԃ��o�߂��Ă�����v�f���폜��
/// �폜�O�ɂ��̗v�f�̍폜���̏������s�����Ɓi�o�t�̉�����s���C���^�[�o���̌o�߂ɂ��s���s�\���������Ȃǁj
/// �������݌���ŃL�����f�[�^������
/// �ƂȂ�ƃL�����f�[�^�̒��̃C���f�b�N�X�����ԃf�[�^�Ɏ������Ƃ��Ȃ��Ƃ����Ȃ���
/// �v�f�̒ǉ��A�폜�ŃC���f�b�N�X�͕ς�邩�猻���I����Ȃ��B
/// </summary>
public class CharacterTimeCheckJob : IJobParallelFor
{

    /// <summary>
    /// CharacterDataDic�̕ϊ���
    /// ���Ԍo�ߌ�ɃX�e�[�^�X�����ɖ߂�����A�t���O��܂����肷��B
    /// </summary>
    [Unity.Collections.WriteOnly]
    public UnsafeList<CharacterData> characterData;

    public void Execute(int index)
    {
        throw new System.NotImplementedException();
    }
}
