using System.Collections.Generic;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Process-wide registry of "a UI panel currently wants the mouse" claims (FP build, Group B b2).
    ///
    /// The first-person rig locks and hides the cursor during gameplay and suspends mouselook while
    /// any claim is open. Panels do not touch <see cref="Cursor"/> themselves — there is exactly one
    /// writer (FirstPersonLook), so nothing can fight over the lock state.
    ///
    /// Claims are keyed by the claiming <see cref="Object"/>, so a panel can call
    /// <see cref="Set"/> unconditionally every frame (including from OnGUI, which runs several times
    /// per frame) — the call is idempotent. Destroyed claimants are pruned on read, so a panel that
    /// disappears without releasing can never strand the cursor in the free state.
    ///
    /// Lives in Game.Networking because most panels (inventory, pause, dream flow, consensus vote) do;
    /// Game.Presentation references Networking, so the camera rig can read it.
    /// </summary>
    public static class UiFocus
    {
        private static readonly HashSet<Object> _claims = new HashSet<Object>();

        // Domain reload is disabled — a claim left over from the previous play session would keep the
        // cursor free forever in the next one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _claims.Clear();

        /// <summary>True while at least one live panel wants the mouse.</summary>
        public static bool AnyOpen
        {
            get
            {
                // Unity's overloaded == treats a destroyed Object as null; RemoveWhere with that test
                // drops claims whose owner was destroyed without releasing.
                _claims.RemoveWhere(o => o == null);
                return _claims.Count > 0;
            }
        }

        /// <summary>Open or close <paramref name="owner"/>'s claim on the mouse. Idempotent.</summary>
        public static void Set(Object owner, bool open)
        {
            if (owner == null) return;
            if (open) _claims.Add(owner);
            else      _claims.Remove(owner);
        }

        /// <summary>Drop <paramref name="owner"/>'s claim. Call from OnDisable / OnNetworkDespawn.</summary>
        public static void Release(Object owner)
        {
            if (owner != null) _claims.Remove(owner);
        }
    }
}
