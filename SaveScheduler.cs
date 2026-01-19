using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveScheduler : MonoBehaviour
{
    public static SaveScheduler Instance { get; private set; }

    [Header("Autosave")]
    [Tooltip("Ïåðèîä àâòîñåéâà â ñåêóíäàõ (ðåàëüíîãî âðåìåíè)")]
    [SerializeField] private float autosaveIntervalSeconds = 1.5f;
    [Tooltip("Ñîõðàíÿòü òîëüêî â èãðîâîé ñöåíå")]
    [SerializeField] private bool autosaveOnlyInGameScene = true;
    [Tooltip("Èìÿ èãðîâîé ñöåíû")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Lifecycle save (optional)")]
    [SerializeField] private bool handleLifecycleSaves = false;
    [SerializeField] private float lifecycleSaveCooldownSeconds = 0.25f;

    private bool pendingSave;
    private float lastLifecycleSaveRealtime;

    private Coroutine autosaveRoutine;
    private bool isSaving;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        autosaveRoutine = StartCoroutine(AutosaveLoop());
    }

    private void OnDisable()
    {
        if (autosaveRoutine != null)
        {
            StopCoroutine(autosaveRoutine);
            autosaveRoutine = null;
        }
    }

    public void RequestSave() => DoSaveNow(ignoreSceneGate: true);
    public void FlushNow() => DoSaveNow(ignoreSceneGate: true);

    private IEnumerator AutosaveLoop()
    {
        float period = Mathf.Max(0.2f, autosaveIntervalSeconds);
        var wait = new WaitForSecondsRealtime(period);

        while (true)
        {
            if (!autosaveOnlyInGameScene ||
                SceneManager.GetActiveScene().name == gameSceneName)
            {
                DoSaveNow(ignoreSceneGate: false);
            }

            yield return wait;
        }
    }

    private void DoSaveNow(bool ignoreSceneGate)
    {
        if (isSaving)
        {
            pendingSave = true;
            return;
        }

        if (!ignoreSceneGate && autosaveOnlyInGameScene && SceneManager.GetActiveScene().name != gameSceneName)
            return;

        isSaving = true;
        try
        {
            GameSaveManager.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveScheduler] Save failed: {e}");
        }
        finally
        {
            isSaving = false;

            if (pendingSave)
            {
                pendingSave = false;
                StartCoroutine(SaveNextFrame());
            }
        }
    }

    private IEnumerator SaveNextFrame()
    {
        yield return null;
        DoSaveNow(ignoreSceneGate: true);
    }



    private void OnApplicationPause(bool pause)
    {
        if (!handleLifecycleSaves) return;
        if (pause) ForceSave("pause");
    }

    private void OnApplicationQuit()
    {
        if (!handleLifecycleSaves) return;
        ForceSave("quit");
    }


    public void ForceSave(string reason = "")
    {
        if (Time.realtimeSinceStartup - lastLifecycleSaveRealtime < lifecycleSaveCooldownSeconds)
            return;

        lastLifecycleSaveRealtime = Time.realtimeSinceStartup;
        DoSaveNow(ignoreSceneGate: true);
    }

}

