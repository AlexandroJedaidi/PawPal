using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

public static class TrainerProgressionSpreadsheetImporter
{
    private const string WorkbookPath = "gamelogic.xlsx";
    private const string WorksheetName = "Game logic";
    private const string OutputPath = "Assets/Resources/GameBalance/trainer_progression.json";

    [MenuItem("PawFriends/Game Logic/Import Trainer Progression")]
    public static void ImportTrainerProgression()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string workbookPath = Path.Combine(projectRoot, WorkbookPath);
        if (!File.Exists(workbookPath))
        {
            Debug.LogError("Trainer progression import failed. Could not find workbook at " + workbookPath + ".");
            return;
        }

        try
        {
            SpreadsheetSheet sheet = SpreadsheetSheet.Load(workbookPath, WorksheetName);
            TrainerProgressionConfig config = BuildConfig(sheet);

            string fullOutputPath = Path.Combine(projectRoot, OutputPath);
            string outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(fullOutputPath, json);
            AssetDatabase.Refresh();
            Debug.Log("Trainer progression imported to " + OutputPath + ".");
        }
        catch (Exception exception)
        {
            Debug.LogError("Trainer progression import failed: " + exception);
        }
    }

    private static TrainerProgressionConfig BuildConfig(SpreadsheetSheet sheet)
    {
        TrainerProgressionConfig config = new TrainerProgressionConfig();
        config.SourceWorkbook = WorkbookPath;
        config.SourceSheet = WorksheetName;

        AddActionReward(config, "food_and_water_dog", PawPalPlayerActionType.FeedDog, GetInt(sheet, "C52"), GetInt(sheet, "D52"), CalculateClubExperience(GetInt(sheet, "C52")));
        AddActionReward(config, "food_and_water_dog", PawPalPlayerActionType.GiveWater, GetInt(sheet, "C52"), GetInt(sheet, "D52"), CalculateClubExperience(GetInt(sheet, "C52")));
        AddActionReward(config, "walking_dog", PawPalPlayerActionType.WalkDog, GetInt(sheet, "C53"), GetInt(sheet, "D53"), CalculateClubExperience(GetInt(sheet, "C53")));
        AddActionReward(config, "cleaning_dog", PawPalPlayerActionType.CleanDog, GetInt(sheet, "C54"), GetInt(sheet, "D54"), CalculateClubExperience(GetInt(sheet, "C54")));
        AddActionReward(config, "training_tricks", PawPalPlayerActionType.TrainTrick, GetInt(sheet, "C55"), GetInt(sheet, "D55"), CalculateClubExperience(GetInt(sheet, "C55")));
        AddActionReward(config, "competition_first", PawPalPlayerActionType.CompetitionFirst, GetInt(sheet, "C56"), GetInt(sheet, "D56"), CalculateClubExperience(GetInt(sheet, "C56")));
        AddActionReward(config, "competition_second", PawPalPlayerActionType.CompetitionSecond, GetInt(sheet, "C57"), GetInt(sheet, "D57"), CalculateClubExperience(GetInt(sheet, "C57")));
        AddActionReward(config, "competition_third", PawPalPlayerActionType.CompetitionThird, GetInt(sheet, "C58"), GetInt(sheet, "D58"), CalculateClubExperience(GetInt(sheet, "C58")));
        AddActionReward(config, "daily_login", PawPalPlayerActionType.DailyLogin, GetInt(sheet, "C59"), GetInt(sheet, "D59"), CalculateClubExperience(GetInt(sheet, "C59")));

        int previousRequirement = GetInt(sheet, "D68");
        for (int row = 68; row <= 117; row++)
        {
            int level = GetInt(sheet, "B" + row.ToString(CultureInfo.InvariantCulture));
            if (level <= 0)
            {
                continue;
            }

            int requirement = previousRequirement;
            if (row > 68)
            {
                double factor = GetDouble(sheet, "E" + row.ToString(CultureInfo.InvariantCulture));
                requirement = (int)Math.Round(previousRequirement * (1d + factor), MidpointRounding.ToEven);
                previousRequirement = requirement;
            }

            TrainerMilestoneDefinition definition = BuildMilestoneDefinition(
                level,
                requirement,
                sheet.GetCell("F" + row.ToString(CultureInfo.InvariantCulture)),
                sheet.GetCell("G" + row.ToString(CultureInfo.InvariantCulture)));
            config.Milestones.Add(definition);
        }

        return config;
    }

    private static TrainerMilestoneDefinition BuildMilestoneDefinition(int level, int requirement, string rewardLabel, string rewardTypeLabel)
    {
        TrainerMilestoneDefinition definition = new TrainerMilestoneDefinition();
        definition.Level = level;
        definition.RequiredExperience = requirement;
        definition.RewardLabel = string.IsNullOrEmpty(rewardLabel) ? "-" : rewardLabel;
        definition.RewardTypeLabel = string.IsNullOrEmpty(rewardTypeLabel) ? "-" : rewardTypeLabel;
        definition.Quantity = 1;

        string rewardLabelLower = (rewardLabel ?? string.Empty).ToLowerInvariant();
        string rewardTypeLower = (rewardTypeLabel ?? string.Empty).ToLowerInvariant();

        if (level == 1 || rewardLabelLower == "-" || rewardTypeLower == "-")
        {
            definition.RewardKind = TrainerMilestoneRewardKind.None;
            return definition;
        }

        if (rewardTypeLower.Contains("currency"))
        {
            definition.CurrencyAmount = ExtractLeadingInteger(rewardLabel);
            definition.RewardKind = rewardTypeLower.Contains("premium")
                ? TrainerMilestoneRewardKind.CurrencyPremium
                : TrainerMilestoneRewardKind.CurrencyBasic;
            return definition;
        }

        if (rewardTypeLower == "food")
        {
            definition.RewardKind = TrainerMilestoneRewardKind.PremiumFood;
            definition.Quantity = Mathf.Max(1, ExtractLeadingInteger(rewardLabel));
            return definition;
        }

        if (rewardTypeLower == "dog")
        {
            definition.RewardKind = TrainerMilestoneRewardKind.DogUnlock;
            if (level == 6)
            {
                definition.DogId = "miso";
            }
            else if (level == 10)
            {
                definition.DogId = "suki";
            }

            return definition;
        }

        if (rewardTypeLower == "feature")
        {
            definition.RewardKind = TrainerMilestoneRewardKind.Feature;
            if (rewardLabelLower.Contains("dog club"))
            {
                definition.FeatureKey = "dog_club";
            }
            else if (rewardLabelLower.Contains("agility"))
            {
                definition.FeatureKey = "competition_agility";
            }
            else if (rewardLabelLower.Contains("disc"))
            {
                definition.FeatureKey = "competition_disc";
            }
            else if (rewardLabelLower.Contains("style"))
            {
                definition.FeatureKey = "competition_style";
            }

            return definition;
        }

        if (rewardTypeLower == "cosmetic")
        {
            definition.RandomSelection = rewardLabelLower.Contains("random");

            if (rewardLabelLower.Contains("collar"))
            {
                definition.RewardKind = TrainerMilestoneRewardKind.CatalogItemReward;
                definition.CatalogCategory = PawPalItemCategory.Collars;
                definition.ItemQuality = rewardLabelLower.Contains("premium")
                    ? TrainerMilestoneItemQuality.Premium
                    : TrainerMilestoneItemQuality.Basic;
                return definition;
            }

            if (rewardLabelLower.Contains("toy"))
            {
                definition.RewardKind = TrainerMilestoneRewardKind.CatalogItemReward;
                definition.CatalogCategory = PawPalItemCategory.Toys;
                definition.ItemQuality = rewardLabelLower.Contains("premium")
                    ? TrainerMilestoneItemQuality.Premium
                    : TrainerMilestoneItemQuality.Basic;
                return definition;
            }

            if (rewardLabelLower.Contains("clothing"))
            {
                definition.RewardKind = TrainerMilestoneRewardKind.CatalogItemReward;
                definition.CatalogCategory = PawPalItemCategory.Clothing;
                definition.ItemQuality = rewardLabelLower.Contains("premium")
                    ? TrainerMilestoneItemQuality.Premium
                    : TrainerMilestoneItemQuality.Basic;
                return definition;
            }

            if (rewardLabelLower.Contains("furniture"))
            {
                definition.RewardKind = TrainerMilestoneRewardKind.CatalogItemReward;
                definition.CatalogCategory = PawPalItemCategory.Furniture;
                definition.ItemQuality = rewardLabelLower.Contains("premium")
                    ? TrainerMilestoneItemQuality.Premium
                    : TrainerMilestoneItemQuality.Basic;
                return definition;
            }

            if (rewardLabelLower.Contains("breed"))
            {
                definition.RewardKind = TrainerMilestoneRewardKind.Unsupported;
                definition.CatalogCategory = PawPalItemCategory.Dogs;
                definition.ItemQuality = rewardLabelLower.Contains("premium")
                    ? TrainerMilestoneItemQuality.Premium
                    : TrainerMilestoneItemQuality.Basic;
                return definition;
            }
        }

        definition.RewardKind = TrainerMilestoneRewardKind.Unsupported;
        return definition;
    }

    private static void AddActionReward(TrainerProgressionConfig config, string id, PawPalPlayerActionType actionType, int experience, int basicCurrency, int clubExperience)
    {
        TrainerActionRewardDefinition definition = new TrainerActionRewardDefinition();
        definition.Id = id;
        definition.ActionType = actionType.ToString();
        definition.Experience = experience;
        definition.BasicCurrency = basicCurrency;
        definition.ClubExperience = clubExperience;
        config.ActionRewards.Add(definition);
    }

    private static int GetInt(SpreadsheetSheet sheet, string cellReference)
    {
        string value = sheet.GetCell(cellReference);
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        double parsed;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            return (int)Math.Round(parsed, MidpointRounding.AwayFromZero);
        }

        return 0;
    }

    private static double GetDouble(SpreadsheetSheet sheet, string cellReference)
    {
        string value = sheet.GetCell(cellReference);
        if (string.IsNullOrEmpty(value))
        {
            return 0d;
        }

        double parsed;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        return 0d;
    }

    private static int CalculateClubExperience(int experience)
    {
        return (int)Math.Round(experience * 0.2d, MidpointRounding.AwayFromZero);
    }

    private static int ExtractLeadingInteger(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return 0;
        }

        string digits = string.Empty;
        for (int i = 0; i < input.Length; i++)
        {
            char character = input[i];
            if (char.IsDigit(character))
            {
                digits += character;
                continue;
            }

            if (character == ',' && digits.Length > 0)
            {
                continue;
            }

            if (digits.Length > 0)
            {
                break;
            }
        }

        int parsed;
        if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        return 0;
    }

    private sealed class SpreadsheetSheet
    {
        private readonly Dictionary<string, string> cells;

        private SpreadsheetSheet(Dictionary<string, string> cells)
        {
            this.cells = cells;
        }

        public string GetCell(string cellReference)
        {
            string value;
            if (cells.TryGetValue(cellReference, out value))
            {
                return value;
            }

            return string.Empty;
        }

        public static SpreadsheetSheet Load(string workbookPath, string worksheetName)
        {
            using (FileStream stream = File.OpenRead(workbookPath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                List<string> sharedStrings = LoadSharedStrings(archive);
                string worksheetPath = ResolveWorksheetPath(archive, worksheetName);
                Dictionary<string, string> loadedCells = LoadWorksheetCells(archive, worksheetPath, sharedStrings);
                return new SpreadsheetSheet(loadedCells);
            }
        }

        private static List<string> LoadSharedStrings(ZipArchive archive)
        {
            List<string> values = new List<string>();
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return values;
            }

            using (Stream stream = entry.Open())
            {
                XDocument document = XDocument.Load(stream);
                XNamespace ns = document.Root.Name.Namespace;
                foreach (XElement stringItem in document.Descendants(ns + "si"))
                {
                    string combined = string.Empty;
                    foreach (XElement textNode in stringItem.Descendants(ns + "t"))
                    {
                        combined += textNode.Value;
                    }

                    values.Add(combined);
                }
            }

            return values;
        }

        private static string ResolveWorksheetPath(ZipArchive archive, string worksheetName)
        {
            XDocument workbookDocument;
            using (Stream workbookStream = archive.GetEntry("xl/workbook.xml").Open())
            {
                workbookDocument = XDocument.Load(workbookStream);
            }

            XDocument relationshipsDocument;
            using (Stream relationshipStream = archive.GetEntry("xl/_rels/workbook.xml.rels").Open())
            {
                relationshipsDocument = XDocument.Load(relationshipStream);
            }

            XNamespace workbookNamespace = workbookDocument.Root.Name.Namespace;
            XNamespace relationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelationshipNamespace = relationshipsDocument.Root.Name.Namespace;

            string relationshipId = string.Empty;
            foreach (XElement sheet in workbookDocument.Descendants(workbookNamespace + "sheet"))
            {
                XAttribute nameAttribute = sheet.Attribute("name");
                if (nameAttribute == null || !string.Equals(nameAttribute.Value, worksheetName, StringComparison.Ordinal))
                {
                    continue;
                }

                XAttribute idAttribute = sheet.Attribute(relationshipNamespace + "id");
                if (idAttribute != null)
                {
                    relationshipId = idAttribute.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(relationshipId))
            {
                throw new InvalidOperationException("Could not find worksheet '" + worksheetName + "' in workbook.");
            }

            foreach (XElement relationship in relationshipsDocument.Descendants(packageRelationshipNamespace + "Relationship"))
            {
                XAttribute idAttribute = relationship.Attribute("Id");
                if (idAttribute == null || !string.Equals(idAttribute.Value, relationshipId, StringComparison.Ordinal))
                {
                    continue;
                }

                XAttribute targetAttribute = relationship.Attribute("Target");
                if (targetAttribute == null)
                {
                    break;
                }

                string target = targetAttribute.Value.Replace('\\', '/');
                if (target.StartsWith("/", StringComparison.Ordinal))
                {
                    return target.TrimStart('/');
                }

                if (!target.StartsWith("xl/", StringComparison.Ordinal))
                {
                    target = "xl/" + target.TrimStart('/');
                }

                return target;
            }

            throw new InvalidOperationException("Could not resolve worksheet relationship for '" + worksheetName + "'.");
        }

        private static Dictionary<string, string> LoadWorksheetCells(ZipArchive archive, string worksheetPath, IList<string> sharedStrings)
        {
            Dictionary<string, string> loadedCells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ZipArchiveEntry entry = archive.GetEntry(worksheetPath);
            if (entry == null)
            {
                throw new InvalidOperationException("Could not find worksheet entry '" + worksheetPath + "'.");
            }

            using (Stream stream = entry.Open())
            {
                XDocument document = XDocument.Load(stream);
                XNamespace ns = document.Root.Name.Namespace;
                foreach (XElement cell in document.Descendants(ns + "c"))
                {
                    XAttribute referenceAttribute = cell.Attribute("r");
                    if (referenceAttribute == null)
                    {
                        continue;
                    }

                    string value = ReadCellValue(cell, ns, sharedStrings);
                    loadedCells[referenceAttribute.Value] = value;
                }
            }

            return loadedCells;
        }

        private static string ReadCellValue(XElement cell, XNamespace ns, IList<string> sharedStrings)
        {
            XAttribute typeAttribute = cell.Attribute("t");
            string cellType = typeAttribute != null ? typeAttribute.Value : string.Empty;

            if (cellType == "inlineStr")
            {
                XElement inlineText = cell.Element(ns + "is");
                return inlineText != null ? inlineText.Value : string.Empty;
            }

            XElement valueElement = cell.Element(ns + "v");
            string rawValue = valueElement != null ? valueElement.Value : string.Empty;
            if (cellType == "s")
            {
                int index;
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    index >= 0 &&
                    index < sharedStrings.Count)
                {
                    return sharedStrings[index];
                }
            }

            return rawValue;
        }
    }
}
