using UnityEngine;
using UnityEngine.UI;

// 가이드북 항목 한 줄(GuideEntry)을 화면에 채우는 뷰다.
// 프리팹(GuideEntryView)에 붙어 있고, GuidePageView가 쪽을 넘길 때마다 재사용한다.
public class GuideEntryView : MonoBehaviour
{
    [SerializeField] private GameObject iconBox;
    // 아이콘이 없는 항목에서는 상자째 숨기기 위해 참조한다.

    [SerializeField] private Image iconImage;
    [SerializeField] private Text titleText;
    [SerializeField] private GameObject badgeRoot;
    // 키 표시 배지 전체다. keyLabel이 비어 있으면 숨긴다.

    [SerializeField] private Text badgeText;
    [SerializeField] private Text bodyText;
    [SerializeField] private GameObject highlightRoot;
    [SerializeField] private Text highlightText;
    [SerializeField] private Transform titleRow;
    // 제목/배지가 놓이는 가로 줄이다. 짧은 항목은 설명도 이 줄에 같이 넣어 세로 공간을 아낀다.

    [SerializeField] private Transform textColumn;
    // 설명이 기본으로 놓이는 세로 열이다. (제목 줄 아래)

    private const int InlineBodyMaxLength = 16;
    // 이 글자 수 이하의 짧은 설명은 제목 옆에 한 줄로 붙인다. 페이지에 항목이 많을 때 넘치지 않게 하기 위해서다.

    public void Show(GuideEntry entry)
    {
        bool hasIcon = entry.icon != null;
        iconBox.SetActive(hasIcon);
        iconImage.sprite = entry.icon;

        bool hasTitle = !string.IsNullOrEmpty(entry.title);
        titleText.gameObject.SetActive(hasTitle);
        titleText.text = entry.title;

        bool hasKey = !string.IsNullOrEmpty(entry.keyLabel);
        badgeRoot.SetActive(hasKey);
        badgeText.text = entry.keyLabel;

        bool hasBody = !string.IsNullOrEmpty(entry.body);
        bodyText.gameObject.SetActive(hasBody);
        bodyText.text = entry.body;

        // 아이콘/강조 박스가 없고 설명이 짧거나(또는 제목 없이 키 배지만 있는) 항목은 설명을 같은 줄에 둔다.
        bool inlineBody = hasBody && !hasIcon && string.IsNullOrEmpty(entry.highlight)
            && (entry.body.Length <= InlineBodyMaxLength || !hasTitle);
        bodyText.transform.SetParent(inlineBody ? titleRow : textColumn, false);
        if (inlineBody) bodyText.transform.SetAsLastSibling();
        else bodyText.transform.SetSiblingIndex(1);

        bool hasHighlight = !string.IsNullOrEmpty(entry.highlight);
        highlightRoot.SetActive(hasHighlight);
        highlightText.text = "→ " + entry.highlight;
    }
}
