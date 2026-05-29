using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// ============================================================
/// UNITY ASYNC / AWAIT — COMPLETE REFERENCE SCRIPT
/// ============================================================
/// Drop this on any GameObject to get a fully self-contained
/// demo of every major async pattern used in Unity projects.
///
/// USE-CASE INDEX
/// ──────────────
/// 1.  Basic async void (fire-and-forget)
/// 2.  Returning a value from async (Task<T>)
/// 3.  Awaiting a Unity frame delay (Task.Delay)
/// 4.  Converting a Coroutine to async/await
/// 5.  HTTP GET — web request (UnityWebRequest)
/// 6.  HTTP POST — sending JSON data
/// 7.  Loading a scene asynchronously
/// 8.  Loading an AssetBundle / Addressable
/// 9.  Running CPU work off the main thread (Task.Run)
/// 10. Running multiple tasks in parallel (Task.WhenAll)
/// 11. Running tasks in a race (Task.WhenAny)
/// 12. Cancellation with CancellationToken
/// 13. Timeout pattern
/// 14. Progress reporting (IProgress<T>)
/// 15. Retry with exponential back-off
/// 16. Sequential async queue / pipeline
/// 17. Async initialisation guard (lazy singleton init)
/// 18. Error handling (try/catch/finally in async)
/// 19. Async event system
/// 20. UniTask drop-in (comment block — opt-in)
/// ============================================================
/// </summary>
public class AsyncAwaitManager:MonoBehaviour {
    // ── Shared cancellation for the lifetime of this object ──
    private CancellationTokenSource _lifetimeCts;

    // ── Simple event for use-case 19 ──
    public event Func<string,Task> OnAsyncEvent;

    // =========================================================
    // Unity lifecycle
    // =========================================================
    private void Awake() {
        _lifetimeCts = new CancellationTokenSource();
    }

    private async void Start() {
        // ── Demonstrate each use-case in sequence ──
        // Swap out the call you want to test; comment the rest.

        // 1. Fire-and-forget
        FireAndForget();

        // 2. Await a value
        int result = await ReturnValueAsync();
        Debug.Log($"[UC2] Result = {result}");

        // 3. Frame / time delay
        await FrameDelayAsync();

        // 4. Coroutine → async
        await CoroutineAsAsync();

        // 5 & 6. Web requests
        string json = await HttpGetAsync("https://jsonplaceholder.typicode.com/todos/1",
                                         _lifetimeCts.Token);
        Debug.Log($"[UC5] GET: {json}");

        await HttpPostAsync("https://jsonplaceholder.typicode.com/posts",
                            "{\"title\":\"foo\",\"body\":\"bar\",\"userId\":1}",
                            _lifetimeCts.Token);

        // 7. Scene loading (comment out if running in Editor without a second scene)
        // await LoadSceneAsync("GameScene", _lifetimeCts.Token);

        // 9. Off-thread work
        int[] data = await RunOnThreadPoolAsync(_lifetimeCts.Token);
        Debug.Log($"[UC9] First item = {data[0]}");

        // 10. Parallel tasks
        await RunParallelAsync(_lifetimeCts.Token);

        // 11. Race
        await RunRaceAsync(_lifetimeCts.Token);

        // 12. Cancellation demo
        await CancellationDemoAsync();

        // 13. Timeout
        await TimeoutDemoAsync(_lifetimeCts.Token);

        // 14. Progress reporting
        await ProgressDemoAsync(_lifetimeCts.Token);

        // 15. Retry
        string retryResult = await RetryAsync(
            () => FlakyNetworkCallAsync(_lifetimeCts.Token),
            maxRetries: 3,
            initialDelayMs: 500,
            _lifetimeCts.Token);
        Debug.Log($"[UC15] Retry result: {retryResult}");

        // 16. Pipeline
        await SequentialPipelineAsync(_lifetimeCts.Token);

        // 17. Lazy init guard
        await EnsureInitialisedAsync(_lifetimeCts.Token);

        // 18. Error handling
        await ErrorHandlingDemoAsync(_lifetimeCts.Token);

        // 19. Async event
        await RaiseAsyncEvent("Hello from async event");
    }

    private void OnDestroy() {
        // Always cancel and dispose the lifetime token on destroy.
        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
    }

    // =========================================================
    // USE-CASE 1 — Fire-and-forget (async void)
    // =========================================================
    /// <summary>
    /// Use for event handlers or top-level triggers where you
    /// don't need to await the result.
    /// WARNING: exceptions in async void crash the app.
    ///          Always wrap the body in try/catch.
    /// </summary>
    private async void FireAndForget() {
        try {
            await Task.Delay(200);
            Debug.Log("[UC1] Fire-and-forget completed.");
        } catch (Exception ex) {
            Debug.LogError($"[UC1] {ex.Message}");
        }
    }

    // =========================================================
    // USE-CASE 2 — Return a value from async (Task<T>)
    // =========================================================
    /// <summary>
    /// Prefer Task<T> over async void whenever you need the
    /// caller to await the result or handle exceptions cleanly.
    /// </summary>
    private async Task<int> ReturnValueAsync() {
        await Task.Delay(100);      // simulate work
        return 42;
    }

    // =========================================================
    // USE-CASE 3 — Frame / time delay
    // =========================================================
    /// <summary>
    /// Task.Delay uses wall-clock time (milliseconds).
    /// For frame-accurate delays use a Coroutine wrapper (UC4).
    /// </summary>
    private async Task FrameDelayAsync() {
        Debug.Log("[UC3] Before delay");
        await Task.Delay(TimeSpan.FromSeconds(1));
        Debug.Log("[UC3] After 1 s delay");
    }

    // =========================================================
    // USE-CASE 4 — Convert a Coroutine to async/await
    // =========================================================
    /// <summary>
    /// Wraps StartCoroutine in a TaskCompletionSource so you
    /// can await existing coroutines from async methods.
    /// </summary>
    private Task CoroutineAsAsync() {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(ExampleCoroutine(tcs));
        return tcs.Task;
    }

    private IEnumerator ExampleCoroutine(TaskCompletionSource<bool> tcs) {
        yield return new WaitForSeconds(0.5f);
        Debug.Log("[UC4] Coroutine finished, signalling async caller.");
        tcs.SetResult(true);
    }

    // =========================================================
    // USE-CASE 5 — HTTP GET
    // =========================================================
    /// <summary>
    /// Downloads a string payload from a URL.
    /// UnityWebRequest must run on the main Unity thread, but
    /// the async wrapper keeps calling code clean.
    /// </summary>
    public async Task<string> HttpGetAsync(string url,
                                           CancellationToken ct = default) {
        using var req = UnityWebRequest.Get(url);
        var op = req.SendWebRequest();

        while (!op.isDone) {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"[UC5] GET failed: {req.error}");

        Debug.Log("[UC5] GET success");
        return req.downloadHandler.text;
    }

    // =========================================================
    // USE-CASE 6 — HTTP POST
    // =========================================================
    /// <summary>
    /// Sends a JSON body to an API endpoint and reads the
    /// response. Adapt the Content-Type header as needed.
    /// </summary>
    public async Task<string> HttpPostAsync(string url,
                                            string jsonBody,
                                            CancellationToken ct = default) {
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        using var req = new UnityWebRequest(url,"POST") {
            uploadHandler = new UploadHandlerRaw(bodyRaw),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type","application/json");

        var op = req.SendWebRequest();
        while (!op.isDone) {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception($"[UC6] POST failed: {req.error}");

        Debug.Log($"[UC6] POST response: {req.downloadHandler.text}");
        return req.downloadHandler.text;
    }

    // =========================================================
    // USE-CASE 7 — Async scene loading
    // =========================================================
    /// <summary>
    /// Loads a scene without blocking the main thread.
    /// Pair with a loading-screen UI that reads progress.
    /// </summary>
    public async Task LoadSceneAsync(string sceneName,
                                     CancellationToken ct = default,
                                     IProgress<float> progress = null) {
        var op = UnityEngine.SceneManagement.SceneManager
                            .LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) {
            ct.ThrowIfCancellationRequested();
            progress?.Report(op.progress);
            await Task.Yield();
        }

        progress?.Report(1f);
        op.allowSceneActivation = true;
        Debug.Log($"[UC7] Scene '{sceneName}' loaded.");
    }

    // =========================================================
    // USE-CASE 8 — AssetBundle loading
    // =========================================================
    /// <summary>
    /// Loads an AssetBundle from a local path or URL and
    /// extracts a named asset. Dispose the bundle when done.
    /// </summary>
    public async Task<T> LoadAssetBundleAsync<T>(string bundlePath,
                                                  string assetName,
                                                  CancellationToken ct = default)
        where T : UnityEngine.Object {
        var bundleOp = AssetBundle.LoadFromFileAsync(bundlePath);
        while (!bundleOp.isDone) {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        AssetBundle bundle = bundleOp.assetBundle;
        if (bundle == null)
            throw new Exception($"[UC8] Failed to load bundle: {bundlePath}");

        var assetOp = bundle.LoadAssetAsync<T>(assetName);
        while (!assetOp.isDone) {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        bundle.Unload(false);   // unload bundle, keep loaded asset
        Debug.Log($"[UC8] Asset '{assetName}' loaded.");
        return assetOp.asset as T;
    }

    // =========================================================
    // USE-CASE 9 — CPU work on a thread-pool thread
    // =========================================================
    /// <summary>
    /// Task.Run offloads heavy computation (sorting, parsing,
    /// procedural generation) away from Unity's main thread.
    /// NEVER touch UnityEngine APIs inside Task.Run.
    /// </summary>
    public async Task<int[]> RunOnThreadPoolAsync(CancellationToken ct = default) {
        int[] result = await Task.Run(() => {
            ct.ThrowIfCancellationRequested();
            int[] data = new int[100_000];
            var rng = new System.Random(42);
            for (int i = 0;i < data.Length;i++)
                data[i] = rng.Next();
            Array.Sort(data);
            return data;
        },ct);

        Debug.Log("[UC9] Thread-pool sort finished on main thread now.");
        return result;
    }

    // =========================================================
    // USE-CASE 10 — Parallel tasks (Task.WhenAll)
    // =========================================================
    /// <summary>
    /// Fires multiple async operations simultaneously and waits
    /// for ALL of them to complete. Ideal for parallel downloads,
    /// batch saves, or independent async initialisations.
    /// </summary>
    public async Task RunParallelAsync(CancellationToken ct = default) {
        Task<string> t1 = HttpGetAsync("https://jsonplaceholder.typicode.com/todos/1",ct);
        Task<string> t2 = HttpGetAsync("https://jsonplaceholder.typicode.com/todos/2",ct);
        Task<string> t3 = HttpGetAsync("https://jsonplaceholder.typicode.com/todos/3",ct);

        string[] results = await Task.WhenAll(t1,t2,t3);
        Debug.Log($"[UC10] All {results.Length} parallel requests done.");
    }

    // =========================================================
    // USE-CASE 11 — Race tasks (Task.WhenAny)
    // =========================================================
    /// <summary>
    /// Returns as soon as the FIRST task completes. Use this
    /// for fallback servers, cache vs network races, or
    /// combined with a timeout task.
    /// </summary>
    public async Task RunRaceAsync(CancellationToken ct = default) {
        Task<string> primary = SlowFetchAsync("primary",800,ct);
        Task<string> fallback = SlowFetchAsync("fallback",400,ct);

        Task<string> winner = await Task.WhenAny(primary,fallback);
        string value = await winner;          // unwrap the winning task
        Debug.Log($"[UC11] Race winner: {value}");
    }

    private async Task<string> SlowFetchAsync(string name,
                                               int delayMs,
                                               CancellationToken ct) {
        await Task.Delay(delayMs,ct);
        return name;
    }

    // =========================================================
    // USE-CASE 12 — Cancellation with CancellationToken
    // =========================================================
    /// <summary>
    /// Pass a CancellationToken everywhere so callers can abort
    /// inflight operations cleanly (scene transitions, logouts,
    /// destroy). Link per-request tokens to _lifetimeCts for
    /// automatic cleanup on object destroy.
    /// </summary>
    public async Task CancellationDemoAsync() {
        using var cts = CancellationTokenSource
                            .CreateLinkedTokenSource(_lifetimeCts.Token);
        cts.CancelAfter(300);   // cancel this specific operation after 300 ms

        try {
            await Task.Delay(2000,cts.Token);
            Debug.Log("[UC12] This line won't be reached.");
        } catch (OperationCanceledException) {
            Debug.Log("[UC12] Task was cancelled cleanly — no crash.");
        }
    }

    // =========================================================
    // USE-CASE 13 — Timeout pattern
    // =========================================================
    /// <summary>
    /// Generic helper that races your task against a deadline.
    /// Throws TimeoutException if the deadline is exceeded.
    /// </summary>
    public async Task<T> WithTimeoutAsync<T>(Task<T> task,
                                              TimeSpan timeout,
                                              CancellationToken ct = default) {
        using var timeoutCts = CancellationTokenSource
                                    .CreateLinkedTokenSource(ct);
        Task delayTask = Task.Delay(timeout,timeoutCts.Token);

        Task completed = await Task.WhenAny(task,delayTask);
        if (completed == delayTask)
            throw new TimeoutException($"Operation exceeded {timeout.TotalSeconds:F1} s");

        timeoutCts.Cancel();       // stop the timer
        return await task;         // propagate any real exception
    }

    private async Task TimeoutDemoAsync(CancellationToken ct) {
        try {
            string r = await WithTimeoutAsync(
                SlowFetchAsync("data",2000,ct),
                TimeSpan.FromMilliseconds(500),
                ct);
            Debug.Log($"[UC13] Got: {r}");
        } catch (TimeoutException ex) {
            Debug.Log($"[UC13] Timeout caught: {ex.Message}");
        }
    }

    // =========================================================
    // USE-CASE 14 — Progress reporting (IProgress<T>)
    // =========================================================
    /// <summary>
    /// IProgress<T> marshals Report() calls back to the thread
    /// that created the Progress<T> object (usually main thread),
    /// so it's safe to update UI from inside Task.Run.
    /// </summary>
    public async Task ProgressDemoAsync(CancellationToken ct = default) {
        var progress = new Progress<float>(pct =>
            Debug.Log($"[UC14] Progress: {pct:P0}"));

        await Task.Run(() => {
            for (int i = 0;i <= 10;i++) {
                ct.ThrowIfCancellationRequested();
                ((IProgress<float>)progress).Report(i / 10f);
                Thread.Sleep(50);
            }
        },ct);

        Debug.Log("[UC14] Work with progress reporting done.");
    }

    // =========================================================
    // USE-CASE 15 — Retry with exponential back-off
    // =========================================================
    /// <summary>
    /// Retries a failable async operation up to maxRetries times
    /// with doubling delays. Essential for unreliable network ops.
    /// </summary>
    public async Task<T> RetryAsync<T>(Func<Task<T>> operation,
                                        int maxRetries,
                                        int initialDelayMs,
                                        CancellationToken ct = default) {
        int delay = initialDelayMs;
        for (int attempt = 1;attempt <= maxRetries;attempt++) {
            try {
                return await operation();
            } catch (Exception ex) when (attempt < maxRetries) {
                Debug.LogWarning($"[UC15] Attempt {attempt} failed: {ex.Message}. " +
                                 $"Retrying in {delay} ms …");
                await Task.Delay(delay,ct);
                delay *= 2;     // exponential back-off
            }
        }
        return await operation();   // final attempt — let it throw
    }

    // Simulates a flaky network call (fails first 2 times)
    private int _flakyCallCount;
    private async Task<string> FlakyNetworkCallAsync(CancellationToken ct) {
        await Task.Delay(100,ct);
        if (++_flakyCallCount < 3)
            throw new Exception("Simulated network error");
        return "success";
    }

    // =========================================================
    // USE-CASE 16 — Sequential async pipeline
    // =========================================================
    /// <summary>
    /// Chains async steps where each step's output feeds the
    /// next. Models a data-processing pipeline (fetch → parse
    /// → transform → save).
    /// </summary>
    public async Task SequentialPipelineAsync(CancellationToken ct = default) {
        string raw = await FetchRawDataAsync(ct);
        object parsed = await ParseAsync(raw,ct);
        object enriched = await EnrichAsync(parsed,ct);
        await SaveAsync(enriched,ct);
        Debug.Log("[UC16] Pipeline complete.");
    }

    private async Task<string> FetchRawDataAsync(CancellationToken ct) { await Task.Delay(50,ct); return "{\"value\":1}"; }
    private async Task<object> ParseAsync(string raw,CancellationToken ct) { await Task.Delay(50,ct); return new { Value = 1 }; }
    private async Task<object> EnrichAsync(object data,CancellationToken ct) { await Task.Delay(50,ct); return data; }
    private async Task SaveAsync(object data,CancellationToken ct) { await Task.Delay(50,ct); }

    // =========================================================
    // USE-CASE 17 — Async lazy initialisation guard
    // =========================================================
    /// <summary>
    /// Ensures one-time async setup even when called from
    /// multiple concurrent callers. The SemaphoreSlim prevents
    /// double-initialisation without blocking threads.
    /// </summary>
    private bool _isInitialised;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1,1);

    public async Task EnsureInitialisedAsync(CancellationToken ct = default) {
        if (_isInitialised)
            return;

        await _initLock.WaitAsync(ct);
        try {
            if (_isInitialised)
                return;         // double-check after lock

            await Task.Delay(200,ct);          // simulate async init work
            _isInitialised = true;
            Debug.Log("[UC17] One-time async init complete.");
        } finally {
            _initLock.Release();
        }
    }

    // =========================================================
    // USE-CASE 18 — Error handling
    // =========================================================
    /// <summary>
    /// try/catch/finally works normally in async methods.
    /// Exceptions bubble through awaited calls.
    /// AggregateException unwraps multiple parallel failures.
    /// </summary>
    public async Task ErrorHandlingDemoAsync(CancellationToken ct = default) {
        try {
            await ThrowingAsync(ct);
        } catch (InvalidOperationException ex) {
            Debug.Log($"[UC18] Caught specific exception: {ex.Message}");
        } catch (Exception ex) {
            Debug.LogError($"[UC18] Unexpected: {ex.Message}");
        } finally {
            Debug.Log("[UC18] Finally block always runs (cleanup here).");
        }
    }

    private async Task ThrowingAsync(CancellationToken ct) {
        await Task.Delay(50,ct);
        throw new InvalidOperationException("Simulated failure");
    }

    // =========================================================
    // USE-CASE 19 — Async event system
    // =========================================================
    /// <summary>
    /// Events whose handlers are async. Await all handlers in
    /// parallel so none blocks the others. Collect exceptions
    /// via AggregateException if needed.
    /// </summary>
    public async Task RaiseAsyncEvent(string payload) {
        if (OnAsyncEvent == null)
            return;

        var handlers = OnAsyncEvent.GetInvocationList();
        var tasks = new List<Task>(handlers.Length);

        foreach (var h in handlers)
            tasks.Add(((Func<string,Task>)h)(payload));

        await Task.WhenAll(tasks);
        Debug.Log($"[UC19] All async event handlers completed for: {payload}");
    }

    // =========================================================
    // USE-CASE 20 — UniTask (optional, Cysharp/UniTask package)
    // =========================================================
    // UniTask is a zero-allocation replacement for Task that is
    // Unity-aware (frame-based scheduling, PlayerLoop integration).
    //
    // Install:  Window → Package Manager → Add from git URL:
    //           https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
    //
    // Then replace:
    //   Task       → UniTask
    //   Task<T>    → UniTask<T>
    //   Task.Delay → UniTask.Delay
    //   Task.Yield → UniTask.Yield
    //   Task.Run   → UniTask.RunOnThreadPool
    //   Task.WhenAll → UniTask.WhenAll
    //
    // Example:
    // private async UniTask UniTaskExampleAsync(CancellationToken ct)
    // {
    //     await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: ct);
    //     await UniTask.WaitUntil(() => _isInitialised, cancellationToken: ct);
    //     Debug.Log("[UC20] UniTask example done.");
    // }
}
