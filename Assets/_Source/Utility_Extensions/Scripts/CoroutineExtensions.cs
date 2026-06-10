using System;
using System.Collections;
using UnityEngine;

namespace HelperTools
{
    /// <summary>
    /// Coroutine helper extension methods for MonoBehaviour.
    /// All methods return the <see cref="Coroutine"/> so callers can StopCoroutine if needed.
    /// </summary>
    public static class CoroutineExtensions
    {
        // ── Delayed call ───────────────────────────────────────────────────

        /// <summary>Invokes <paramref name="action"/> after <paramref name="delay"/> seconds (scaled time).</summary>
        public static Coroutine DelayedCall(this MonoBehaviour mb, float delay, Action action) =>
            mb.StartCoroutine(DelayedRoutine(delay, action, unscaled: false));

        /// <summary>Invokes <paramref name="action"/> after <paramref name="delay"/> real seconds (unscaled time).</summary>
        public static Coroutine DelayedCallRealtime(this MonoBehaviour mb, float delay, Action action) =>
            mb.StartCoroutine(DelayedRoutine(delay, action, unscaled: true));

        // ── Repeated call ──────────────────────────────────────────────────

        /// <summary>
        /// Calls <paramref name="action"/> every <paramref name="interval"/> seconds.
        /// If <paramref name="times"/> is 0, repeats indefinitely.
        /// </summary>
        public static Coroutine RepeatCall(this MonoBehaviour mb, float interval, Action action, int times = 0) =>
            mb.StartCoroutine(RepeatRoutine(interval, action, times));

        // ── Execute over time ──────────────────────────────────────────────

        /// <summary>
        /// Runs <paramref name="callback"/> each frame for <paramref name="duration"/> seconds,
        /// passing normalized time t ∈ [0, 1].
        /// </summary>
        public static Coroutine ExecuteOverTime(this MonoBehaviour mb, float duration, Action<float> callback,
            Action onComplete = null) =>
            mb.StartCoroutine(OverTimeRoutine(duration, callback, onComplete));

        // ── Wait then call ─────────────────────────────────────────────────

        /// <summary>Waits for the given <see cref="YieldInstruction"/> then calls <paramref name="action"/>.</summary>
        public static Coroutine AfterYield(this MonoBehaviour mb, YieldInstruction yield, Action action) =>
            mb.StartCoroutine(AfterYieldRoutine(yield, action));

        /// <summary>Waits until the predicate is true, then calls <paramref name="action"/>.</summary>
        public static Coroutine WaitUntil(this MonoBehaviour mb, Func<bool> predicate, Action action) =>
            mb.StartCoroutine(WaitUntilRoutine(predicate, action));

        // ── Internal routines ──────────────────────────────────────────────

        private static IEnumerator DelayedRoutine(float delay, Action action, bool unscaled)
        {
            if (unscaled) yield return new WaitForSecondsRealtime(delay);
            else          yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        private static IEnumerator RepeatRoutine(float interval, Action action, int times)
        {
            var wait = new WaitForSeconds(interval);
            int count = 0;
            while (times == 0 || count < times)
            {
                yield return wait;
                action?.Invoke();
                count++;
            }
        }

        private static IEnumerator OverTimeRoutine(float duration, Action<float> callback, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                callback?.Invoke(elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            callback?.Invoke(1f);
            onComplete?.Invoke();
        }

        private static IEnumerator AfterYieldRoutine(YieldInstruction yield, Action action)
        {
            yield return yield;
            action?.Invoke();
        }

        private static IEnumerator WaitUntilRoutine(Func<bool> predicate, Action action)
        {
            yield return new UnityEngine.WaitUntil(predicate);
            action?.Invoke();
        }
    }
}
