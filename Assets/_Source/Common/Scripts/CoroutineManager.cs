using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// ================================================================
/// UNITY COROUTINES — COMPLETE GENERIC REFERENCE CLASS
/// ================================================================
/// Attach to any GameObject. Every use-case is self-contained
/// and production-ready. All yield instructions are demonstrated.
///
/// USE-CASE INDEX
/// ──────────────────────────────────────────────────────────────
///  1.  Basic coroutine start / stop
///  2.  WaitForSeconds
///  3.  WaitForSecondsRealtime   (ignores Time.timeScale)
///  4.  WaitForEndOfFrame        (after all rendering)
///  5.  WaitForFixedUpdate       (physics step sync)
///  6.  WaitUntil                (condition lambda)
///  7.  WaitWhile                (condition lambda)
///  8.  yield return null         (next frame / Update tick)
///  9.  Chaining coroutines      (yield return StartCoroutine)
/// 10.  Nested coroutines
/// 11.  Coroutine with return value via callback / ref
/// 12.  Coroutine with IEnumerator<T> + custom yielder
/// 13.  Cancelling a specific running coroutine
/// 14.  Cancelling ALL coroutines on object
/// 15.  Coroutine as a timer / countdown
/// 16.  Coroutine as a repeating invoke (InvokeRepeating alt.)
/// 17.  Coroutine with progress reporting
/// 18.  Coroutine sequence queue (run list of routines)
/// 19.  HTTP GET with UnityWebRequest
/// 20.  HTTP POST with JSON body
/// 21.  Async scene loading with progress bar
/// 22.  Smooth lerp / animation coroutine
/// 23.  Typewriter text effect
/// 24.  Fade in / fade out (CanvasGroup alpha)
/// 25.  Screenshake via coroutine
/// 26.  Delayed callback (coroutine as WaitForSeconds wrapper)
/// 27.  Coroutine-based event system
/// 28.  Parallel coroutines with join (all must finish)
/// 29.  Race coroutines (first to finish wins)
/// 30.  Global static CoroutineRunner (run from non-MonoBehaviour)
/// ================================================================
/// </summary>
public class CoroutineManager : MonoBehaviour
{
    // ── Handles for running coroutines (needed to stop them) ──
    private Coroutine _timerHandle;
    private Coroutine _repeatingHandle;
    private Coroutine _animHandle;

    // ── Shared state used by several demos ──
    private bool  _playerIsAlive   = true;
    private bool  _dataLoaded      = false;
    private bool  _doorOpen        = false;
    private float _health          = 100f;
    private int   _pendingParallel = 0;

    // ── Events for use-case 27 ──
    public static event Action<string> OnCoroutineEvent;

    // =========================================================
    // Unity lifecycle — run demos from Start
    // =========================================================
    private void Start()
    {
        // Comment out or swap the call you want to test.

        StartCoroutine(UC01_BasicStartStop());
        StartCoroutine(UC02_WaitForSeconds());
        StartCoroutine(UC03_WaitForSecondsRealtime());
        StartCoroutine(UC04_WaitForEndOfFrame());
        StartCoroutine(UC05_WaitForFixedUpdate());
        StartCoroutine(UC06_WaitUntil());
        StartCoroutine(UC07_WaitWhile());
        StartCoroutine(UC08_YieldNull());
        StartCoroutine(UC09_ChainedCoroutines());
        StartCoroutine(UC10_NestedCoroutines());
        StartCoroutine(UC11_ReturnValueViaCallback(val => Debug.Log($"[UC11] Received: {val}")));
        StartCoroutine(UC13_CancelSpecific());
        StartCoroutine(UC15_Timer(5f, () => Debug.Log("[UC15] Countdown done!")));
        _repeatingHandle = StartCoroutine(UC16_Repeating(1f, () => Debug.Log("[UC16] Tick")));
        StartCoroutine(UC17_WithProgress(p => Debug.Log($"[UC17] {p:P0}")));
        StartCoroutine(UC18_CoroutineQueue());
        StartCoroutine(UC19_HttpGet("https://jsonplaceholder.typicode.com/todos/1",
            result => Debug.Log($"[UC19] {result}")));
        StartCoroutine(UC20_HttpPost("https://jsonplaceholder.typicode.com/posts",
            "{\"title\":\"test\",\"userId\":1}",
            result => Debug.Log($"[UC20] {result}")));
        // StartCoroutine(UC21_LoadScene("GameScene", p => Debug.Log($"[UC21] {p:P0}")));
        StartCoroutine(UC22_LerpPosition(transform, Vector3.zero, Vector3.up * 3f, 2f));
        StartCoroutine(UC26_DelayedCallback(1.5f, () => Debug.Log("[UC26] Delayed!")));
        StartCoroutine(UC27_EventCoroutine("LevelStart"));
        StartCoroutine(UC28_ParallelJoin());
        StartCoroutine(UC29_Race());
    }

    // =========================================================
    // UC 1 — Basic start & stop
    // =========================================================
    /// <summary>
    /// StartCoroutine begins execution; StopCoroutine halts it.
    /// Always store the Coroutine reference if you need to stop it.
    /// </summary>
    private IEnumerator UC01_BasicStartStop()
    {
        Debug.Log("[UC01] Coroutine started.");
        yield return new WaitForSeconds(0.5f);
        Debug.Log("[UC01] Running after 0.5 s.");
        yield return new WaitForSeconds(0.5f);
        Debug.Log("[UC01] Coroutine finished naturally.");
    }

    // =========================================================
    // UC 2 — WaitForSeconds
    // =========================================================
    /// <summary>
    /// Pauses execution for N seconds, scaled by Time.timeScale.
    /// If timeScale = 0 (paused game) this yield will never resume.
    /// Use WaitForSecondsRealtime for UI that must tick while paused.
    /// </summary>
    private IEnumerator UC02_WaitForSeconds()
    {
        Debug.Log("[UC02] Before wait.");
        yield return new WaitForSeconds(2f);
        Debug.Log("[UC02] 2 scaled seconds passed.");
    }

    // =========================================================
    // UC 3 — WaitForSecondsRealtime
    // =========================================================
    /// <summary>
    /// Pauses for N REAL-TIME seconds regardless of Time.timeScale.
    /// Essential for pause menus, cutscene skips, and cooldown UI
    /// that must keep ticking even when the game is frozen.
    /// </summary>
    private IEnumerator UC03_WaitForSecondsRealtime()
    {
        Time.timeScale = 0f;                        // freeze game
        Debug.Log("[UC03] Game paused, waiting 1 real second...");
        yield return new WaitForSecondsRealtime(1f);
        Time.timeScale = 1f;
        Debug.Log("[UC03] Real second elapsed — game unpaused.");
    }

    // =========================================================
    // UC 4 — WaitForEndOfFrame
    // =========================================================
    /// <summary>
    /// Resumes after Unity has finished rendering the current frame
    /// (after OnRenderImage / post-processing). Use for reading pixels
    /// with Texture2D.ReadPixels, screenshot capture, or end-of-frame
    /// canvas layout reads.
    /// </summary>
    private IEnumerator UC04_WaitForEndOfFrame()
    {
        Debug.Log("[UC04] Waiting for frame render to complete...");
        yield return new WaitForEndOfFrame();
        // Safe here to call ReadPixels, EncodeToJPG, etc.
        Debug.Log("[UC04] Frame fully rendered — safe to capture screen.");
    }

    // =========================================================
    // UC 5 — WaitForFixedUpdate
    // =========================================================
    /// <summary>
    /// Resumes after the next FixedUpdate physics step.
    /// Use when you need a physics result (rigidbody velocity,
    /// collision outcome) that only updates in FixedUpdate.
    /// </summary>
    private IEnumerator UC05_WaitForFixedUpdate()
    {
        Debug.Log("[UC05] Applying physics impulse...");
        // GetComponent<Rigidbody>().AddForce(Vector3.up * 500f);
        yield return new WaitForFixedUpdate();
        Debug.Log("[UC05] Physics step processed — velocity is now updated.");
    }

    // =========================================================
    // UC 6 — WaitUntil
    // =========================================================
    /// <summary>
    /// Suspends the coroutine and checks the lambda EACH FRAME.
    /// Resumes as soon as the condition returns true.
    /// Perfect for: "wait until data is loaded", "wait until enemy dies",
    /// "wait until player enters trigger".
    /// </summary>
    private IEnumerator UC06_WaitUntil()
    {
        Debug.Log("[UC06] Waiting until _dataLoaded == true...");

        // Simulate a background load completing after 1.5 s
        StartCoroutine(SimulateDataLoad(1.5f));

        yield return new WaitUntil(() => _dataLoaded);

        Debug.Log("[UC06] Data is loaded! Continuing.");
    }

    private IEnumerator SimulateDataLoad(float delay)
    {
        yield return new WaitForSeconds(delay);
        _dataLoaded = true;
    }

    // =========================================================
    // UC 7 — WaitWhile
    // =========================================================
    /// <summary>
    /// Suspends while the condition is TRUE; resumes when it becomes FALSE.
    /// Think of it as the inverse of WaitUntil.
    /// Great for: "wait while door is open", "wait while player is stunned",
    /// "wait while dialog box is showing".
    /// </summary>
    private IEnumerator UC07_WaitWhile()
    {
        _doorOpen = true;
        Debug.Log("[UC07] Waiting while door is open...");

        // Simulate the door closing after 1 s
        StartCoroutine(CloseDoorAfter(1f));

        yield return new WaitWhile(() => _doorOpen);

        Debug.Log("[UC07] Door closed — player can now pass through.");
    }

    private IEnumerator CloseDoorAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _doorOpen = false;
    }

    // =========================================================
    // UC 8 — yield return null (skip one frame)
    // =========================================================
    /// <summary>
    /// Yields for exactly one Update frame. Use inside tight loops
    /// to spread heavy work across multiple frames and prevent
    /// frame spikes / hitches. Essential for procedural generation,
    /// pathfinding initialisation, and large data processing.
    /// </summary>
    private IEnumerator UC08_YieldNull()
    {
        int total = 10_000;
        int batchSize = 500;

        for (int i = 0; i < total; i++)
        {
            // heavy work here (e.g., mesh generation step)
            _ = Mathf.Sqrt(i);

            if (i % batchSize == 0)
            {
                Debug.Log($"[UC08] Processed {i}/{total} — yielding frame.");
                yield return null;   // let Unity render a frame
            }
        }

        Debug.Log("[UC08] All work complete across multiple frames.");
    }

    // =========================================================
    // UC 9 — Chaining coroutines
    // =========================================================
    /// <summary>
    /// yield return StartCoroutine(Other()) blocks the parent
    /// coroutine until the child coroutine fully completes.
    /// Use to build readable sequential workflows without nesting
    /// all logic in a single long coroutine.
    /// </summary>
    private IEnumerator UC09_ChainedCoroutines()
    {
        Debug.Log("[UC09] Step 1: initialise.");
        yield return StartCoroutine(Step_Initialise());

        Debug.Log("[UC09] Step 2: load assets.");
        yield return StartCoroutine(Step_LoadAssets());

        Debug.Log("[UC09] Step 3: spawn entities.");
        yield return StartCoroutine(Step_SpawnEntities());

        Debug.Log("[UC09] All chained steps done.");
    }

    private IEnumerator Step_Initialise()    { yield return new WaitForSeconds(0.2f); }
    private IEnumerator Step_LoadAssets()    { yield return new WaitForSeconds(0.3f); }
    private IEnumerator Step_SpawnEntities() { yield return new WaitForSeconds(0.1f); }

    // =========================================================
    // UC 10 — Nested coroutines
    // =========================================================
    /// <summary>
    /// A coroutine can start another coroutine WITHOUT waiting for it
    /// by simply calling StartCoroutine() without yield return.
    /// Use this for fire-and-forget child effects (particles, sounds)
    /// while the parent continues its own logic.
    /// </summary>
    private IEnumerator UC10_NestedCoroutines()
    {
        Debug.Log("[UC10] Parent: starting, launching non-blocking child.");
        StartCoroutine(UC10_Child());          // fire-and-forget
        yield return new WaitForSeconds(0.5f);
        Debug.Log("[UC10] Parent: finished before child.");
    }

    private IEnumerator UC10_Child()
    {
        yield return new WaitForSeconds(2f);
        Debug.Log("[UC10] Child: finished (parent already done).");
    }

    // =========================================================
    // UC 11 — Return a value via callback / Action<T>
    // =========================================================
    /// <summary>
    /// IEnumerator cannot return values directly. Pass an Action<T>
    /// callback and invoke it with the result when ready.
    /// Alternative: use a shared field or a wrapper class with a
    /// .result property (see UC12).
    /// </summary>
    private IEnumerator UC11_ReturnValueViaCallback(Action<int> onComplete)
    {
        yield return new WaitForSeconds(0.5f);
        int computedValue = 42;
        onComplete?.Invoke(computedValue);
    }

    // =========================================================
    // UC 12 — Typed result wrapper (CoroutineResult<T>)
    // =========================================================
    /// <summary>
    /// A generic wrapper class lets coroutines "return" typed values
    /// that the caller can read after awaiting the coroutine.
    /// </summary>
    public class CoroutineResult<T>
    {
        public T     Value     { get; set; }
        public bool  IsDone   { get; set; }
        public Exception Error { get; set; }
    }

    private IEnumerator UC12_ComputeWithResult(CoroutineResult<string> result)
    {
        yield return new WaitForSeconds(0.5f);
        try
        {
            result.Value = "Hello from coroutine";
            result.IsDone = true;
        }
        catch (Exception ex)
        {
            result.Error  = ex;
            result.IsDone = true;
        }
    }

    // Caller usage:
    private IEnumerator UC12_CallerExample()
    {
        var result = new CoroutineResult<string>();
        yield return StartCoroutine(UC12_ComputeWithResult(result));

        if (result.Error != null) Debug.LogError(result.Error);
        else Debug.Log($"[UC12] Result: {result.Value}");
    }

    // =========================================================
    // UC 13 — Cancel a specific coroutine
    // =========================================================
    /// <summary>
    /// Store the Coroutine reference returned by StartCoroutine.
    /// Pass it to StopCoroutine to halt that one routine.
    /// Always null-check before stopping — StopCoroutine(null) throws.
    /// </summary>
    private IEnumerator UC13_CancelSpecific()
    {
        Coroutine handle = StartCoroutine(UC13_LongTask());
        yield return new WaitForSeconds(0.3f);

        StopCoroutine(handle);      // stop it mid-way
        Debug.Log("[UC13] Long task cancelled after 0.3 s.");
    }

    private IEnumerator UC13_LongTask()
    {
        for (int i = 0; i < 10; i++)
        {
            Debug.Log($"[UC13] Long task tick {i}");
            yield return new WaitForSeconds(0.2f);
        }
    }

    // =========================================================
    // UC 14 — Cancel ALL coroutines on this MonoBehaviour
    // =========================================================
    /// <summary>
    /// StopAllCoroutines() halts every coroutine running on this
    /// MonoBehaviour. Call it in OnDestroy or on state transitions
    /// (e.g., player death) to prevent ghost routines running on a
    /// destroyed or reset object.
    /// </summary>
    private void UC14_StopAll()
    {
        StopAllCoroutines();
        Debug.Log("[UC14] All coroutines stopped.");
    }

    private void OnDestroy()
    {
        StopAllCoroutines();    // always clean up on destroy
    }

    // =========================================================
    // UC 15 — Timer / countdown
    // =========================================================
    /// <summary>
    /// Counts down from duration to 0, invoking onTick each second
    /// and onComplete when done. Store the handle to cancel early.
    /// </summary>
    public IEnumerator UC15_Timer(float duration, Action onComplete,
                                   Action<float> onTick = null)
    {
        float remaining = duration;
        while (remaining > 0f)
        {
            onTick?.Invoke(remaining);
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }
        onComplete?.Invoke();
    }

    // =========================================================
    // UC 16 — Repeating invoke (coroutine alternative)
    // =========================================================
    /// <summary>
    /// Loops forever at a fixed interval, calling the action each tick.
    /// More flexible than InvokeRepeating: you can adjust the interval,
    /// skip ticks conditionally, and cancel via StopCoroutine.
    /// </summary>
    public IEnumerator UC16_Repeating(float interval, Action onTick)
    {
        while (true)
        {
            yield return new WaitForSeconds(interval);
            onTick?.Invoke();
        }
    }

    // =========================================================
    // UC 17 — Progress reporting
    // =========================================================
    /// <summary>
    /// Reports fractional progress [0..1] via a callback so the caller
    /// can drive a loading bar or spinner without polling.
    /// </summary>
    public IEnumerator UC17_WithProgress(Action<float> onProgress,
                                          int steps = 10)
    {
        for (int i = 0; i <= steps; i++)
        {
            onProgress?.Invoke((float)i / steps);
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log("[UC17] Progress complete.");
    }

    // =========================================================
    // UC 18 — Sequential coroutine queue
    // =========================================================
    /// <summary>
    /// Runs a list of coroutine factories one after another.
    /// Add IEnumerator-returning lambdas/methods to the queue;
    /// each runs to completion before the next begins.
    /// </summary>
    private IEnumerator UC18_CoroutineQueue()
    {
        var queue = new Queue<Func<IEnumerator>>();
        queue.Enqueue(() => UC18_Step("A", 0.2f));
        queue.Enqueue(() => UC18_Step("B", 0.3f));
        queue.Enqueue(() => UC18_Step("C", 0.1f));

        while (queue.Count > 0)
            yield return StartCoroutine(queue.Dequeue()());

        Debug.Log("[UC18] Queue complete.");
    }

    private IEnumerator UC18_Step(string name, float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log($"[UC18] Step {name} done.");
    }

    // =========================================================
    // UC 19 — HTTP GET (UnityWebRequest)
    // =========================================================
    /// <summary>
    /// Sends a GET request and delivers the response text via callback.
    /// UnityWebRequest.SendWebRequest() returns an AsyncOperation that
    /// can be yielded directly — Unity polls isDone every frame.
    /// </summary>
    public IEnumerator UC19_HttpGet(string url, Action<string> onSuccess,
                                     Action<string> onError = null)
    {
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke(req.error);
    }

    // =========================================================
    // UC 20 — HTTP POST with JSON
    // =========================================================
    /// <summary>
    /// Posts a JSON body. Sets Content-Type header and uses
    /// UploadHandlerRaw for the UTF-8 encoded payload.
    /// Extend headers for Bearer token auth as needed.
    /// </summary>
    public IEnumerator UC20_HttpPost(string url, string jsonBody,
                                      Action<string> onSuccess,
                                      Action<string> onError = null)
    {
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using UnityWebRequest req = new UnityWebRequest(url, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(bodyBytes),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");
        // req.SetRequestHeader("Authorization", "Bearer " + _token);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke(req.error);
    }

    // =========================================================
    // UC 21 — Async scene loading with progress
    // =========================================================
    /// <summary>
    /// Loads a scene without a frame hitch. Keeps allowSceneActivation
    /// false until progress reaches 0.9 (fully loaded but not yet shown),
    /// then flips it when your loading screen is ready.
    /// </summary>
    public IEnumerator UC21_LoadScene(string sceneName,
                                       Action<float> onProgress = null,
                                       Action onReady = null)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            onProgress?.Invoke(op.progress / 0.9f);   // normalise to 0..1
            yield return null;
        }

        onProgress?.Invoke(1f);
        onReady?.Invoke();                              // show "Press any key"
        yield return new WaitUntil(() => Input.anyKeyDown);
        op.allowSceneActivation = true;
    }

    // =========================================================
    // UC 22 — Smooth lerp / tween coroutine
    // =========================================================
    /// <summary>
    /// Generic lerp driver. Drives any value from start to end over
    /// duration seconds using a normalised t. Pass an easing function
    /// for non-linear motion (ease-in, ease-out, bounce, etc.).
    /// </summary>
    public IEnumerator UC22_LerpPosition(Transform target,
                                          Vector3 from, Vector3 to,
                                          float duration,
                                          Func<float, float> easing = null)
    {
        easing = easing ?? (t => t);    // default: linear
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = easing(elapsed / duration);
            target.position = Vector3.LerpUnclamped(from, to, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        target.position = to;
        Debug.Log("[UC22] Lerp complete.");
    }

    // Easing helpers — pass any of these as the easing parameter:
    public static float EaseInQuad(float t)  => t * t;
    public static float EaseOutQuad(float t) => t * (2f - t);
    public static float EaseInOut(float t)   => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

    // =========================================================
    // UC 23 — Typewriter text effect
    // =========================================================
    /// <summary>
    /// Reveals text one character at a time. Pass a TMPro or legacy
    /// Text component's setter as the onChar callback.
    /// Use WaitForSecondsRealtime so it works while game is paused.
    /// </summary>
    public IEnumerator UC23_Typewriter(string fullText, float charDelay,
                                        Action<string> onChar,
                                        Action onComplete = null)
    {
        for (int i = 1; i <= fullText.Length; i++)
        {
            onChar?.Invoke(fullText.Substring(0, i));
            yield return new WaitForSecondsRealtime(charDelay);
        }
        onComplete?.Invoke();
        Debug.Log("[UC23] Typewriter finished.");
    }

    // =========================================================
    // UC 24 — Fade in / out (CanvasGroup alpha)
    // =========================================================
    /// <summary>
    /// Smoothly interpolates a CanvasGroup's alpha between 0 and 1.
    /// Set interactable/blocksRaycasts at the start and end to
    /// prevent interaction during the transition.
    /// </summary>
    public IEnumerator UC24_Fade(CanvasGroup cg, float targetAlpha,
                                  float duration)
    {
        float startAlpha = cg.alpha;
        float elapsed    = 0f;

        cg.interactable    = false;
        cg.blocksRaycasts  = false;

        while (elapsed < duration)
        {
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            elapsed += Time.unscaledDeltaTime;   // real-time so menus work
            yield return null;
        }

        cg.alpha           = targetAlpha;
        cg.interactable    = Mathf.Approximately(targetAlpha, 1f);
        cg.blocksRaycasts  = Mathf.Approximately(targetAlpha, 1f);
        Debug.Log($"[UC24] Fade to {targetAlpha:F0} complete.");
    }

    // =========================================================
    // UC 25 — Camera screenshake
    // =========================================================
    /// <summary>
    /// Offsets the camera transform by random values that decay over
    /// duration. Store the handle and StopCoroutine+reset on death or
    /// scene unload to avoid drifted camera position.
    /// </summary>
    public IEnumerator UC25_Screenshake(Transform camTransform,
                                         float duration, float magnitude)
    {
        Vector3 origin  = camTransform.localPosition;
        float   elapsed = 0f;

        while (elapsed < duration)
        {
            float decayFactor = 1f - (elapsed / duration);   // 1 → 0
            float offsetX = UnityEngine.Random.Range(-1f, 1f) * magnitude * decayFactor;
            float offsetY = UnityEngine.Random.Range(-1f, 1f) * magnitude * decayFactor;

            camTransform.localPosition = origin + new Vector3(offsetX, offsetY, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        camTransform.localPosition = origin;
        Debug.Log("[UC25] Screenshake done.");
    }

    // =========================================================
    // UC 26 — Delayed callback wrapper
    // =========================================================
    /// <summary>
    /// A clean one-liner replacement for Invoke("MethodName", delay).
    /// Type-safe, supports lambdas and closures, can be stopped.
    /// Use WaitForSecondsRealtime variant for pause-proof delays.
    /// </summary>
    public IEnumerator UC26_DelayedCallback(float delay, Action callback,
                                             bool realtime = false)
    {
        if (realtime) yield return new WaitForSecondsRealtime(delay);
        else          yield return new WaitForSeconds(delay);

        callback?.Invoke();
    }

    // =========================================================
    // UC 27 — Coroutine-based event system
    // =========================================================
    /// <summary>
    /// Raise an event string, then yield one frame so all listeners
    /// registered this frame get the event before the sender continues.
    /// Useful for decoupled game-state signalling without coupling
    /// sender to receiver.
    /// </summary>
    public IEnumerator UC27_EventCoroutine(string eventName)
    {
        Debug.Log($"[UC27] Raising event: {eventName}");
        OnCoroutineEvent?.Invoke(eventName);
        yield return null;   // allow listeners to react in the same frame
        Debug.Log($"[UC27] All listeners notified for: {eventName}");
    }

    // =========================================================
    // UC 28 — Parallel coroutines with join (WhenAll equivalent)
    // =========================================================
    /// <summary>
    /// Launches multiple coroutines simultaneously and waits until
    /// ALL complete. Uses a shared counter decremented by each child.
    /// The parent yields while (_pendingParallel > 0).
    /// </summary>
    private IEnumerator UC28_ParallelJoin()
    {
        _pendingParallel = 3;

        StartCoroutine(UC28_Worker("Alpha",   0.5f));
        StartCoroutine(UC28_Worker("Beta",    1.0f));
        StartCoroutine(UC28_Worker("Gamma",   0.3f));

        yield return new WaitWhile(() => _pendingParallel > 0);
        Debug.Log("[UC28] All parallel workers done.");
    }

    private IEnumerator UC28_Worker(string name, float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log($"[UC28] Worker '{name}' finished.");
        _pendingParallel--;
    }

    // =========================================================
    // UC 29 — Race coroutines (WhenAny equivalent)
    // =========================================================
    /// <summary>
    /// Starts multiple coroutines and proceeds as soon as the FIRST
    /// sets a shared flag. The others keep running — StopCoroutine
    /// their handles if you want to cancel the losers.
    /// </summary>
    private bool _raceWon = false;

    private IEnumerator UC29_Race()
    {
        _raceWon = false;

        Coroutine c1 = StartCoroutine(UC29_Racer("Fast",  0.4f));
        Coroutine c2 = StartCoroutine(UC29_Racer("Slow",  1.2f));
        Coroutine c3 = StartCoroutine(UC29_Racer("Medium",0.7f));

        yield return new WaitUntil(() => _raceWon);

        // Cancel losers
        StopCoroutine(c1);
        StopCoroutine(c2);
        StopCoroutine(c3);
        Debug.Log("[UC29] Race complete — losers cancelled.");
    }

    private IEnumerator UC29_Racer(string name, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_raceWon)
        {
            _raceWon = true;
            Debug.Log($"[UC29] '{name}' won the race!");
        }
    }

    // =========================================================
    // UC 30 — Global static CoroutineRunner
    // =========================================================
    /// <summary>
    /// Run coroutines from plain C# classes, ScriptableObjects,
    /// or static contexts that have no MonoBehaviour.
    /// The runner creates a hidden persistent GameObject on first use.
    /// </summary>
    public static class CoroutineRunner
    {
        private static MonoBehaviour _runner;

        private static MonoBehaviour GetRunner()
        {
            if (_runner != null) return _runner;

            var go = new GameObject("[CoroutineRunner]");
            GameObject.DontDestroyOnLoad(go);
            _runner = go.AddComponent<CoroutineRunnerBehaviour>();
            return _runner;
        }

        public static Coroutine Start(IEnumerator routine)
            => GetRunner().StartCoroutine(routine);

        public static void Stop(Coroutine routine)
            => GetRunner().StopCoroutine(routine);

        public static void StopAll()
            => GetRunner().StopAllCoroutines();
    }

    // Hidden MonoBehaviour backing the static runner
    private class CoroutineRunnerBehaviour : MonoBehaviour { }
}
