using System.Collections.Generic;
using UnityEngine;

namespace InputFramework
{
    /// <summary>
    /// ScriptableObject that holds a list of named, rebindable <see cref="InputAction"/>s.
    /// Create via <c>Right-click → Create → HelperTools → Input Actions Asset</c>,
    /// then assign to <see cref="InputManager"/> in the Inspector.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InputActions",
        menuName  = "HelperTools/Input Actions Asset",
        order     = 50)]
    public class InputActionAsset : ScriptableObject
    {
        [SerializeField] private List<InputAction> _actions = new List<InputAction>();

        public IReadOnlyList<InputAction> Actions => _actions;

        // ── Lookup ─────────────────────────────────────────────────────────────

        /// <summary>Returns the action with the given name, or null if not found.</summary>
        public InputAction FindAction(string name)
        {
            foreach (var a in _actions)
                if (a != null && a.Name == name) return a;
            return null;
        }

        // ── Runtime mutation ───────────────────────────────────────────────────

        /// <summary>Adds an action at runtime (not persisted to asset).</summary>
        public void AddAction(InputAction action)
        {
            if (action != null && !_actions.Contains(action))
                _actions.Add(action);
        }

        /// <summary>Removes the first action with the given name at runtime.</summary>
        public void RemoveAction(string name)
        {
            for (int i = _actions.Count - 1; i >= 0; i--)
                if (_actions[i]?.Name == name) { _actions.RemoveAt(i); break; }
        }
    }
}
