using TMPro;
using UnityEngine;

public class PlaceholderScreenView : AppScreenViewBase
{
    private string title;
    private string headerIcon;
    private string bodyHeading;
    private string bodyCopy;

    public void Configure(string screenTitle, string iconName, string heading, string copy)
    {
        title = screenTitle;
        headerIcon = iconName;
        bodyHeading = heading;
        bodyCopy = copy;
    }

    protected override void BuildContent()
    {
        container.SetSpacing(18f);

        ScreenHeaderView header = UiFactory.CreateRect("Header", container.ContentColumn).gameObject.AddComponent<ScreenHeaderView>();
        header.Initialize(sprites, headerIcon, title);

        InfoCardView card = UiFactory.CreateRect("PrimaryCard", container.ContentColumn).gameObject.AddComponent<InfoCardView>();
        card.Initialize(158f);

        TextMeshProUGUI heading = UiFactory.CreateLabel("Heading", card.ContentRoot, bodyHeading, 22, UiTheme.BodyText, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        UiFactory.EnsureLayoutElement(heading.gameObject, -1f, 30f, 1f, 0f);

        TextMeshProUGUI copy = UiFactory.CreateLabel("Copy", card.ContentRoot, bodyCopy, 14, UiTheme.SubtleText, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        copy.textWrappingMode = TextWrappingModes.Normal;
        copy.overflowMode = TextOverflowModes.Overflow;
        UiFactory.EnsureLayoutElement(copy.gameObject, -1f, 84f, 1f, 0f);

        RectTransform spacer = UiFactory.CreateRect("FlexibleSpacer", container.ContentColumn);
        UiFactory.EnsureLayoutElement(spacer.gameObject, -1f, 0f, 1f, 1f);
    }
}
