// SPDX-License-Identifier: MIT
using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GhostSummonTravel {
 internal static class SummonDismissButton {
  const string Label = "해골 소환 해제";
  static Button button;
  static InventoryGui inventory;
  static float nextRefresh;

  internal static void Clear() {
   if (button)
    UnityEngine.Object.Destroy(button.gameObject);
   button = null;
   inventory = null;
   nextRefresh = 0;
  }

  static void Refresh(InventoryGui gui) {
   if (!InventoryGui.IsVisible()) {
    if (button && button.gameObject.activeSelf)
     button.gameObject.SetActive(false);
    nextRefresh = 0;
    return;
   }
   if (Time.unscaledTime < nextRefresh)
    return;
   nextRefresh = Time.unscaledTime + 0.25f;
   bool show = SummonDismissal.HasSummons(Player.m_localPlayer);
   if (show && !button)
    Create(gui);
   if (button && button.gameObject.activeSelf != show)
    button.gameObject.SetActive(show);
  }

  static void Create(InventoryGui gui) {
   if (!gui.m_player || !gui.m_craftButton)
    return;
   var clone = UnityEngine.Object.Instantiate(gui.m_craftButton.gameObject, gui.m_player, false);
   clone.SetActive(false);
   clone.name = "GhostDismissSkeletons";
   var control = clone.GetComponent<Button>();
   var label = clone.GetComponentInChildren<TMP_Text>(true);
   var rect = clone.transform as RectTransform;
   if (!control || !label || !rect) {
    UnityEngine.Object.Destroy(clone);
    return;
   }
   foreach (var localizer in clone.GetComponentsInChildren<Localize>(true))
    localizer.enabled = false;
   foreach (var tooltip in clone.GetComponentsInChildren<UITooltip>(true)) {
    tooltip.enabled = false;
    UnityEngine.Object.Destroy(tooltip);
   }
   foreach (var trigger in clone.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)) {
    trigger.enabled = false;
    UnityEngine.Object.Destroy(trigger);
   }
   control.onClick = new Button.ButtonClickedEvent();
   control.onClick.AddListener(Click);
   control.interactable = true;
   label.text = Label;
   rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
   rect.pivot = new Vector2(0f, 0f);
   rect.anchoredPosition = new Vector2(0f, 8f);
   rect.sizeDelta = new Vector2(190f, 34f);
   inventory = gui;
   button = control;
  }

  static void Click() {
   try {
    if (!InventoryGui.IsVisible())
     return;
    var player = Player.m_localPlayer;
    int count = SummonDismissal.Dismiss(player);
    if (count > 0)
     player.Message(MessageHud.MessageType.Center, $"해골 {count}마리 소환 해제");
    if (inventory)
     nextRefresh = 0;
   } catch (Exception error) {
    Plugin.LogFault(error);
   }
  }

  [HarmonyPatch(typeof(InventoryGui), "Update")]
  static class InventoryUpdate {
   [HarmonyPostfix]
   static void Postfix(InventoryGui __instance) {
    try {
     Refresh(__instance);
    } catch (Exception error) {
     Plugin.LogFault(error);
    }
   }
  }

  [HarmonyPatch(typeof(InventoryGui), "OnDestroy")]
  static class InventoryDestroy {
   [HarmonyPrefix]
   static void Prefix(InventoryGui __instance) {
    if (inventory == __instance)
     Clear();
   }
  }
 }
}
