using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class MapScreenView : AppScreenViewBase
{
    private enum MapMode
    {
        Base,
        Social,
        SocialChat,
        SocialAddFriend,
        SocialEnterClub,
        CompetitionCenter,
        Walk,
        KennelAdopt,
        KennelRelease
    }

    private enum FlagType
    {
        Usa,
        Japan,
        Germany
    }

    private struct ActivityProgressData
    {
        public string Title;
        public string ProgressText;
        public string RewardText;
        public float FillWidth;
    }

    private struct SocialMemberData
    {
        public string Name;
        public string LevelText;
        public bool IsHighlighted;
    }

    private struct SocialLeaderboardData
    {
        public string Name;
        public FlagType Flag;
        public string Score;
        public string Rank;
        public bool IsHighlighted;
    }

    private struct KennelRowData
    {
        public string Title;
        public string Description;
        public string ImageResource;
        public string ButtonText;
        public bool IsPriceButton;
    }

    private static readonly Color32 Cream = new Color32(252, 248, 232, 255);
    private static readonly Color32 CreamSupport = new Color32(236, 223, 200, 255);
    private static readonly Color32 Coral = new Color32(223, 120, 97, 255);
    private static readonly Color32 CoralDark = new Color32(138, 75, 60, 255);
    private static readonly Color32 CoralText = new Color32(227, 119, 95, 255);
    private static readonly Color32 BodyBrown = new Color32(138, 75, 60, 255);
    private static readonly Color32 BodyBlack = new Color32(0, 0, 0, 255);
    private static readonly Color32 Gray = new Color32(163, 163, 163, 255);
    private static readonly Color32 GrayLight = new Color32(220, 220, 220, 255);
    private static readonly Color32 CtaBlue = new Color32(50, 187, 255, 255);
    private static readonly Color32 CtaBlueDark = new Color32(0, 118, 177, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 FlagOutline = new Color32(163, 163, 163, 255);
    private static readonly Color32 EncounterCoral = new Color32(234, 130, 108, 255);
    private static readonly Color32 TransparentHit = new Color32(255, 255, 255, 1);
    private static readonly Vector2 SelectorArrowSize = new Vector2(16f, 24f);

    private static readonly ActivityProgressData[] GuildActivities =
    {
        new ActivityProgressData { Title = "Go for 100 walks", ProgressText = "60/100", RewardText = "150 XP", FillWidth = 140f },
        new ActivityProgressData { Title = "Win 20 competitions", ProgressText = "14/20", RewardText = "100 XP", FillWidth = 140f }
    };

    private static readonly SocialMemberData[] ClubMembers =
    {
        new SocialMemberData { Name = "DaveBrave", LevelText = "Level 18", IsHighlighted = false },
        new SocialMemberData { Name = "John Smith", LevelText = "Level 7", IsHighlighted = false },
        new SocialMemberData { Name = "DogLover5", LevelText = "Level 11", IsHighlighted = false }
    };

    private static readonly SocialLeaderboardData[] ClubLeaderboard =
    {
        new SocialLeaderboardData { Name = "Bark Brigade2", Flag = FlagType.Germany, Score = "3,800", Rank = "#134", IsHighlighted = false },
        new SocialLeaderboardData { Name = "Bark Brigade1", Flag = FlagType.Germany, Score = "3,700", Rank = "#135", IsHighlighted = false },
        new SocialLeaderboardData { Name = "Bark Brigade", Flag = FlagType.Germany, Score = "3,600", Rank = "#136", IsHighlighted = false },
        new SocialLeaderboardData { Name = "Paw Patrol", Flag = FlagType.Japan, Score = "3,450", Rank = "#137", IsHighlighted = true },
        new SocialLeaderboardData { Name = "Fur Friends Club", Flag = FlagType.Usa, Score = "3,300", Rank = "#138", IsHighlighted = false },
        new SocialLeaderboardData { Name = "Fur Friends Club1", Flag = FlagType.Usa, Score = "3,200", Rank = "#139", IsHighlighted = false },
        new SocialLeaderboardData { Name = "Fur Friends Club2", Flag = FlagType.Usa, Score = "3,100", Rank = "#140", IsHighlighted = false }
    };

    private static readonly KennelRowData[] KennelAdoptRows =
    {
        new KennelRowData
        {
            Title = "Labrador",
            Description = "Renowned for their strength and friendliness, Labradors have a love for the outdoors.",
            ImageResource = "UI/Figma/Map/kennel_labrador",
            ButtonText = "₱1,500",
            IsPriceButton = true
        },
        new KennelRowData
        {
            Title = "Husky",
            Description = "Huskies are playful, energetic, and full of mischief - your perfect adventure buddy.",
            ImageResource = "UI/Figma/Map/kennel_husky",
            ButtonText = "Shop",
            IsPriceButton = false
        },
        new KennelRowData
        {
            Title = "Husky",
            Description = "Huskies are playful, energetic, and full of mischief - your perfect adventure buddy.",
            ImageResource = "UI/Figma/Map/kennel_husky",
            ButtonText = "Shop",
            IsPriceButton = false
        },
        new KennelRowData
        {
            Title = "Husky",
            Description = "Huskies are playful, energetic, and full of mischief - your perfect adventure buddy.",
            ImageResource = "UI/Figma/Map/kennel_husky",
            ButtonText = "Shop",
            IsPriceButton = false
        }
    };

    private RectTransform exactFrame;
    private RectTransform baseRoot;
    private RectTransform walkRoot;
    private RectTransform competitionRoot;
    private RectTransform kennelAdoptRoot;
    private RectTransform kennelReleaseRoot;
    private RectTransform overlayLayer;
    private RectTransform socialSheetRoot;
    private RectTransform socialChatRoot;
    private RectTransform socialAddFriendRoot;
    private RectTransform socialEnterClubRoot;
    private PawPalAgilityTrialSelectionOverlay agilityTrialOverlay;
    private TextMeshProUGUI competitionDogNameLabel;
    private MapMode currentMode;
    private ResponsiveFigmaFrameLayout frameLayout;
    private MapWalkPlannerController walkPlanner;
    private WalkStaminaButtonView baseWalkButton;
    private WalkStaminaButtonView plannerWalkButton;
    private Coroutine quickWalkRoutine;
    private string lockedWalkDogId = string.Empty;
    private float walkPlannerPreviewCost;

    protected override bool UseScreenContainer
    {
        get { return false; }
    }

    private void OnEnable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged += HandleRuntimeStateChanged;
            CaptureLockedWalkDog(runtime);
        }

        if (exactFrame != null)
        {
            SetMode(MapMode.Base);
        }

        RefreshRuntimeState();
    }

    private void OnDisable()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.StateChanged -= HandleRuntimeStateChanged;
        }
    }

    protected override void BuildContent()
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        exactFrame = UiFactory.CreateRect("MapExactFrame", root);
        exactFrame.anchorMin = new Vector2(0.5f, 1f);
        exactFrame.anchorMax = new Vector2(0.5f, 1f);
        exactFrame.pivot = new Vector2(0.5f, 1f);
        exactFrame.sizeDelta = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
        exactFrame.anchoredPosition = Vector2.zero;

        baseRoot = CreateFullScreenRoot("BaseRoot");
        BuildBaseState(baseRoot);

        walkRoot = CreateFullScreenRoot("WalkRoot");
        BuildWalkState(walkRoot);

        competitionRoot = CreateFullScreenRoot("CompetitionRoot");
        BuildCompetitionState(competitionRoot);

        kennelAdoptRoot = CreateFullScreenRoot("KennelAdoptRoot");
        BuildKennelAdoptState(kennelAdoptRoot);

        kennelReleaseRoot = CreateFullScreenRoot("KennelReleaseRoot");
        BuildKennelReleaseState(kennelReleaseRoot);

        overlayLayer = CreateFullScreenRoot("OverlayLayer");
        socialSheetRoot = BuildSocialSheet(overlayLayer);
        socialChatRoot = BuildSocialChatModal(overlayLayer);
        socialAddFriendRoot = BuildSocialPromptModal(
            overlayLayer,
            "SocialAddFriend",
            85f,
            326.5f,
            223f,
            133f,
            "Enter your <b><color=#DF7861>friend's name</color></b> to send a request:",
            "Add",
            delegate { ToggleSocialAddFriend(); },
            delegate { Debug.Log("Map social add-friend confirm flow is unresolved in the provided Figma states."); });
        socialEnterClubRoot = BuildSocialPromptModal(
            overlayLayer,
            "SocialEnterClub",
            85f,
            326.5f,
            223f,
            133f,
            "Enter the <b><color=#DF7861>name of the club</color></b> that you want to join:",
            "Join",
            delegate { ToggleSocialEnterClub(); },
            delegate { Debug.Log("Map social join-club confirm flow is unresolved in the provided Figma states."); });

        SetMode(MapMode.Base);
        RefreshRuntimeState();
    }

    public override void ApplyLayout(UiLayoutBucket bucket)
    {
        RectTransform root = GetComponent<RectTransform>();
        UiFactory.Stretch(root, 0f, 0f, 0f, 0f);

        if (exactFrame != null)
        {
            frameLayout = ResponsiveFigmaFrame.Apply(root, exactFrame);
            ApplyResponsivePositions();
        }
    }

    private void BuildBaseState(RectTransform parent)
    {
        CreateBackgroundImage(parent, "MapMainBackground", "UI/Figma/Map/walking_map", -46f, 0f, 524f, 786f);

        baseWalkButton = CreateWalkButton(parent, 11f, 699f, delegate
        {
            SetMode(MapMode.Walk);
        });

        CreateSocialButton(parent, 326f, 708f, delegate
        {
            SetMode(MapMode.Social);
        });
    }

    private void BuildWalkState(RectTransform parent)
    {
        CreateBackgroundImage(parent, "WalkBackground", "UI/Figma/Map/walking_map", -46f, 0f, 524f, 786f);
        plannerWalkButton = CreateWalkButton(parent, 11f, 699f, null);
        walkPlanner = parent.gameObject.AddComponent<MapWalkPlannerController>();
        walkPlanner.SetLockedDogId(lockedWalkDogId);
        walkPlanner.PlanPreviewChanged += HandleWalkPlanPreviewChanged;
        walkPlanner.Initialize(shell, sprites, delegate
        {
            SetMode(MapMode.Base);
        });
    }

    private static void OpenKennelIntroScene()
    {
        if (!PawPalIntroSceneFlow.LoadIntroScene())
        {
            Debug.LogWarning("Map kennel transition could not load the IntroPetSelection scene.");
        }
    }

    private void TryStartQuickWalk()
    {
        if (quickWalkRoutine != null)
        {
            return;
        }

        quickWalkRoutine = StartCoroutine(TryStartQuickWalkRoutine());
    }

    private IEnumerator TryStartQuickWalkRoutine()
    {
        const float transitionReadyNudgeSeconds = 1.5f;
        const float transitionForceReleaseSeconds = 3f;
        const float transitionWaitTimeoutSeconds = 6f;
        float startedWaitingAt = Time.unscaledTime;
        bool nudgedReady = false;
        bool forcedRelease = false;
        while (PawPalSceneTransitionController.IsTransitionActive && Time.unscaledTime < startedWaitingAt + transitionWaitTimeoutSeconds)
        {
            if (!nudgedReady && Time.unscaledTime >= startedWaitingAt + transitionReadyNudgeSeconds)
            {
                PawPalSceneTransitionController.MarkActiveTransitionReady("Map quick walk is waiting for a previous transition to finish.");
                nudgedReady = true;
            }

            if (!forcedRelease && Time.unscaledTime >= startedWaitingAt + transitionForceReleaseSeconds)
            {
                PawPalSceneTransitionController.ForceCompleteActiveTransition("Map quick walk recovered from a stale previous transition.");
                forcedRelease = true;
            }

            yield return null;
        }

        if (PawPalSceneTransitionController.IsTransitionActive)
        {
            Debug.LogWarning("Map quick walk timed out waiting for the previous scene transition to finish.");
            quickWalkRoutine = null;
            yield break;
        }

        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            Debug.LogWarning("Map quick walk could not start because the runtime is unavailable.");
            quickWalkRoutine = null;
            yield break;
        }

        if (runtime.ActiveDog == null)
        {
            Debug.LogWarning("Map quick walk could not start because no active dog is selected.");
            quickWalkRoutine = null;
            yield break;
        }

        runtime.CancelActiveWalkSession();

        string failureMessage;
        if (!runtime.TryStartDeferredWalkSession(PawPalWalkSceneFlow.HomeSceneName, out failureMessage))
        {
            Debug.LogWarning("Map quick walk could not start: " + failureMessage);
            quickWalkRoutine = null;
            yield break;
        }

        if (!PawPalWalkSceneFlow.LoadWalkScene())
        {
            runtime.CancelActiveWalkSession();
            Debug.LogWarning("Map quick walk could not load the walking scene.");
        }

        quickWalkRoutine = null;
    }

    private void BuildCompetitionState(RectTransform parent)
    {
        CreateBackgroundImage(parent, "CompetitionBackground", "UI/Figma/Map/competition_center_background", 0f, 0f, 393f, 786f);

        RectTransform panel = CreateNode("CompetitionPanel", parent, 21f, 165f, 350f, 385f);
        Image panelFill = panel.gameObject.AddComponent<Image>();
        panelFill.sprite = UiTheme.RoundedTenSprite;
        panelFill.type = Image.Type.Sliced;
        panelFill.preserveAspect = false;
        panelFill.color = Cream;

        CreateHeaderTextBox(panel, "Title", "Competition center", 20f, 15f, 310f, 24f);

        TextMeshProUGUI description = CreateText(panel, "Description", "Select your dog and choose a competition to enter together.", 14, BodyBrown, UiTheme.NavRegularFont, TextAlignmentOptions.Center);
        description.rectTransform.anchorMin = new Vector2(0f, 1f);
        description.rectTransform.anchorMax = new Vector2(0f, 1f);
        description.rectTransform.pivot = new Vector2(0f, 1f);
        description.rectTransform.sizeDelta = new Vector2(338f, 42f);
        description.rectTransform.anchoredPosition = new Vector2(6f, -51f);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Overflow;

        CreateNameSelector(panel, 20f, 105f, 310f);
        CreateSectionLineHeader(panel, "Competitions", 0f, 141f, 350f, 131.509f);

        CreateCompetitionRow(panel, "ObedienceRow", 0f, 174f, "Obedience Trial", "Beginner / Amateur", true);
        CreateCompetitionRow(panel, "AgilityRow", 0f, 226f, "Agility Trial", "Practice first", true);
        CreateCompetitionRow(panel, "DiscRow", 0f, 278f, "Disc (coming soon)", null, false);
        CreateCompetitionRow(panel, "StyleRow", 0f, 330f, "Style (coming soon)", null, false);
    }

    private void BuildKennelAdoptState(RectTransform parent)
    {
        RectTransform selectionPanel = CreateNode("SelectionPanel", parent, 7f, 519f, 378f, 267f);
        Image selectionFill = selectionPanel.gameObject.AddComponent<Image>();
        selectionFill.sprite = UiTheme.WhiteSprite;
        selectionFill.type = Image.Type.Simple;
        selectionFill.preserveAspect = false;
        selectionFill.color = Cream;
        CreateOutline(selectionPanel, Coral, 1f);

        RectTransform selectionContent;
        CreateScrollArea(selectionPanel, "SelectionScroll", 0f, 0f, 378f, 267f, 395f, out selectionContent);
        BuildKennelAdoptContent(selectionContent);

        RectTransform topPanel = CreateNode("TopPanel", parent, 7f, 446f, 378f, 84.0175f);
        BuildKennelTopPanel(topPanel, true);
    }

    private void BuildKennelReleaseState(RectTransform parent)
    {
        RectTransform selectionPanel = CreateNode("ReleaseSelectionPanel", parent, 7f, 519f, 378f, 267f);
        Image selectionFill = selectionPanel.gameObject.AddComponent<Image>();
        selectionFill.sprite = UiTheme.WhiteSprite;
        selectionFill.type = Image.Type.Simple;
        selectionFill.preserveAspect = false;
        selectionFill.color = Cream;
        CreateOutline(selectionPanel, Coral, 1f);

        RectTransform content = UiFactory.CreateRect("Content", selectionPanel);
        UiFactory.Stretch(content, 0f, 0f, 0f, 0f);
        BuildKennelReleaseContent(content);

        RectTransform topPanel = CreateNode("TopPanel", parent, 7f, 446f, 378f, 84.0175f);
        BuildKennelTopPanel(topPanel, false);
    }

    private RectTransform BuildSocialSheet(RectTransform parent)
    {
        const float socialX = 89f;
        const float socialY = 153f;

        RectTransform sheet = CreateNode("SocialSheet", parent, socialX, socialY, 294f, 618f);
        Image fill = sheet.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;

        CreateOutline(sheet, Coral, 1f);
        Shadow shadow = sheet.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = true;

        RectTransform contentViewport;
        RectTransform content;
        CreateScrollArea(sheet, "ContentViewport", 10f, 15f, 274f, 542f, 642f, out content, out contentViewport);
        BuildSocialContent(content);

        RectTransform bottomRow = CreateNode("BottomRow", sheet, 10f, 577f, 274f, 36f);
        CreateSocialRoundButton(bottomRow, "AddFriendButton", 20f, 0f, "icon_plus", ToggleSocialAddFriend);
        CreateSocialRoundButton(bottomRow, "ChatButton", 119f, 0f, "icon_friends_brand", ToggleSocialChat);
        CreateSocialRoundButton(bottomRow, "JoinButton", 218f, 0f, "icon_join_brand", ToggleSocialEnterClub);

        return sheet;
    }

    private RectTransform BuildSocialChatModal(RectTransform parent)
    {
        RectTransform modal = CreateNode("SocialChatModal", parent, 37.5f, 245f, 318f, 296f);
        Image fill = modal.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;
        CreateOutline(modal, Coral, 1f);

        CreateHeaderTextBox(modal, "Title", "“Paw Patrol” chatroom", 10f, 15.9855f, 298f, 24f);

        RectTransform firstBubble = CreateNode("Member1Bubble", modal, 10f, 44.9855f, 208f, 101f);
        BuildChatBubble(firstBubble, new Color32(253, 253, 253, 255), Gray, "Member1", "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.", false);

        RectTransform secondBubble = CreateNode("MeBubble", modal, 100f, 150.9855f, 208f, 101f);
        BuildChatBubble(secondBubble, new Color32(241, 236, 226, 255), Coral, "Me", "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.", true);

        CreateLine(modal, "ChatDivider", 10f, 257f, 298f, CreamSupport);

        CreateInputField(modal, "ChatField", 10f, 262f, 235f, 21f, "Tap to begin chatting.");

        CreateActionButton(modal, "SendButton", "Send", 260f, 262f, 48f, true, delegate
        {
            Debug.Log("Map social chat send flow is unresolved in the provided Figma states.");
        });

        return modal;
    }

    private RectTransform BuildSocialPromptModal(RectTransform parent, string name, float x, float y, float width, float height, string richText, string confirmLabel, UnityEngine.Events.UnityAction cancelAction, UnityEngine.Events.UnityAction confirmAction)
    {
        RectTransform modal = CreateNode(name, parent, x, y, width, height);
        Image fill = modal.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;
        CreateOutline(modal, Coral, 1f);

        TextMeshProUGUI prompt = CreateText(modal, "Prompt", richText, 13, BodyBrown, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        prompt.rectTransform.anchorMin = new Vector2(0f, 1f);
        prompt.rectTransform.anchorMax = new Vector2(0f, 1f);
        prompt.rectTransform.pivot = new Vector2(0f, 1f);
        prompt.rectTransform.sizeDelta = new Vector2(width - 20f, 38f);
        prompt.rectTransform.anchoredPosition = new Vector2(10f, -10f);
        prompt.textWrappingMode = TextWrappingModes.Normal;
        prompt.overflowMode = TextOverflowModes.Overflow;
        prompt.richText = true;

        CreateInputField(modal, "InputField", 10f, 63f, width - 20f, 21f, "Enter a name...");

        float optionsX = confirmLabel == "Join" ? 46.5f : 48f;
        float confirmWidth = confirmLabel == "Join" ? 44f : 41f;
        float cancelGap = 27f;
        float optionsWidth = 59f + cancelGap + confirmWidth;
        RectTransform options = CreateNode("Options", modal, optionsX, 99f, optionsWidth, 24f);
        CreateActionButton(options, "CancelButton", "Cancel", 0f, 0f, 59f, false, cancelAction);
        CreateActionButton(options, "ConfirmButton", confirmLabel, 59f + cancelGap, 0f, confirmWidth, true, confirmAction);
        return modal;
    }

    private void BuildSocialContent(RectTransform content)
    {
        RectTransform friendsSection = CreateNode("FriendsSection", content, 0f, 0f, 274f, 92.0267f);
        CreateHeaderTextBox(friendsSection, "FriendsHeader", "Friends", 0f, 0f, 274f, 24f);
        BuildFriendRow(friendsSection, "Friend1", 0f, 29f, "Wittless");
        CreateLine(friendsSection, "FriendDivider1", 0f, 58.0133f, 274f, CreamSupport);
        BuildFriendRow(friendsSection, "Friend2", 0f, 63.0133f, "DaveBrave");
        CreateLine(friendsSection, "FriendDivider2", 0f, 92.0267f, 274f, CreamSupport);

        RectTransform guildSection = CreateNode("GuildSection", content, 0f, 112.0267f, 274f, 530f);
        CreateHeaderTextBox(guildSection, "GuildHeader", "Dog club", 0f, 0f, 274f, 24f);

        TextMeshProUGUI clubText = CreateText(guildSection, "ClubInfo", "“Paw Patrol”\nExperience: 3,450\n(Global rank #137)", 15, BodyBrown, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        clubText.rectTransform.anchorMin = new Vector2(0f, 1f);
        clubText.rectTransform.anchorMax = new Vector2(0f, 1f);
        clubText.rectTransform.pivot = new Vector2(0f, 1f);
        clubText.rectTransform.sizeDelta = new Vector2(274f, 60f);
        clubText.rectTransform.anchoredPosition = new Vector2(0f, -37f);
        clubText.textWrappingMode = TextWrappingModes.Normal;
        clubText.overflowMode = TextOverflowModes.Overflow;
        clubText.richText = true;
        clubText.text = "<size=15><b><color=#8A4B3C>“Paw Patrol”</color></b></size>\n<size=13><b><color=#8A4B3C>Experience: 3,450</color></b></size>\n<size=13><color=#8A4B3C>(Global rank #137)</color></size>";

        BuildSocialActivitiesGroup(guildSection, "Weekly guild activities", 0f, 110f, GuildActivities[0], GuildActivities[1]);
        BuildIndividualActivityGroup(guildSection, 0f, 243f);
        BuildMembersList(guildSection, 0f, 317f);
        BuildLeaderboardList(guildSection, 0f, 429f);
    }

    private void BuildKennelAdoptContent(RectTransform content)
    {
        float y = 5f;
        for (int i = 0; i < KennelAdoptRows.Length; i++)
        {
            KennelRowData row = KennelAdoptRows[i];
            BuildKennelAdoptRow(content, "AdoptRow_" + i, y, row);
            y += 90f;
            if (i < KennelAdoptRows.Length - 1)
            {
                CreateLine(content, "Divider_" + i, 10f, y + 1f, 359f, CreamSupport);
                y += 10f;
            }
        }
    }

    private void BuildKennelReleaseContent(RectTransform parent)
    {
        BuildKennelReleaseRow(parent, "ReleaseRow", 10f, 5f);
        CreateLine(parent, "ReleaseDivider", 10f, 114f, 359f, CreamSupport);
    }

    private RectTransform CreateFullScreenRoot(string name)
    {
        RectTransform rect = UiFactory.CreateRect(name, exactFrame);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(UiTheme.ReferenceWidth, GetFrameHeight());
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private void ApplyResponsivePositions()
    {
        float frameHeight = GetFrameHeight();
        float visibleHeight = GetVisibleFrameHeight();
        ApplyRootSize(baseRoot, frameHeight);
        ApplyRootSize(walkRoot, frameHeight);
        ApplyRootSize(competitionRoot, frameHeight);
        ApplyRootSize(kennelAdoptRoot, frameHeight);
        ApplyRootSize(kennelReleaseRoot, frameHeight);
        ApplyRootSize(overlayLayer, frameHeight);

        SetNodeY(baseRoot, "WalkButton", visibleHeight - 73f - 14f);
        SetNodeY(baseRoot, "SocialButton", visibleHeight - 55f - 23f);

        SetNodeY(walkRoot, "WalkButton", visibleHeight - 73f - 14f);
        if (baseWalkButton != null)
        {
            baseWalkButton.ApplyLayout(1f);
        }

        if (plannerWalkButton != null)
        {
            plannerWalkButton.ApplyLayout(1f);
        }

        SetBackgroundHeight(baseRoot, "MapMainBackground", frameHeight);
        SetBackgroundHeight(walkRoot, "WalkBackground", frameHeight);
        SetBackgroundHeight(competitionRoot, "CompetitionBackground", frameHeight);

        SetNodeY(overlayLayer, "SocialSheet", visibleHeight - 618f - 15f);
    }

    private float GetFrameHeight()
    {
        return frameLayout.LogicalHeight > 0f ? frameLayout.LogicalHeight : UiTheme.ReferenceContentHeight;
    }

    private float GetVisibleFrameHeight()
    {
        return frameLayout.VisibleLogicalHeight > 0f ? frameLayout.VisibleLogicalHeight : UiTheme.ReferenceContentHeight;
    }

    private static void ApplyRootSize(RectTransform root, float height)
    {
        if (root != null)
        {
            root.sizeDelta = new Vector2(UiTheme.ReferenceWidth, height);
        }
    }

    private static void SetNodeY(RectTransform parent, string name, float y)
    {
        if (parent == null)
        {
            return;
        }

        Transform child = parent.Find(name);
        RectTransform rect = child as RectTransform;
        if (rect != null)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -Mathf.Max(0f, y));
        }
    }

    private static void SetBackgroundHeight(RectTransform parent, string name, float height)
    {
        if (parent == null)
        {
            return;
        }

        Transform child = parent.Find(name);
        RectTransform rect = child as RectTransform;
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }
    }

    private void SetMode(MapMode mode)
    {
        currentMode = mode;
        EnsureLockedWalkDogSelected(PawPalGameRuntime.Instance);
        if (mode != MapMode.Walk)
        {
            HandleWalkPlanPreviewChanged(0f);
        }

        if (baseRoot != null)
        {
            bool showBase = mode == MapMode.Base || mode == MapMode.Social || mode == MapMode.SocialChat || mode == MapMode.SocialAddFriend || mode == MapMode.SocialEnterClub;
            baseRoot.gameObject.SetActive(showBase);
        }

        if (walkRoot != null)
        {
            walkRoot.gameObject.SetActive(mode == MapMode.Walk);
            if (mode == MapMode.Walk && walkPlanner != null)
            {
                walkPlanner.EnterPlanner();
            }
        }

        if (competitionRoot != null)
        {
            competitionRoot.gameObject.SetActive(mode == MapMode.CompetitionCenter);
        }

        if (kennelAdoptRoot != null)
        {
            kennelAdoptRoot.gameObject.SetActive(mode == MapMode.KennelAdopt);
        }

        if (kennelReleaseRoot != null)
        {
            kennelReleaseRoot.gameObject.SetActive(mode == MapMode.KennelRelease);
        }

        if (socialSheetRoot != null)
        {
            bool showSocialSheet = mode == MapMode.Social || mode == MapMode.SocialChat || mode == MapMode.SocialAddFriend || mode == MapMode.SocialEnterClub;
            socialSheetRoot.gameObject.SetActive(showSocialSheet);
        }

        if (socialChatRoot != null)
        {
            socialChatRoot.gameObject.SetActive(mode == MapMode.SocialChat);
        }

        if (socialAddFriendRoot != null)
        {
            socialAddFriendRoot.gameObject.SetActive(mode == MapMode.SocialAddFriend);
        }

        if (socialEnterClubRoot != null)
        {
            socialEnterClubRoot.gameObject.SetActive(mode == MapMode.SocialEnterClub);
        }
    }

    private void ToggleSocialChat()
    {
        SetMode(currentMode == MapMode.SocialChat ? MapMode.Social : MapMode.SocialChat);
    }

    private void ToggleSocialAddFriend()
    {
        SetMode(currentMode == MapMode.SocialAddFriend ? MapMode.Social : MapMode.SocialAddFriend);
    }

    private void ToggleSocialEnterClub()
    {
        SetMode(currentMode == MapMode.SocialEnterClub ? MapMode.Social : MapMode.SocialEnterClub);
    }

    private void ShowAgilityTrialOverlay()
    {
        if (overlayLayer == null)
        {
            return;
        }

        agilityTrialOverlay = PawPalAgilityTrialSelectionOverlay.Show(overlayLayer);
    }

    private void CreateLocationHotspot(RectTransform parent, string name, float x, float y, float width, float height, string labelText, string iconName, float iconSize, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform root = CreateNode(name, parent, x, y, width, height);
        if (onClick != null)
        {
            UiFactory.AddButton(root.gameObject, onClick);
        }

        RectTransform group = UiFactory.CreateRect("CircleGroup", root);
        group.anchorMin = new Vector2(0.5f, 1f);
        group.anchorMax = new Vector2(0.5f, 1f);
        group.pivot = new Vector2(0.5f, 1f);
        group.sizeDelta = new Vector2(44f, 44f);
        group.anchoredPosition = new Vector2(width * 0.5f, 0f);

        CreateMapCircleGroup(group);

        Image icon = UiFactory.CreateImage("Icon", group, sprites.GetIcon(iconName), Coral);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        icon.rectTransform.anchoredPosition = Vector2.zero;

        float labelWidth = width;
        RectTransform labelBox = CreateTextbox(root, "LabelBox", 0f, 40f, labelWidth, 24f, 15, true);
        TextMeshProUGUI label = CreateText(labelBox, "Label", labelText, 15, BodyBrown, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 10f, 0f, 10f, 0f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    private void CreateMapCircleGroup(RectTransform group)
    {
        Image outer = UiFactory.CreateImage("Outer", group, UiTheme.CircleOutlineSprite, Coral);
        outer.type = Image.Type.Simple;
        outer.preserveAspect = false;
        outer.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        outer.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        outer.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        outer.rectTransform.sizeDelta = new Vector2(44f, 44f);
        outer.rectTransform.anchoredPosition = Vector2.zero;

        Image inner = UiFactory.CreateImage("Inner", group, UiTheme.CircleSprite, White);
        inner.type = Image.Type.Simple;
        inner.preserveAspect = false;
        inner.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        inner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        inner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        inner.rectTransform.sizeDelta = new Vector2(42f, 42f);
        inner.rectTransform.anchoredPosition = Vector2.zero;
    }

    private WalkStaminaButtonView CreateWalkButton(RectTransform parent, float x, float y, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform root = CreateNode("WalkButton", parent, x, y, 73f, 73f);
        WalkStaminaButtonView button = root.gameObject.AddComponent<WalkStaminaButtonView>();
        button.Initialize(sprites, onClick);
        return button;
    }

    private void CreateSocialButton(RectTransform parent, float x, float y, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform buttonRect = CreateNode("SocialButton", parent, x, y, 55f, 55f);

        Image fill = buttonRect.gameObject.GetComponent<Image>();
        if (fill == null)
        {
            fill = buttonRect.gameObject.AddComponent<Image>();
        }

        fill.sprite = UiTheme.CircleSprite;
        fill.type = Image.Type.Simple;
        fill.preserveAspect = false;
        fill.color = Cream;

        UiFactory.AddButton(buttonRect.gameObject, onClick);

        Image outline = UiFactory.CreateImage("Outline", buttonRect, UiTheme.CircleOutlineSprite, Coral);
        outline.type = Image.Type.Simple;
        outline.preserveAspect = false;
        UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);

        Shadow shadow = buttonRect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        Image icon = UiFactory.CreateImage("Icon", buttonRect, sprites.GetIcon("icon_friends_brand"), Coral);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(35f, 35f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void CreatePawMarker(RectTransform parent, float x, float y)
    {
        RectTransform root = CreateNode("PawMarker", parent, x, y, 44f, 44f);

        Image outer = UiFactory.CreateImage("Outer", root, UiTheme.CircleSprite, White);
        outer.type = Image.Type.Simple;
        outer.preserveAspect = false;
        outer.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        outer.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        outer.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        outer.rectTransform.sizeDelta = new Vector2(44f, 44f);
        outer.rectTransform.anchoredPosition = Vector2.zero;

        Image ring = UiFactory.CreateImage("Ring", root, UiTheme.CircleOutlineSprite, Coral);
        ring.type = Image.Type.Simple;
        ring.preserveAspect = false;
        ring.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ring.rectTransform.sizeDelta = new Vector2(42f, 42f);
        ring.rectTransform.anchoredPosition = Vector2.zero;

        Image icon = UiFactory.CreateImage("PawIcon", root, sprites.GetIcon("icon_pawprint_other"), Coral);
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 17.93f);
    }

    private void CreateEncounter(RectTransform parent, float x, float y)
    {
        RectTransform root = CreateNode("Encounter_" + x + "_" + y, parent, x, y, 33f, 33f);
        Image circle = root.gameObject.AddComponent<Image>();
        circle.sprite = UiTheme.CircleSprite;
        circle.type = Image.Type.Simple;
        circle.preserveAspect = false;
        circle.color = EncounterCoral;

        Shadow shadow = root.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        TextMeshProUGUI label = CreateText(root, "QuestionMark", "?", 22, White, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void CreateNameSelector(RectTransform parent, float x, float y, float width)
    {
        RectTransform row = CreateNode("NameSelector", parent, x, y, width, 24f);

        CreateArrow(row, "BackArrow", "UI/Figma/HomeMain/button_back", 44f, 0f, true, SelectPreviousDog);
        CreateTextbox(row, "NameField", 64f, 0f, 182f, 24f, 15, false);
        competitionDogNameLabel = CreateText(row, "NameLabel", "Pepper", 15, BodyBrown, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        competitionDogNameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        competitionDogNameLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        competitionDogNameLabel.rectTransform.pivot = new Vector2(0f, 1f);
        competitionDogNameLabel.rectTransform.sizeDelta = new Vector2(182f, 24f);
        competitionDogNameLabel.rectTransform.anchoredPosition = new Vector2(64f, 0f);

        CreateArrow(row, "ForwardArrow", "UI/Figma/HomeMain/button_forward", 266f, 0f, false, SelectNextDog);
    }

    private void CreateArrow(RectTransform parent, string name, string resourcePath, float x, float y, bool previousDog, Action onClick)
    {
        Image hitArea = UiFactory.CreateImage(name + "HitArea", parent, UiTheme.WhiteSprite, new Color(1f, 1f, 1f, 0.002f));
        hitArea.type = Image.Type.Simple;
        hitArea.preserveAspect = false;
        hitArea.rectTransform.anchorMin = new Vector2(0f, 1f);
        hitArea.rectTransform.anchorMax = new Vector2(0f, 1f);
        hitArea.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hitArea.rectTransform.sizeDelta = new Vector2(28f, 28f);
        hitArea.rectTransform.anchoredPosition = new Vector2(x + 6f, -y - 12f);

        Image arrow = UiFactory.CreateImage(name, hitArea.rectTransform, sprites.GetResourceSprite(resourcePath), White);
        arrow.type = Image.Type.Simple;
        arrow.preserveAspect = true;
        arrow.raycastTarget = false;
        arrow.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.sizeDelta = SelectorArrowSize;
        arrow.rectTransform.anchoredPosition = Vector2.zero;
        UiFactory.AddButton(hitArea.gameObject, delegate
        {
            if (onClick != null)
            {
                onClick();
            }
        });
    }

    private void HandleRuntimeStateChanged()
    {
        RefreshRuntimeState();
    }

    private void RefreshRuntimeState()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime == null)
        {
            RefreshWalkButtons(null);
            return;
        }

        EnsureLockedWalkDogSelected(runtime);

        if (competitionDogNameLabel != null)
        {
            PawPalDogState activeDog = runtime.ActiveDog;
            competitionDogNameLabel.text = activeDog == null || string.IsNullOrEmpty(activeDog.DisplayName) ? "Dog" : activeDog.DisplayName;
        }

        if (walkPlanner != null)
        {
            walkPlanner.RefreshRuntimeState();
        }

        RefreshWalkButtons(runtime);
    }

    private void CaptureLockedWalkDog(PawPalGameRuntime runtime)
    {
        PawPalDogState activeDog = runtime != null ? runtime.ActiveDog : null;
        if (activeDog != null)
        {
            lockedWalkDogId = activeDog.Id;
        }
        else
        {
            lockedWalkDogId = string.Empty;
        }

        if (walkPlanner != null)
        {
            walkPlanner.SetLockedDogId(lockedWalkDogId);
        }
    }

    private void EnsureLockedWalkDogSelected(PawPalGameRuntime runtime)
    {
        if (runtime == null || string.IsNullOrEmpty(lockedWalkDogId))
        {
            return;
        }

        PawPalDogState activeDog = runtime.ActiveDog;
        if (activeDog != null && string.Equals(activeDog.Id, lockedWalkDogId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        runtime.SelectDogById(lockedWalkDogId, false);
    }

    private PawPalDogState ResolveLockedWalkDog(PawPalGameRuntime runtime)
    {
        if (runtime == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(lockedWalkDogId))
        {
            IReadOnlyList<PawPalDogState> dogs = runtime.Dogs;
            for (int i = 0; i < dogs.Count; i++)
            {
                PawPalDogState dog = dogs[i];
                if (dog != null && string.Equals(dog.Id, lockedWalkDogId, StringComparison.OrdinalIgnoreCase))
                {
                    return dog;
                }
            }
        }

        return runtime.ActiveDog;
    }

    private void HandleWalkPlanPreviewChanged(float previewCost)
    {
        walkPlannerPreviewCost = Mathf.Max(0f, previewCost);
        RefreshWalkButtons(PawPalGameRuntime.Instance);
    }

    private void RefreshWalkButtons(PawPalGameRuntime runtime)
    {
        PawPalDogState lockedDog = ResolveLockedWalkDog(runtime);
        PawPalWalkStaminaSnapshot stamina = runtime != null
            ? runtime.GetWalkStaminaSnapshot(lockedDog)
            : new PawPalWalkStaminaSnapshot();
        float previewCost = currentMode == MapMode.Walk ? walkPlannerPreviewCost : 0f;

        if (baseWalkButton != null)
        {
            baseWalkButton.SetStamina(stamina.Current, stamina.Max, previewCost);
        }

        if (plannerWalkButton != null)
        {
            plannerWalkButton.SetStamina(stamina.Current, stamina.Max, previewCost);
        }
    }

    private void SelectPreviousDog()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.SelectPreviousDog();
            DogCycleCamera.TryFocusRuntimeActiveDogFromSelection();
        }
    }

    private void SelectNextDog()
    {
        PawPalGameRuntime runtime = PawPalGameRuntime.Instance;
        if (runtime != null)
        {
            runtime.SelectNextDog();
            DogCycleCamera.TryFocusRuntimeActiveDogFromSelection();
        }
    }

    private void CreateSectionLineHeader(RectTransform parent, string labelText, float x, float y, float width, float labelWidth)
    {
        RectTransform group = CreateNode("SectionHeader_" + labelText.Replace(" ", string.Empty), parent, x, y, width, 21f);
        CreateLine(group, "Line", 0f, 13f, width, CreamSupport);

        Image mask = CreateImageNode(group, "LabelMask", UiTheme.WhiteSprite, Cream, (width - labelWidth) * 0.5f, 0f, labelWidth, 19f);
        mask.type = Image.Type.Simple;
        mask.preserveAspect = false;

        TextMeshProUGUI label = CreateText(group, "Label", labelText, 14, Coral, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(labelWidth, 19f);
        label.rectTransform.anchoredPosition = new Vector2((width - labelWidth) * 0.5f, 0f);
    }

    private void CreateCompetitionRow(RectTransform parent, string name, float x, float y, string title, string detail, bool enabled)
    {
        RectTransform row = CreateNode(name, parent, x, y, 350f, 40f);
        Image fill = row.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedFiveSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;

        CreateOutline(row, enabled ? Coral : Gray, 1f);
        Shadow shadow = row.gameObject.AddComponent<Shadow>();
        shadow.effectColor = (enabled ? CreamSupport : GrayLight);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        Color32 titleColor = enabled ? Coral : Gray;
        Color32 detailColor = enabled ? BodyBrown : Gray;

        TextMeshProUGUI titleLabel = CreateText(row, "Title", title, 15, titleColor, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleLabel.rectTransform.pivot = new Vector2(0f, 1f);
        titleLabel.rectTransform.sizeDelta = new Vector2(130f, 18f);
        titleLabel.rectTransform.anchoredPosition = new Vector2(10f, -9f);

        if (!string.IsNullOrEmpty(detail))
        {
            TextMeshProUGUI detailLabel = CreateText(row, "Detail", detail, 12, detailColor, UiTheme.NavRegularFont, TextAlignmentOptions.Right);
            detailLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            detailLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            detailLabel.rectTransform.pivot = new Vector2(0f, 1f);
            detailLabel.rectTransform.sizeDelta = new Vector2(114f, 16f);
            detailLabel.rectTransform.anchoredPosition = new Vector2(145f, -10f);
        }

        CreateActionButton(row, "EnterButton", "Enter", 284f, 8f, 61f, enabled, delegate
        {
            if (string.Equals(title, "Obedience Trial", StringComparison.OrdinalIgnoreCase))
            {
                if (!PawPalObedienceTrialSceneFlow.LoadTrialScene())
                {
                    Debug.LogWarning("Competition center could not load the Obedience Trial scene.");
                }

                return;
            }

            if (string.Equals(title, "Agility Trial", StringComparison.OrdinalIgnoreCase))
            {
                ShowAgilityTrialOverlay();
                return;
            }

            Debug.Log("Competition center enter flow is unresolved for '" + title + "'.");
        });
    }

    private void BuildSocialActivitiesGroup(RectTransform parent, string title, float x, float y, ActivityProgressData first, ActivityProgressData second)
    {
        RectTransform group = CreateNode("GuildActivities", parent, x, y, 274f, 120f);
        TextMeshProUGUI label = CreateText(group, "Label", title, 15, Coral, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(274f, 18f);
        label.rectTransform.anchoredPosition = Vector2.zero;

        BuildSocialActivityCard(group, "Activity1", 0f, 23f, first);
        BuildSocialActivityCard(group, "Activity2", 0f, 76f, second);
    }

    private void BuildSocialActivityCard(RectTransform parent, string name, float x, float y, ActivityProgressData data)
    {
        RectTransform card = CreateNode(name, parent, x, y, 274f, 44f);
        Image fill = card.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedFiveSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;
        CreateOutline(card, Coral, 1f);

        TextMeshProUGUI title = CreateText(card, "Title", data.Title, 13, BodyBrown, UiTheme.NavMediumFont, TextAlignmentOptions.Left);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(150f, 14f);
        title.rectTransform.anchoredPosition = new Vector2(5f, -5f);

        RectTransform barGroup = CreateNode("BarGroup", card, 5f, 23f, 176f, 14f);
        BuildProgressBar(barGroup, 176f, 14f, data.FillWidth, data.ProgressText);

        RectTransform xpBox = CreateNode("XpBox", card, 216f, 8f, 53f, 28f);
        Image xpFill = xpBox.gameObject.AddComponent<Image>();
        xpFill.sprite = UiTheme.RoundedFiveSprite;
        xpFill.type = Image.Type.Sliced;
        xpFill.preserveAspect = false;
        xpFill.color = GrayLight;
        CreateOutline(xpBox, GrayLight, 1f);

        TextMeshProUGUI xpLabel = CreateText(xpBox, "XpLabel", data.RewardText, 13, BodyBrown, UiTheme.NavMediumFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(xpLabel.rectTransform, 4f, 0f, 4f, 0f);
    }

    private void BuildIndividualActivityGroup(RectTransform parent, float x, float y)
    {
        RectTransform group = CreateNode("IndividualActivities", parent, x, y, 274f, 61f);
        TextMeshProUGUI label = CreateText(group, "Label", "Weekly individual activities", 15, Coral, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(274f, 18f);
        label.rectTransform.anchoredPosition = Vector2.zero;

        RectTransform card = CreateNode("ActivityCard", group, 0f, 23f, 274f, 38f);
        Image fill = card.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedFiveSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;
        CreateOutline(card, Coral, 1f);

        RectTransform barGroup = CreateNode("BarGroup", card, 10f, 12f, 254f, 14f);
        BuildProgressBar(barGroup, 254f, 14f, 178.53f, "60/100 XP");
    }

    private void BuildMembersList(RectTransform parent, float x, float y)
    {
        RectTransform group = CreateNode("MembersSection", parent, x, y, 274f, 99f);
        TextMeshProUGUI label = CreateText(group, "Label", "Members", 15, Coral, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(274f, 18f);
        label.rectTransform.anchoredPosition = Vector2.zero;

        RectTransform list = CreateNode("List", group, 0f, 24f, 274f, 72f);
        for (int i = 0; i < ClubMembers.Length; i++)
        {
            BuildMemberRow(list, "Member_" + i, i * 24f, ClubMembers[i]);
            if (i < ClubMembers.Length - 1)
            {
                CreateLine(list, "Divider_" + i, 0f, 21f + (i * 24f), 274f, CreamSupport);
            }
        }
    }

    private void BuildMemberRow(RectTransform parent, string name, float y, SocialMemberData data)
    {
        RectTransform row = CreateNode(name, parent, 0f, y, 274f, 21f);
        TMP_FontAsset font = data.IsHighlighted ? UiTheme.NavExtraBoldFont : UiTheme.NavRegularFont;
        TextMeshProUGUI nameLabel = CreateText(row, "Name", data.Name, 13, BodyBrown, font, TextAlignmentOptions.Left);
        nameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        nameLabel.rectTransform.pivot = new Vector2(0f, 1f);
        nameLabel.rectTransform.sizeDelta = new Vector2(200f, 18f);
        nameLabel.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI levelLabel = CreateText(row, "Level", data.LevelText, 13, BodyBrown, font, TextAlignmentOptions.Right);
        levelLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        levelLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        levelLabel.rectTransform.pivot = new Vector2(0f, 1f);
        levelLabel.rectTransform.sizeDelta = new Vector2(55f, 18f);
        levelLabel.rectTransform.anchoredPosition = new Vector2(219f, 0f);
    }

    private void BuildLeaderboardList(RectTransform parent, float x, float y)
    {
        RectTransform group = CreateNode("LeaderboardSection", parent, x, y, 274f, 101f);
        TextMeshProUGUI label = CreateText(group, "Label", "Leaderboard", 15, Coral, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(274f, 18f);
        label.rectTransform.anchoredPosition = Vector2.zero;

        RectTransform viewport;
        RectTransform content;
        CreateScrollArea(group, "LeaderboardScroll", 0f, 24f, 274f, 74f, 161f, out content, out viewport);

        for (int i = 0; i < ClubLeaderboard.Length; i++)
        {
            BuildLeaderboardRow(content, "LeaderboardRow_" + i, i * 24f, ClubLeaderboard[i]);
            if (i < ClubLeaderboard.Length - 1)
            {
                CreateLine(content, "Divider_" + i, 0f, 21f + (i * 24f), 274f, CreamSupport);
            }
        }
    }

    private void BuildLeaderboardRow(RectTransform parent, string name, float y, SocialLeaderboardData data)
    {
        RectTransform row = CreateNode(name, parent, 0f, y, 274f, 21f);
        TMP_FontAsset font = data.IsHighlighted ? UiTheme.NavExtraBoldFont : UiTheme.NavRegularFont;

        TextMeshProUGUI nameLabel = CreateText(row, "Name", data.Name, 13, BodyBrown, font, TextAlignmentOptions.Left);
        nameLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        nameLabel.rectTransform.pivot = new Vector2(0f, 1f);
        nameLabel.rectTransform.sizeDelta = new Vector2(157f, 18f);
        nameLabel.rectTransform.anchoredPosition = Vector2.zero;

        CreateFlag(row, data.Flag, 173f, 0f);

        TextMeshProUGUI scoreLabel = CreateText(row, "Score", data.Score, 13, BodyBrown, font, TextAlignmentOptions.Right);
        scoreLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        scoreLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        scoreLabel.rectTransform.pivot = new Vector2(0f, 1f);
        scoreLabel.rectTransform.sizeDelta = new Vector2(39f, 18f);
        scoreLabel.rectTransform.anchoredPosition = new Vector2(199f, 0f);

        TextMeshProUGUI rankLabel = CreateText(row, "Rank", data.Rank, 13, BodyBrown, font, TextAlignmentOptions.Right);
        rankLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        rankLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
        rankLabel.rectTransform.pivot = new Vector2(0f, 1f);
        rankLabel.rectTransform.sizeDelta = new Vector2(35f, 18f);
        rankLabel.rectTransform.anchoredPosition = new Vector2(239f, 0f);
    }

    private void BuildFriendRow(RectTransform parent, string name, float x, float y, string friendName)
    {
        RectTransform row = CreateNode(name, parent, x, y, 274f, 24f);
        TextMeshProUGUI label = CreateText(row, "Label", friendName, 13, BodyBrown, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(150f, 18f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        CreateActionButton(row, "VisitButton", "Visit", 226f, 0f, 48f, true, delegate
        {
            Debug.Log("Map social visit flow is unresolved in the provided Figma states.");
        });
    }

    private void BuildKennelTopPanel(RectTransform panel, bool adoptSelected)
    {
        Image fill = panel.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.TopRoundedPanelSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;

        CreateEdgeBorder(panel, "Top", Coral, 0f, 0f, 378f, 1f);
        CreateEdgeBorder(panel, "Left", Coral, 0f, 0f, 1f, 84.0175f);
        CreateEdgeBorder(panel, "Right", Coral, 377f, 0f, 1f, 84.0175f);

        CreateHeaderTextBox(panel, "Title", "Dog selection", 10f, 10f, 358f, 24f);

        RectTransform toggleRow = CreateNode("ToggleRow", panel, 106f, 44f, 166f, 26f);
        BuildKennelToggle(toggleRow, adoptSelected);

        CreateLine(panel, "BottomDivider", 10f, 79f, 359f, CreamSupport);
    }

    private void BuildKennelToggle(RectTransform parent, bool adoptSelected)
    {
        RectTransform adopt = CreateNode("AdoptTab", parent, 0f, 0f, 82f, 26f);
        Image adoptFill = adopt.gameObject.AddComponent<Image>();
        adoptFill.sprite = UiTheme.RoundedTenSprite;
        adoptFill.type = Image.Type.Sliced;
        adoptFill.preserveAspect = false;
        adoptFill.color = adoptSelected ? Coral : Gray;
        UiFactory.AddButton(adopt.gameObject, delegate
        {
            if (!adoptSelected)
            {
                SetMode(MapMode.KennelAdopt);
            }
        });

        TextMeshProUGUI adoptLabel = CreateText(adopt, "Label", "Adopt", 16, White, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(adoptLabel.rectTransform, 10f, 0f, 10f, 0f);

        Image divider = CreateImageNode(parent, "Divider", UiTheme.WhiteSprite, new Color32(217, 217, 217, 255), 82f, 0f, 2f, 26f);
        divider.type = Image.Type.Simple;
        divider.preserveAspect = false;

        RectTransform release = CreateNode("ReleaseTab", parent, 84f, 0f, 82f, 26f);
        Image releaseFill = release.gameObject.AddComponent<Image>();
        releaseFill.sprite = UiTheme.RoundedTenSprite;
        releaseFill.type = Image.Type.Sliced;
        releaseFill.preserveAspect = false;
        releaseFill.color = adoptSelected ? Gray : Coral;
        UiFactory.AddButton(release.gameObject, delegate
        {
            if (adoptSelected)
            {
                SetMode(MapMode.KennelRelease);
            }
        });

        TextMeshProUGUI releaseLabel = CreateText(release, "Label", "Release", 16, White, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(releaseLabel.rectTransform, 10f, 0f, 10f, 0f);
    }

    private void BuildKennelAdoptRow(RectTransform parent, string name, float y, KennelRowData data)
    {
        RectTransform row = CreateNode(name, parent, 10f, y, 358f, 90f);

        RectTransform content = CreateNode("Content", row, 0f, 0f, 273f, 80f);
        Image photo = CreateImageNode(content, "Photo", sprites.GetResourceSprite(data.ImageResource), White, 0f, 0f, 80f, 80f);
        photo.type = Image.Type.Sliced;
        photo.preserveAspect = false;
        CreateOutline(photo.rectTransform, Coral, 1f);

        RectTransform textFrame = CreateNode("TextFrame", content, 85f, 0f, 188f, 79f);
        TextMeshProUGUI title = CreateText(textFrame, "Title", data.Title, 15, Coral, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Left);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(188f, 18f);
        title.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI description = CreateText(textFrame, "Description", data.Description, 13, BodyBrown, UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        description.rectTransform.anchorMin = new Vector2(0f, 1f);
        description.rectTransform.anchorMax = new Vector2(0f, 1f);
        description.rectTransform.pivot = new Vector2(0f, 1f);
        description.rectTransform.sizeDelta = new Vector2(188f, 61f);
        description.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Overflow;

        string buttonLabel = data.ButtonText;
        float buttonWidth = data.IsPriceButton ? 75f : 55f;
        CreateActionButton(row, "ActionButton", buttonLabel, 278f, 33f, buttonWidth, true, delegate
        {
            if (data.IsPriceButton)
            {
                Debug.Log("Kennel adoption purchase flow is unresolved in the provided Figma states.");
                return;
            }

            shell.ShowScreen(AppScreenId.Shop);
        });
    }

    private void BuildKennelReleaseRow(RectTransform parent, string name, float x, float y)
    {
        RectTransform row = CreateNode(name, parent, x, y, 358f, 108f);

        RectTransform content = CreateNode("Content", row, 0f, 0f, 283f, 98f);
        Image photo = CreateImageNode(content, "Photo", sprites.GetResourceSprite("UI/Figma/Map/kennel_pepper"), White, 0f, 0f, 90f, 90f);
        photo.type = Image.Type.Simple;
        photo.preserveAspect = false;
        CreateOutline(photo.rectTransform, Coral, 1f);

        RectTransform textFrame = CreateNode("TextFrame", content, 95f, 0f, 188f, 98f);
        CreateKennelStatLine(textFrame, "Pepper", 0f, 0f, true);
        CreateKennelStatLine(textFrame, "Endurance: 4", 0f, 20f, false);
        CreateKennelStatLine(textFrame, "Mobility: 4", 0f, 40f, false);
        CreateKennelStatLine(textFrame, "Speed: 4", 0f, 60f, false);
        CreateKennelStatLine(textFrame, "Focus: 4", 0f, 80f, false);

        CreateActionButton(row, "ReleaseButton", "Release", 276f, 42f, 77f, true, delegate
        {
            Debug.Log("Kennel release confirmation flow is unresolved in the provided Figma states.");
        });
    }

    private void CreateKennelStatLine(RectTransform parent, string text, float x, float y, bool isTitle)
    {
        TextMeshProUGUI label = CreateText(parent, "Line_" + text.Replace(" ", string.Empty), text, 13, isTitle ? Coral : BodyBrown, isTitle ? UiTheme.NavExtraBoldFont : UiTheme.NavRegularFont, TextAlignmentOptions.Left);
        label.rectTransform.anchorMin = new Vector2(0f, 1f);
        label.rectTransform.anchorMax = new Vector2(0f, 1f);
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.rectTransform.sizeDelta = new Vector2(188f, 18f);
        label.rectTransform.anchoredPosition = new Vector2(x, -y);
        if (isTitle)
        {
            label.fontSize = 15f;
        }
    }

    private void BuildProgressBar(RectTransform parent, float width, float height, float fillWidth, string text)
    {
        Image track = UiFactory.CreateImage("Track", parent, UiTheme.ProgressPillSprite, White);
        track.type = Image.Type.Sliced;
        track.preserveAspect = false;
        UiFactory.Stretch(track.rectTransform, 0f, 0f, 0f, 0f);

        Shadow trackShadow = track.gameObject.AddComponent<Shadow>();
        trackShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
        trackShadow.effectDistance = new Vector2(0f, -1f);
        trackShadow.useGraphicAlpha = true;

        Image fill = CreateImageNode(parent, "Fill", UiTheme.TrainerLevelProgressSprite, White, 0f, 0f, fillWidth, height);
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;

        TextMeshProUGUI progressText = CreateText(parent, "ProgressText", text, 13, BodyBrown, UiTheme.NavMediumFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(progressText.rectTransform, 0f, 0f, 0f, 0f);
    }

    private void BuildChatBubble(RectTransform parent, Color32 fillColor, Color32 borderColor, string sender, string body, bool rightAligned)
    {
        Image fill = parent.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedFiveSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = fillColor;
        CreateOutline(parent, borderColor, 1f);

        TextMeshProUGUI text = CreateText(parent, "Text", "<b><color=#DF7861>" + sender + "</color></b>\n" + body, 13, BodyBrown, UiTheme.NavRegularFont, rightAligned ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopLeft);
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = new Vector2(0f, 1f);
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.sizeDelta = new Vector2(202f, 95f);
        text.rectTransform.anchoredPosition = new Vector2(3f, -3f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
    }

    private void CreateFlag(RectTransform parent, FlagType type, float x, float y)
    {
        RectTransform root = CreateNode("Flag_" + type, parent, x, y, 18f, 18f);

        RectTransform flag = CreateNode("FlagBody", root, 1.25f, 4.25f, 15.5f, 9.5f);
        Image background = flag.gameObject.AddComponent<Image>();
        background.sprite = UiTheme.RoundedFiveSprite;
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.color = White;
        CreateOutline(flag, FlagOutline, 1f);

        switch (type)
        {
            case FlagType.Japan:
                Image sun = CreateImageNode(flag, "Sun", UiTheme.CircleSprite, new Color32(204, 40, 57, 255), 5.1f, 1.75f, 5.3f, 5.3f);
                sun.type = Image.Type.Simple;
                sun.preserveAspect = false;
                break;
            case FlagType.Germany:
                CreateImageNode(flag, "BlackStripe", UiTheme.WhiteSprite, new Color32(0, 0, 0, 255), 0f, 0f, 15.5f, 3.1666f);
                CreateImageNode(flag, "RedStripe", UiTheme.WhiteSprite, new Color32(221, 0, 0, 255), 0f, 3.1666f, 15.5f, 3.1666f);
                CreateImageNode(flag, "GoldStripe", UiTheme.WhiteSprite, new Color32(255, 206, 0, 255), 0f, 6.3333f, 15.5f, 3.1666f);
                break;
            case FlagType.Usa:
                for (int i = 0; i < 6; i++)
                {
                    CreateImageNode(flag, "Stripe_" + i, UiTheme.WhiteSprite, i % 2 == 0 ? new Color32(191, 10, 48, 255) : White, 0f, i * 1.5833f, 15.5f, 1.5833f);
                }

                CreateImageNode(flag, "Canton", UiTheme.WhiteSprite, new Color32(0, 40, 104, 255), 0f, 0f, 6.5f, 4.75f);
                break;
        }
    }

    private RectTransform CreateTextbox(RectTransform parent, string name, float x, float y, float width, float height, int fontSize, bool useExtraBold)
    {
        RectTransform box = CreateNode(name, parent, x, y, width, height);
        Image fill = box.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedFiveSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Cream;
        CreateOutline(box, Coral, 1f);
        return box;
    }

    private void CreateHeaderTextBox(RectTransform parent, string name, string labelText, float x, float y, float width, float height)
    {
        RectTransform box = CreateNode(name, parent, x, y, width, height);
        Image fill = box.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedFiveSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = Coral;

        Shadow shadow = box.gameObject.AddComponent<Shadow>();
        shadow.effectColor = CoralDark;
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;

        TextMeshProUGUI label = CreateText(box, "Label", labelText, 16, White, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 5f, 0f, 5f, 0f);
    }

    private RectTransform CreateInputField(RectTransform parent, string name, float x, float y, float width, float height, string placeholderText)
    {
        RectTransform field = CreateNode(name, parent, x, y, width, height);
        Image fill = field.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = White;
        CreateOutline(field, Coral, 1f);

        Image inset = CreateImageNode(field, "InsetTop", UiTheme.WhiteSprite, CreamSupport, 0f, 0f, width, 3f);
        inset.type = Image.Type.Simple;
        inset.preserveAspect = false;

        RectTransform viewport = UiFactory.CreateRect("TextViewport", field);
        UiFactory.Stretch(viewport, 8f, 2f, 8f, 2f);
        Image viewportFill = viewport.gameObject.AddComponent<Image>();
        viewportFill.color = new Color(1f, 1f, 1f, 0.002f);
        viewportFill.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI text = CreateText(viewport, "Text", string.Empty, 13, BodyBrown, UiTheme.NavMediumFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;

        TextMeshProUGUI placeholder = CreateText(viewport, "Placeholder", placeholderText, 13, Gray, UiTheme.NavMediumFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);
        placeholder.textWrappingMode = TextWrappingModes.Normal;
        placeholder.overflowMode = TextOverflowModes.Overflow;

        TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.pointSize = 13f;
        input.text = string.Empty;
        return field;
    }

    private void CreateActionButton(RectTransform parent, string name, string labelText, float x, float y, float width, bool primary, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform buttonRect = CreateNode(name, parent, x, y, width, 24f);
        Color32 fillColor = primary ? CtaBlue : GrayLight;
        Color32 underlineColor = primary ? CtaBlueDark : Gray;
        Color32 textColor = primary ? White : Gray;

        Image fill = buttonRect.gameObject.AddComponent<Image>();
        fill.sprite = UiTheme.RoundedTenSprite;
        fill.type = Image.Type.Sliced;
        fill.preserveAspect = false;
        fill.color = fillColor;

        UiFactory.AddButton(buttonRect.gameObject, onClick);

        Image underline = CreateImageNode(buttonRect, "Underline", UiTheme.WhiteSprite, underlineColor, 0f, 22f, width, 2f);
        underline.type = Image.Type.Simple;
        underline.preserveAspect = false;

        TextMeshProUGUI label = CreateText(buttonRect, "Label", labelText, 13, textColor, UiTheme.NavExtraBoldFont, TextAlignmentOptions.Center);
        UiFactory.Stretch(label.rectTransform, 8f, 0f, 8f, 0f);
    }

    private void CreateSocialRoundButton(RectTransform parent, string name, float x, float y, string iconName, UnityEngine.Events.UnityAction onClick)
    {
        if (parent == null)
        {
            return;
        }

        try
        {
            RectTransform buttonRect = CreateNode(name, parent, x, y, 36f, 36f);
            if (buttonRect == null)
            {
                return;
            }

            GameObject buttonObject = buttonRect.gameObject;
            UiFactory.AddButton(buttonObject, onClick);

            Image fill = buttonObject.GetComponent<Image>();
            if (fill == null)
            {
                fill = buttonObject.AddComponent<Image>();
            }

            fill.sprite = UiTheme.WhiteSprite;
            fill.type = Image.Type.Simple;
            fill.preserveAspect = false;
            fill.color = Cream;
            fill.raycastTarget = true;

            Sprite outlineSprite = UiTheme.CircleOutlineSprite;
            if (outlineSprite != null)
            {
                Image outline = UiFactory.CreateImage("Outline", buttonRect, outlineSprite, Coral);
                outline.type = Image.Type.Simple;
                outline.preserveAspect = false;
                UiFactory.Stretch(outline.rectTransform, 0f, 0f, 0f, 0f);
            }

            Shadow shadow = buttonObject.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = buttonObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.useGraphicAlpha = true;

            if (sprites == null)
            {
                return;
            }

            Sprite iconSprite = sprites.GetIcon(iconName);
            if (iconSprite == null || iconSprite == UiTheme.WhiteSprite)
            {
                return;
            }

            Image icon = UiFactory.CreateImage("Icon", buttonRect, iconSprite, Coral);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(28f, 28f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("MapScreenView could not build social round button '" + name + "': " + exception.Message);
        }
    }

    private void CreateScrollArea(RectTransform parent, string name, float x, float y, float width, float height, float contentHeight, out RectTransform content)
    {
        RectTransform viewport;
        CreateScrollArea(parent, name, x, y, width, height, contentHeight, out content, out viewport);
    }

    private void CreateScrollArea(RectTransform parent, string name, float x, float y, float width, float height, float contentHeight, out RectTransform content, out RectTransform viewport)
    {
        RectTransform root = CreateNode(name, parent, x, y, width, height);
        Image rootImage = root.gameObject.AddComponent<Image>();
        rootImage.sprite = UiTheme.WhiteSprite;
        rootImage.type = Image.Type.Simple;
        rootImage.preserveAspect = false;
        rootImage.color = new Color(1f, 1f, 1f, 0.002f);

        viewport = UiFactory.CreateRect("Viewport", root);
        UiFactory.Stretch(viewport, 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.sprite = UiTheme.WhiteSprite;
        viewportImage.type = Image.Type.Simple;
        viewportImage.preserveAspect = false;
        viewportImage.color = new Color(1f, 1f, 1f, 0.002f);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        content = UiFactory.CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.sizeDelta = new Vector2(width, contentHeight);
        content.anchoredPosition = Vector2.zero;

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;
        scroll.verticalNormalizedPosition = 1f;
    }

    private void CreateOutline(RectTransform parent, Color color, float thickness)
    {
        CreateEdgeBorder(parent, "OutlineTop", color, 0f, 0f, parent.sizeDelta.x, thickness);
        CreateEdgeBorder(parent, "OutlineLeft", color, 0f, 0f, thickness, parent.sizeDelta.y);
        CreateEdgeBorder(parent, "OutlineRight", color, parent.sizeDelta.x - thickness, 0f, thickness, parent.sizeDelta.y);
        CreateEdgeBorder(parent, "OutlineBottom", color, 0f, parent.sizeDelta.y - thickness, parent.sizeDelta.x, thickness);
    }

    private void CreateEdgeBorder(RectTransform parent, string name, Color color, float x, float y, float width, float height)
    {
        Image line = CreateImageNode(parent, name, UiTheme.WhiteSprite, color, x, y, width, height);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
    }

    private void CreateLine(RectTransform parent, string name, float x, float y, float width, Color color)
    {
        Image line = CreateImageNode(parent, name, UiTheme.WhiteSprite, color, x, y, width, 1f);
        line.type = Image.Type.Simple;
        line.preserveAspect = false;
    }

    private Image CreateBackgroundImage(RectTransform parent, string name, string resourcePath, float x, float y, float width, float height)
    {
        return CreateImageNode(parent, name, sprites.GetResourceSprite(resourcePath), White, x, y, width, height);
    }

    private Image CreateImageNode(RectTransform parent, string name, Sprite sprite, Color color, float x, float y, float width, float height)
    {
        Image image = UiFactory.CreateImage(name, parent, sprite, color);
        image.rectTransform.anchorMin = new Vector2(0f, 1f);
        image.rectTransform.anchorMax = new Vector2(0f, 1f);
        image.rectTransform.pivot = new Vector2(0f, 1f);
        image.rectTransform.sizeDelta = new Vector2(width, height);
        image.rectTransform.anchoredPosition = new Vector2(x, -y);
        image.preserveAspect = false;
        return image;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = UiFactory.CreateLabel(name, parent, text, fontSize, color, FontStyles.Normal, alignment);
        label.font = font;
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private RectTransform CreateNode(string name, RectTransform parent, float x, float y, float width, float height)
    {
        RectTransform rect = UiFactory.CreateRect(name, parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
        return rect;
    }
}
