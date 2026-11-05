using System;
using System.Collections.Generic;
using UnityEngine;

// ES3(Easy Save 3)로 저장/로드되는 세이브 데이터 묶음이다.
// 체크포인트 리스폰에 필요한 값과, 진행 중 얻은 도구 해금 기록을 담는다.
[Serializable]
public class SaveData
{
    public string sceneName;
    // 세이브 당시 활성화되어 있던 씬 이름이다. 스테이지가 여러 개 생기면 LoadGame에서 이 씬을 불러오는 데 사용한다.

    public Vector3 checkpointPosition;
    // 마지막으로 활성화된 체크포인트(리스폰 지점)의 월드 좌표다.

    public string savedAtIso;
    // 저장 시각을 ISO 8601 문자열로 기록한다. 세이브 슬롯 목록 UI 등에서 "언제 저장했는지" 표시할 때 사용한다.

    public bool torchUnlocked;
    // 가을 퍼즐②에서 횃불을 얻었는지 기록한다. 이 필드가 없던 예전 세이브 파일은 false(미획득)로 읽힌다.
    // 겨울 이후 씬은 씬 설정상 처음부터 해금이라, 예전 세이브에서도 진행이 막히지 않는다.

    public int coinCount;
    // 저장 시점의 보유 코인이다. 사망 후 계속하기를 하면 이 값으로 코인이 되돌아간다.

    public List<string> activatedCheckpointIds;
    // 이미 지나간 체크포인트의 ID 목록이다. 다시 지나가도 저장을 반복하지 않게 하는 데 쓴다.
    // 이 필드가 없던 예전 세이브 파일은 null로 읽힐 수 있으므로, 불러올 때 null이면 빈 목록으로 취급해야 한다.

    public List<string> defeatedEnemyIds;
    // 저장 전에 처치한 적의 ID 목록이다. 씬을 다시 불러올 때 이 적들은 죽은 상태로 유지된다.
    // 예전 세이브 파일에는 없어서 null일 수 있다.

    public bool savedOnTube;
    // 물 위 튜브를 탄 채로 체크포인트가 저장됐는지 기록한다. 불러올 때 튜브를 그 근처로 옮기는 데 쓴다.
}
