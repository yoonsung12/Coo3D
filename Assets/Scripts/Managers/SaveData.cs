using System;
using UnityEngine;

// ES3(Easy Save 3)로 저장/로드되는 세이브 데이터 묶음이다.
// 씬 전환이나 도구 해금처럼 아직 없는 개념은 담지 않고, 지금 있는 체크포인트 리스폰에 필요한 값만 담는다.
[Serializable]
public class SaveData
{
    public string sceneName;
    // 세이브 당시 활성화되어 있던 씬 이름이다. 스테이지가 여러 개 생기면 LoadGame에서 이 씬을 불러오는 데 사용한다.

    public Vector3 checkpointPosition;
    // 마지막으로 활성화된 체크포인트(리스폰 지점)의 월드 좌표다.

    public string savedAtIso;
    // 저장 시각을 ISO 8601 문자열로 기록한다. 세이브 슬롯 목록 UI 등에서 "언제 저장했는지" 표시할 때 사용한다.
}
