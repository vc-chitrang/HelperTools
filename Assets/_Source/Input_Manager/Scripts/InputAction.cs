using System;
using UnityEngine;

namespace InputFramework
{
    /// <summary>
    /// A named, rebindable input action bound to one or two KeyCodes.
    /// Add to an <see cref="InputActionAsset"/> and assign it to <see cref="InputManager"/>
    /// to get polled automatically every frame.
    /// </summary>
    [Serializable]
    public class InputAction
    {
        [SerializeField] private string  _name;
        [SerializeField] private KeyCode _primaryKey;
        [SerializeField] private KeyCode _alternateKey = KeyCode.None;

        // Required for Unity serialization (ScriptableObject lists)
        public InputAction() { }

        public InputAction(string name, KeyCode primary, KeyCode alternate = KeyCode.None)
        {
            _name         = name;
            _primaryKey   = primary;
            _alternateKey = alternate;
        }

        public string  Name         => _name;
        public KeyCode PrimaryKey   { get => _primaryKey;   set => _primaryKey   = value; }
        public KeyCode AlternateKey { get => _alternateKey; set => _alternateKey = value; }

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired on the frame the action key is first pressed.</summary>
        public event Action OnStarted;

        /// <summary>Fired every frame the action key is held down.</summary>
        public event Action OnHeld;

        /// <summary>Fired on the frame the action key is released.</summary>
        public event Action OnCanceled;

        // ── Rebinding ──────────────────────────────────────────────────────────

        /// <summary>Rebind the primary (default) or alternate key at runtime.</summary>
        public void Rebind(KeyCode newKey, bool alternate = false)
        {
            if (alternate) _alternateKey = newKey;
            else           _primaryKey   = newKey;
        }

        // ── Polling helpers ────────────────────────────────────────────────────

        public bool IsDown =>
            Input.GetKey(_primaryKey) ||
            (_alternateKey != KeyCode.None && Input.GetKey(_alternateKey));

        public bool WasPressed =>
            Input.GetKeyDown(_primaryKey) ||
            (_alternateKey != KeyCode.None && Input.GetKeyDown(_alternateKey));

        public bool WasReleased =>
            Input.GetKeyUp(_primaryKey) ||
            (_alternateKey != KeyCode.None && Input.GetKeyUp(_alternateKey));

        // Called by InputManager.Update each frame
        internal void Poll()
        {
            if (WasPressed)  OnStarted?.Invoke();
            if (IsDown)      OnHeld?.Invoke();
            if (WasReleased) OnCanceled?.Invoke();
        }
    }
}
