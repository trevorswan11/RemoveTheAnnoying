using System.Collections;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RemoveTheAnnoying.Patches;

// MIT License
//
// Copyright (c) 2023 tinyhoot
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

[HarmonyPatch(typeof(HUDManager))]
internal class CountShipLoot {
    private static GameObject _ship;
    private static VehicleController _cruiser;
    private static GameObject _totalCounter;
    private static TextMeshProUGUI _textMesh;
    private static float _displayTimeLeft;
    private static float _lastScanPostfixAt = -999f;

    private static ManualLogSource Log => RemoveTheAnnoyingBase.Log;
    private static float BaseDisplayTime => RemoveTheAnnoyingBase.Instance.ShipLootDisplayTime.Value;

    private const float MINIMUM_DISPLAY_TIME = 0.2f;

    [HarmonyPostfix]
    [HarmonyPatch("PingScan_performed")]
    public static void Postfix(ref InputAction.CallbackContext context) {
        if (GameNetworkManager.Instance.localPlayerController == null) return;
        if (!context.performed || (Time.time - _lastScanPostfixAt) < 0.25f) return;
        _lastScanPostfixAt = Time.time;

        if (BaseDisplayTime < MINIMUM_DISPLAY_TIME) {
            Log.LogDebug($"Configured display time below {MINIMUM_DISPLAY_TIME}s skips ShipLoot calculation, got {BaseDisplayTime}s");
            return;
        }

        // Only allow this special scan to work while inside the ship.
        if (!StartOfRound.Instance.inShipPhase && !GameNetworkManager.Instance.localPlayerController.isInHangarShipRoom)
            return;

        if (!_ship) _ship = GameObject.Find("/Environment/HangarShip");
        if (!_cruiser) _cruiser = StartOfRound.Instance.attachedVehicle;
        if (!_totalCounter) CopyValueCounter();

        float value = CalculateShipLoot();
        if (_cruiser && _cruiser.magnetedToShip)
            value += CalculateCruiserLoot();
        _textMesh.text = $"SCRAP: ${value:F0}";
        _displayTimeLeft = BaseDisplayTime;

        if (!_totalCounter.activeSelf) GameNetworkManager.Instance.StartCoroutine(ShipLootCoroutine());
    }

    private static IEnumerator ShipLootCoroutine() {
        _totalCounter.SetActive(true);
        while (_displayTimeLeft > 0f) {
            float time = _displayTimeLeft;
            _displayTimeLeft = 0f;
            yield return new WaitForSeconds(time);
        }
        _totalCounter.SetActive(false);
    }

    private static float CalculateShipLoot() => CalculateTargetLoot(_ship, "Ship");
    private static float CalculateCruiserLoot() => CalculateTargetLoot(_cruiser, "Cruiser");

    // Calculate the value of all scrap in the cruiser.
    // Drops items that are technically scrap but do not count towards quota from the calculation.
    private static float CalculateTargetLoot<T>(T target, string name) where T : Object {
        if (target == null) {
            Log.LogWarning($"{name} could not be located, something went wrong!");
            return 0f;
        }

        // Pattern match on the target to get the items
        System.Collections.Generic.IEnumerable<GrabbableObject> items;
        if (target is GameObject go) {
            items = go.GetComponentsInChildren<GrabbableObject>();
        } else if (target is Component comp) {
            items = comp.GetComponentsInChildren<GrabbableObject>();
        } else {
            return 0f;
        }

        var loot = items.Where(obj => obj.itemProperties.isScrap && obj is not RagdollGrabbableObject);
        Log.LogDebug($"Calculating total {name.ToLower()} scrap value.");
        loot.Do(scrap => Log.LogDebug($"{scrap.name} - ${scrap.scrapValue}"));

        return loot.Sum(scrap => scrap.scrapValue);
    }

    // Copy an existing object loaded by the game for the display of ship loot and put it in the right position.
    private static void CopyValueCounter() {
        GameObject valueCounter = GameObject.Find("/Systems/UI/Canvas/IngamePlayerHUD/BottomMiddle/ValueCounter");
        if (!valueCounter) Log.LogError("Failed to find ValueCounter object to copy!");
        _totalCounter = Object.Instantiate(valueCounter.gameObject, valueCounter.transform.parent, false);
        _totalCounter.transform.Translate(0f, 1f, 0f);
        Vector3 pos = _totalCounter.transform.localPosition;
        _totalCounter.transform.localPosition = new Vector3(pos.x + 50f, -50f, pos.z);
        _textMesh = _totalCounter.GetComponentInChildren<TextMeshProUGUI>();
    }
}
