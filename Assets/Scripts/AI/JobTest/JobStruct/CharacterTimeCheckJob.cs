using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

/// <summary>
/// 各キャラクターの特殊効果などの計測時間を一括で判断する。
/// 
/// 以下予定仕様
/// UnsafeListに時間情報を入れて、時間が経過していたら要素を削除し
/// 削除前にその要素の削除時の処理を行うこと（バフの解除や行動インターバルの経過による行動不能解除処理など）
/// 書き込み限定でキャラデータも持つ
/// となるとキャラデータの直のインデックスを時間データに持たせとかないといけないな
/// 要素の追加、削除でインデックスは変わるから現実的じゃない。
/// </summary>
public class CharacterTimeCheckJob : IJobParallelFor
{

    /// <summary>
    /// CharacterDataDicの変換後
    /// 時間経過後にステータスを元に戻したり、フラグを折ったりする。
    /// </summary>
    [Unity.Collections.WriteOnly]
    public UnsafeList<CharacterData> characterData;

    public void Execute(int index)
    {
        throw new System.NotImplementedException();
    }
}
