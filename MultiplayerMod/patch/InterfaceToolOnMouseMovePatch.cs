using System;
using HarmonyLib;
using UnityEngine;

namespace MultiplayerMod.patch
{
    [HarmonyPatch(typeof(InterfaceTool), nameof(InterfaceTool.OnMouseMove))]
    public static class InterfaceToolOnMouseMovePatch
    {
        public static event Action<Pair<float, float>> OnMouseMove;

        public static void Prefix(Vector3 cursor_pos)
        {
            OnMouseMove?.Invoke(new Pair<float, float>(cursor_pos.x, cursor_pos.y));
        }
    }
}