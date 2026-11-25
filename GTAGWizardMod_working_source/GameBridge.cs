using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace GTAGWizard.Game
{
    /// <summary>
    /// The ONLY place that touches Gorilla Tag's own code, and it is read-only:
    /// it looks up the player's right-hand Transform so the staff can follow it.
    ///
    /// It uses runtime reflection instead of a compile-time reference to Assembly-CSharp.dll, so:
    ///   * the project builds without the game's assembly,
    ///   * if a game update renames/moves a member, the mod logs one warning and the staff simply
    ///     doesn't appear (nothing crashes, nothing else in the game is affected).
    ///
    /// Candidate members are tried in order. These names are NOT yet verified against your game version;
    /// the log line "GameBridge: using ..." tells us which one (if any) matched.
    /// </summary>
    internal static class GameBridge
    {
        // { type name, static member holding the singleton, instance member holding the hand Transform }
        private static readonly string[][] Candidates =
        {
            new[] { "GorillaTagger",              "Instance", "offlineVRRig.rightHandTransform" },
            new[] { "GorillaTagger",              "Instance", "rightHandTransform" },
            new[] { "GorillaLocomotion.GTPlayer", "Instance", "rightControllerTransform" },
            new[] { "GorillaLocomotion.Player",   "Instance", "rightControllerTransform" },
            new[] { "GorillaLocomotion.Player",   "Instance", "rightHandTransform" },
        };

        private const BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _resolved;
        private static bool _loggedFailure;
        private static float _nextResolveAttempt;
        private static MemberInfo _singletonMember;
        private static MemberInfo _handMember;

        /// <summary>Set once by the controller so this static class can log.</summary>
        public static ManualLogSource Log;

        /// <summary>Returns the right-hand Transform, or null if the game isn't ready / the member wasn't found.</summary>
        public static Transform GetRightHand()
        {
            if (!_resolved && !TryResolve()) return null;

            object singleton = ReadMember(_singletonMember, null);
            if (singleton == null) return null;

            // Unity objects can be "destroyed but not null"; the overloaded == handles that.
            UnityEngine.Object unityObject = singleton as UnityEngine.Object;
            if (unityObject != null && unityObject == null) return null;

            object handValue = ReadMemberPath(singleton, _handMember);
            Transform hand = handValue as Transform;
            return hand != null ? hand : null;
        }

        private static bool TryResolve()
        {
            // Retry at most every 5 seconds (the game's assemblies are loaded long before we need them).
            if (Time.unscaledTime < _nextResolveAttempt) return false;
            _nextResolveAttempt = Time.unscaledTime + 5f;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int c = 0; c < Candidates.Length; c++)
            {
                string[] candidate = Candidates[c];
                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type type = null;
                    try { type = assemblies[a].GetType(candidate[0], false); }
                    catch (Exception) { /* some dynamic assemblies don't support GetType */ }
                    if (type == null) continue;

                    MemberInfo singleton = FindMember(type, candidate[1], StaticFlags);
                    MemberInfo hand = FindMemberPath(type, candidate[2]);
                    if (singleton == null || hand == null) continue;

                    _singletonMember = singleton;
                    _handMember = hand;
                    _handPath = candidate[2];
                    _resolved = true;
                    if (Log != null)
                        Log.LogInfo("GameBridge: using " + candidate[0] + "." + candidate[1] + " -> ." + candidate[2]);
                    return true;
                }
            }

            if (!_loggedFailure && Log != null)
            {
                _loggedFailure = true;
                Log.LogWarning("GameBridge: could not find the player's right-hand Transform. " +
                               "The staff will not appear until GameBridge.Candidates is updated for this game version.");
            }
            return false;
        }

        private static MemberInfo FindMemberPath(Type type, string path)
        {
            // Resolve a dotted path such as offlineVRRig.rightHandTransform.
            // The first segment is on the singleton type; later segments are on the preceding member type.
            string[] parts = path.Split('.');
            Type current = type;
            MemberInfo last = null;
            for (int i = 0; i < parts.Length; i++)
            {
                last = FindMember(current, parts[i], InstanceFlags);
                if (last == null) return null;
                current = GetMemberType(last);
                if (current == null && i < parts.Length - 1) return null;
            }
            return last;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null) return field.FieldType;
            PropertyInfo property = member as PropertyInfo;
            return property != null ? property.PropertyType : null;
        }

        private static MemberInfo FindMember(Type type, string name, BindingFlags flags)
        {
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanRead) return property;
            return type.GetField(name, flags);
        }

        private static object ReadMemberPath(object target, MemberInfo finalMember)
        {
            // Re-resolve the path against the runtime object graph because intermediate objects can change.
            // The selected candidate is stored as a final MemberInfo; find its declaring-type chain by name.
            if (target == null || finalMember == null) return null;
            string path = finalMember.Name;
            // For a nested candidate, _handPath is populated during resolution.
            string[] parts = _handPath.Split('.');
            object current = target;
            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null) return null;
                Type type = current.GetType();
                MemberInfo member = FindMember(type, parts[i], InstanceFlags);
                if (member == null) return null;
                current = ReadMember(member, current);
            }
            return current;
        }

        private static string _handPath;

        private static object ReadMember(MemberInfo member, object target)
        {
            FieldInfo field = member as FieldInfo;
            if (field != null) return field.GetValue(target);
            PropertyInfo property = member as PropertyInfo;
            return property != null ? property.GetValue(target, null) : null;
        }
    }
}
