using System;
using System.Globalization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

// 세이브 슬롯 선택 화면에서 슬롯 한 칸(번호, 저장 시각 또는 "비어있음", 삭제 버튼)을 표시한다.
// 클릭 이벤트 처리는 SaveSlotSelectController가 슬롯 번호를 캡처한 람다로 직접 연결한다.
public class SaveSlotUIItem : MonoBehaviour
{
    [Title("연결")]
    [SerializeField, LabelText("상태 텍스트")]
    private Text stateText;
    // 저장 시각 또는 "비어있음"을 표시한다. 슬롯 번호 라벨("슬롯 1" 등)은 Inspector에서 고정 텍스트로 미리 넣어 둔다.

    [SerializeField, LabelText("선택 버튼")]
    private Button selectButton;

    [SerializeField, LabelText("삭제 버튼")]
    private Button deleteButton;

    public Button SelectButton => selectButton;
    public Button DeleteButton => deleteButton;

    // hasSave가 true면 저장 시각을 표시하고, false면 "비어있음"을 표시한다.
    // showDeleteButton은 화면 자체에서 삭제 기능을 쓸지(타이틀=true, 파우즈 중 저장=false) 결정한다.
    // 삭제할 대상이 없는 빈 슬롯에서는 showDeleteButton이 true여도 항상 숨긴다.
    public void SetState(bool hasSave, string savedAtIso, bool showDeleteButton = true)
    {
        stateText.text = hasSave ? FormatSavedAt(savedAtIso) : "비어있음";
        deleteButton.gameObject.SetActive(hasSave && showDeleteButton);
    }

    // SaveData.savedAtIso(ISO 8601, DateTime.UtcNow.ToString("o"))를 "yyyy-MM-dd HH:mm 저장됨" 형태로 바꾼다.
    // 파싱에 실패하면 원본 문자열을 그대로 보여준다.
    private string FormatSavedAt(string savedAtIso)
    {
        if (DateTime.TryParse(savedAtIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var savedAt))
            return $"{savedAt.ToLocalTime():yyyy-MM-dd HH:mm} 저장됨";

        return savedAtIso;
    }
}
