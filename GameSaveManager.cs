using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System;
using UObject = UnityEngine.Object;


public static class GameSaveManager
{
    
    private static readonly string MainPath = Path.Combine(Application.persistentDataPath, "savegame.json");
    private static readonly string BakPath = Path.Combine(Application.persistentDataPath, "savegame.bak");
    private static readonly string TmpPath = Path.Combine(Application.persistentDataPath, "savegame.tmp");

    
    public static bool HasLocalSave()
    {
        return File.Exists(MainPath) || File.Exists(BakPath);
    }

    public static void Save()
    {
        var holder = UserDataHolder.Instance;
        if (holder == null)
        {
            Debug.LogWarning("[Save] UserDataHolder missing");
            return;
        }

        var bubbles = CollectBubbles();
        var data = new SaveData
        {
            currentScore = holder.CurrentScore,
            bestScore = holder.BestScore,
            coins = holder.Coins,
            bubbles = bubbles,
            categoriesProgress = holder.categoriesProgress,
            collectionCompletions = holder.collectionCompletions,

            boosterInventory = holder.boosters
        };

        if (data.bubbles == null || data.bubbles.Count == 0)
        {
            Debug.Log("[Save] Field snapshot is empty. Skip saving session to avoid wiping/mixing.");
            return;
        }


        string json = JsonUtility.ToJson(data, prettyPrint: false);

        try
        {
            using (var fs = new FileStream(TmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(json);
                sw.Flush();
                fs.Flush(true);
            }
            if (File.Exists(MainPath)) File.Copy(MainPath, BakPath, true);
            if (File.Exists(MainPath)) File.Delete(MainPath);
            File.Move(TmpPath, MainPath);

            holder.hasSavedData = true;
            holder.savedProgressJson = json;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Failed: {e}");
        }
        finally
        {
            try { if (File.Exists(TmpPath)) File.Delete(TmpPath); } catch { }
        }

    }

    private static bool TryLoadExisting(out SaveData data)
    {
        data = null;
        if (TryReadJson(MainPath, out var main)) { data = main; return true; }
        if (TryReadJson(BakPath, out var bak)) { data = bak; return true; }
        return false;
    }


    public static SaveData Load()
    {
        if (TryReadJson(MainPath, out var dataFromMain))
            return dataFromMain;

        if (TryReadJson(BakPath, out var dataFromBak))
        {
            Debug.LogWarning("[Save] Main is missing/corrupted. Restored from backup.");
            return dataFromBak;
        }

        return null;
    }

    public static void DeleteSave()
    {
        try { if (File.Exists(MainPath)) File.Delete(MainPath); } catch { }
        try { if (File.Exists(BakPath)) File.Delete(BakPath); } catch { }

        var holder = UserDataHolder.Instance;
        if (holder != null)
        {
            holder.hasSavedData = false;
            holder.savedProgressJson = "";
        }
    }


    private static bool TryReadJson(string path, out SaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;
            data = JsonUtility.FromJson<SaveData>(json);
            return data != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Save] Read failed from {path}: {e}");
            return false;
        }
    }

    private static List<BubbleSnapshot> CollectBubbles()
    {
        var result = new List<BubbleSnapshot>();
        var all = UnityEngine.Object.FindObjectsByType<BubbleMergeHandler>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var mh in all)
        {
            var go = mh.gameObject;
            var rb = go.GetComponent<Rigidbody2D>();
            var touch = go.GetComponent<BubbleTouchController>();

            if (touch != null && touch.enabled)
                continue;

            int mergeLevel = 0;
            var mid = go.GetComponentInChildren<MergeItemData>();
            if (mid != null) mergeLevel = mid.mergeLevel;

            result.Add(new BubbleSnapshot
            {
                mergeLevel = mergeLevel,
                position = go.transform.position,
                linearVelocity = rb ? rb.linearVelocity : Vector2.zero,
                isControlledTop = false 
            });
        }
        return result;
    }

}

