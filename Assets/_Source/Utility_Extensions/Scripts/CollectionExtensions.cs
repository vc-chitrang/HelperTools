using System;
using System.Collections.Generic;
using UnityEngine;

namespace HelperTools
{
    /// <summary>IList and IEnumerable extension methods.</summary>
    public static class CollectionExtensions
    {
        // ── Null / empty guards ────────────────────────────────────────────

        public static bool IsNullOrEmpty<T>(this IList<T> list) => list == null || list.Count == 0;

        // ── Random access ──────────────────────────────────────────────────

        /// <summary>Returns a random element. Throws if the list is empty.</summary>
        public static T RandomElement<T>(this IList<T> list)
        {
            if (list.IsNullOrEmpty()) throw new InvalidOperationException("Cannot pick from an empty list.");
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        /// <summary>Returns a random element, or <paramref name="defaultValue"/> if the list is empty.</summary>
        public static T RandomElementOrDefault<T>(this IList<T> list, T defaultValue = default)
        {
            if (list.IsNullOrEmpty()) return defaultValue;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        // ── Mutation ───────────────────────────────────────────────────────

        /// <summary>Fisher-Yates in-place shuffle using Unity's Random.</summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Swaps elements at indices <paramref name="a"/> and <paramref name="b"/>.</summary>
        public static void Swap<T>(this IList<T> list, int a, int b) =>
            (list[a], list[b]) = (list[b], list[a]);

        // ── Safe access ────────────────────────────────────────────────────

        /// <summary>Returns the element at <paramref name="index"/> clamped to the valid range.</summary>
        public static T GetClamped<T>(this IList<T> list, int index) =>
            list[Mathf.Clamp(index, 0, list.Count - 1)];

        /// <summary>Returns the element at <paramref name="index"/> wrapping around both ends.</summary>
        public static T GetWrapped<T>(this IList<T> list, int index) =>
            list[((index % list.Count) + list.Count) % list.Count];

        // ── Utility ────────────────────────────────────────────────────────

        /// <summary>Adds <paramref name="item"/> only if it is not already in the list.</summary>
        public static bool AddUnique<T>(this IList<T> list, T item)
        {
            if (list.Contains(item)) return false;
            list.Add(item);
            return true;
        }

        /// <summary>Removes the last element. Throws if empty.</summary>
        public static T Pop<T>(this IList<T> list)
        {
            if (list.IsNullOrEmpty()) throw new InvalidOperationException("Cannot pop from an empty list.");
            int last = list.Count - 1;
            T item = list[last];
            list.RemoveAt(last);
            return item;
        }

        /// <summary>Fills a pre-allocated list with <paramref name="count"/> items produced by <paramref name="factory"/>.</summary>
        public static void Fill<T>(this IList<T> list, int count, Func<int, T> factory)
        {
            for (int i = 0; i < count; i++)
                list.Add(factory(i));
        }

        // ── Dictionary helpers ─────────────────────────────────────────────

        /// <summary>Returns the value for <paramref name="key"/>, adding it via <paramref name="factory"/> if absent.</summary>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict,
            TKey key, Func<TValue> factory)
        {
            if (!dict.TryGetValue(key, out var value))
            {
                value = factory();
                dict[key] = value;
            }
            return value;
        }
    }
}
