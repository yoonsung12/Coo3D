using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 책의 한쪽(GuidePage)을 화면에 채우는 뷰다.
// 비어 있는 항목(부제목, 그림, 팁 등)은 숨겨서 레이아웃이 자동으로 당겨지게 한다.
public class GuidePageView : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Image heroImage;
    [SerializeField] private LayoutElement heroLayout;
    // 그림 높이(heroHeight)를 레이아웃에 전달하기 위한 컴포넌트다.

    [SerializeField] private Text descriptionText;
    [SerializeField] private RectTransform entriesRoot;
    [SerializeField] private GuideEntryView entryPrefab;
    [SerializeField] private GameObject tipRoot;
    [SerializeField] private Text tipTitleText;
    [SerializeField] private Text tipBodyText;

    private readonly List<GuideEntryView> _entryPool = new List<GuideEntryView>();
    // 쪽을 넘길 때마다 Instantiate/Destroy 하지 않고 만들어 둔 항목 뷰를 재사용한다.

    public void Show(GuidePage page)
    {
        SetText(titleText, page.title);
        SetText(subtitleText, page.subtitle);
        SetText(descriptionText, page.description);

        bool hasHero = page.heroImage != null;
        heroImage.gameObject.SetActive(hasHero);
        if (hasHero)
        {
            heroImage.sprite = page.heroImage;
            heroImage.preserveAspect = true;
            heroLayout.preferredHeight = page.heroHeight;
        }

        int count = page.entries != null ? page.entries.Count : 0;
        while (_entryPool.Count < count)
            _entryPool.Add(Instantiate(entryPrefab, entriesRoot));

        for (int i = 0; i < _entryPool.Count; i++)
        {
            bool used = i < count;
            _entryPool[i].gameObject.SetActive(used);
            if (used) _entryPool[i].Show(page.entries[i]);
        }
        entriesRoot.gameObject.SetActive(count > 0);

        bool hasTip = !string.IsNullOrEmpty(page.tipText) || !string.IsNullOrEmpty(page.tipTitle);
        tipRoot.SetActive(hasTip);
        tipTitleText.text = page.tipTitle;
        tipBodyText.text = page.tipText;

        // 쪽마다 내용이 바뀌므로 레이아웃을 바로 다시 계산해 한 프레임 깜빡임을 막는다.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }

    // 문자열이 비어 있으면 텍스트 오브젝트를 숨긴다.
    private static void SetText(Text target, string value)
    {
        bool has = !string.IsNullOrEmpty(value);
        target.gameObject.SetActive(has);
        target.text = value;
    }
}
