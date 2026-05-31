using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class PawPalPhotoRecord
{
    public string Id;
    public string DogId;
    public string DogName;
    public string Title;
    public string FileName;
    public string ThumbnailFileName;
    public long CreatedUtcTicks;
    public int Width;
    public int Height;
    public bool Favorite;
}

[Serializable]
public sealed class PawPalPhotoAlbumData
{
    public List<PawPalPhotoRecord> Photos = new List<PawPalPhotoRecord>();
}

public sealed class PawPalPhotoAlbumStore
{
    private const string RootFolderName = "PawPalPhotos";
    private const string PhotosFolderName = "Photos";
    private const string ThumbnailsFolderName = "Thumbnails";
    private const string AlbumFileName = "pawpal_photo_album_v1.json";
    private const int ThumbnailSize = 192;

    private readonly List<PawPalPhotoRecord> records = new List<PawPalPhotoRecord>();

    public IReadOnlyList<PawPalPhotoRecord> Records
    {
        get { return records; }
    }

    public string RootDirectory
    {
        get { return Path.Combine(Application.persistentDataPath, RootFolderName); }
    }

    public string PhotosDirectory
    {
        get { return Path.Combine(RootDirectory, PhotosFolderName); }
    }

    public string ThumbnailsDirectory
    {
        get { return Path.Combine(RootDirectory, ThumbnailsFolderName); }
    }

    public void Load()
    {
        EnsureDirectories();
        records.Clear();

        string albumPath = GetAlbumPath();
        if (!File.Exists(albumPath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(albumPath);
            PawPalPhotoAlbumData data = JsonUtility.FromJson<PawPalPhotoAlbumData>(json);
            if (data != null && data.Photos != null)
            {
                records.AddRange(data.Photos);
            }

            bool pruned = PruneMissingFiles();
            SortLatestFirst();
            if (pruned)
            {
                SaveMetadata();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalPhotoAlbumStore could not load album metadata: " + exception.Message);
            records.Clear();
        }
    }

    public PawPalPhotoRecord SavePhoto(Texture2D source, string dogId, string dogName)
    {
        if (source == null)
        {
            return null;
        }

        EnsureDirectories();

        string id = CreatePhotoId();
        string fileName = id + ".png";
        string thumbnailFileName = id + "_thumb.png";
        string fullPath = Path.Combine(PhotosDirectory, fileName);
        string thumbnailPath = Path.Combine(ThumbnailsDirectory, thumbnailFileName);

        try
        {
            File.WriteAllBytes(fullPath, source.EncodeToPNG());

            Texture2D thumbnail = CreateThumbnail(source, ThumbnailSize);
            File.WriteAllBytes(thumbnailPath, thumbnail.EncodeToPNG());
            UnityEngine.Object.Destroy(thumbnail);

            PawPalPhotoRecord record = new PawPalPhotoRecord
            {
                Id = id,
                DogId = dogId ?? string.Empty,
                DogName = string.IsNullOrWhiteSpace(dogName) ? "Dog" : dogName,
                Title = string.Empty,
                FileName = fileName,
                ThumbnailFileName = thumbnailFileName,
                CreatedUtcTicks = DateTime.UtcNow.Ticks,
                Width = source.width,
                Height = source.height,
                Favorite = false
            };

            records.Insert(0, record);
            SortLatestFirst();
            SaveMetadata();
            return record;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalPhotoAlbumStore could not save photo: " + exception.Message);
            TryDeleteFile(fullPath);
            TryDeleteFile(thumbnailPath);
            return null;
        }
    }

    public Texture2D LoadPhotoTexture(PawPalPhotoRecord record)
    {
        return LoadTexture(GetPhotoPath(record));
    }

    public Texture2D LoadThumbnailTexture(PawPalPhotoRecord record)
    {
        return LoadTexture(GetThumbnailPath(record));
    }

    public bool DeletePhoto(PawPalPhotoRecord record)
    {
        if (record == null)
        {
            return false;
        }

        bool removed = false;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            PawPalPhotoRecord candidate = records[i];
            if (candidate != null && string.Equals(candidate.Id, record.Id, StringComparison.Ordinal))
            {
                records.RemoveAt(i);
                removed = true;
            }
        }

        TryDeleteFile(GetPhotoPath(record));
        TryDeleteFile(GetThumbnailPath(record));

        if (removed)
        {
            SaveMetadata();
        }

        return removed;
    }

    public bool SetFavorite(PawPalPhotoRecord record, bool favorite)
    {
        PawPalPhotoRecord stored = FindRecord(record);
        if (stored == null)
        {
            return false;
        }

        stored.Favorite = favorite;
        SaveMetadata();
        return true;
    }

    public bool SetTitle(PawPalPhotoRecord record, string title)
    {
        PawPalPhotoRecord stored = FindRecord(record);
        if (stored == null)
        {
            return false;
        }

        stored.Title = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
        SaveMetadata();
        return true;
    }

    public string GetPhotoPath(PawPalPhotoRecord record)
    {
        return record == null || string.IsNullOrEmpty(record.FileName)
            ? string.Empty
            : Path.Combine(PhotosDirectory, record.FileName);
    }

    public string GetThumbnailPath(PawPalPhotoRecord record)
    {
        return record == null || string.IsNullOrEmpty(record.ThumbnailFileName)
            ? string.Empty
            : Path.Combine(ThumbnailsDirectory, record.ThumbnailFileName);
    }

    private void SaveMetadata()
    {
        EnsureDirectories();

        PawPalPhotoAlbumData data = new PawPalPhotoAlbumData();
        data.Photos.AddRange(records);

        try
        {
            File.WriteAllText(GetAlbumPath(), JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalPhotoAlbumStore could not save album metadata: " + exception.Message);
        }
    }

    private bool PruneMissingFiles()
    {
        bool pruned = false;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            PawPalPhotoRecord record = records[i];
            if (record == null || !File.Exists(GetPhotoPath(record)) || !File.Exists(GetThumbnailPath(record)))
            {
                records.RemoveAt(i);
                pruned = true;
            }
        }

        return pruned;
    }

    private PawPalPhotoRecord FindRecord(PawPalPhotoRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.Id))
        {
            return null;
        }

        for (int i = 0; i < records.Count; i++)
        {
            PawPalPhotoRecord candidate = records[i];
            if (candidate != null && string.Equals(candidate.Id, record.Id, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private void SortLatestFirst()
    {
        records.Sort(delegate(PawPalPhotoRecord left, PawPalPhotoRecord right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return right.CreatedUtcTicks.CompareTo(left.CreatedUtcTicks);
        });
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(PhotosDirectory);
        Directory.CreateDirectory(ThumbnailsDirectory);
    }

    private string GetAlbumPath()
    {
        return Path.Combine(RootDirectory, AlbumFileName);
    }

    private static string CreatePhotoId()
    {
        return DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + UnityEngine.Random.Range(1000, 9999);
    }

    private static Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return texture;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalPhotoAlbumStore could not load texture: " + exception.Message);
            return null;
        }
    }

    private static Texture2D CreateThumbnail(Texture2D source, int size)
    {
        Texture2D thumbnail = new Texture2D(size, size, TextureFormat.RGBA32, false);
        thumbnail.name = "PawPalPhotoThumbnail";
        thumbnail.filterMode = FilterMode.Bilinear;
        thumbnail.wrapMode = TextureWrapMode.Clamp;

        float sourceAspect = source.width / Mathf.Max(1f, source.height);
        float sampleWidth01 = 1f;
        float sampleHeight01 = 1f;
        if (sourceAspect > 1f)
        {
            sampleWidth01 = 1f / sourceAspect;
        }
        else if (sourceAspect < 1f)
        {
            sampleHeight01 = sourceAspect;
        }

        float xMin = (1f - sampleWidth01) * 0.5f;
        float yMin = (1f - sampleHeight01) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            float v = yMin + ((y + 0.5f) / size) * sampleHeight01;
            for (int x = 0; x < size; x++)
            {
                float u = xMin + ((x + 0.5f) / size) * sampleWidth01;
                thumbnail.SetPixel(x, y, source.GetPixelBilinear(u, v));
            }
        }

        thumbnail.Apply(false);
        return thumbnail;
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("PawPalPhotoAlbumStore could not delete file: " + exception.Message);
        }
    }
}
