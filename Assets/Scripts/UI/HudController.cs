using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal Milestone 1 HUD, built entirely in code (no prefab wiring):
/// crosshair dot, ammo counter, barrier percentage, center message line.
/// Finds the weapon and barrier at startup and subscribes to their events.
/// </summary>
public sealed class HudController : MonoBehaviour
{
    private Text ammoText;
    private Text barrierText;
    private Text messageText;

    private WeaponController weapon;
    private BarrierHealth barrier;

    private void Start()
    {
        weapon = FindFirstObjectByType<WeaponController>();
        barrier = FindFirstObjectByType<BarrierHealth>();

        BuildHud();

        if (weapon != null)
        {
            weapon.OnAmmoChanged += UpdateAmmo;
            weapon.OnReloadStateChanged += OnReloadChanged;
            UpdateAmmo(weapon.AmmoInMag, weapon.Reserve);
        }

        if (barrier != null)
        {
            barrier.OnChanged += UpdateBarrier;
            barrier.OnDestroyed += ShowGameOver;
            UpdateBarrier(barrier.Current, barrier.Max);
        }
    }

    private void UpdateAmmo(int inMag, int reserve)
    {
        if (weapon != null && weapon.IsReloading)
        {
            return;
        }
        ammoText.text = $"AMMO  {inMag} / {reserve}";
        ammoText.color = inMag == 0 ? new Color(1f, 0.35f, 0.3f) : Color.white;
    }

    private void OnReloadChanged(bool isReloading)
    {
        if (isReloading)
        {
            ammoText.text = "RELOADING...";
            ammoText.color = new Color(1f, 0.8f, 0.4f);
        }
        else
        {
            UpdateAmmo(weapon.AmmoInMag, weapon.Reserve);
        }
    }

    private void UpdateBarrier(float current, float max)
    {
        barrierText.text = $"BARRIER  {current:0}%";
        float danger = Mathf.InverseLerp(40f, 0f, current);
        barrierText.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f), danger);
    }

    private void ShowGameOver()
    {
        messageText.text = "THE LINE IS BROKEN\nGAME OVER";
    }

    // --- construction -----------------------------------------------------------

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject("HUD Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Crosshair dot
        GameObject crosshair = new GameObject("Crosshair");
        crosshair.transform.SetParent(canvasObject.transform, false);
        Image dot = crosshair.AddComponent<Image>();
        dot.color = new Color(1f, 1f, 1f, 0.85f);
        RectTransform dotRect = dot.rectTransform;
        dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.sizeDelta = new Vector2(5f, 5f);
        dotRect.anchoredPosition = Vector2.zero;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        barrierText = CreateText(canvasObject.transform, "Barrier Text", font, 26,
            new Vector2(0.5f, 1f), new Vector2(0f, -36f), TextAnchor.UpperCenter);

        ammoText = CreateText(canvasObject.transform, "Ammo Text", font, 26,
            new Vector2(1f, 0f), new Vector2(-130f, 40f), TextAnchor.LowerRight);

        messageText = CreateText(canvasObject.transform, "Message Text", font, 44,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), TextAnchor.MiddleCenter);
        messageText.color = new Color(1f, 0.25f, 0.2f);
        messageText.text = string.Empty;
    }

    private static Text CreateText(Transform parent, string name, Font font, int size,
        Vector2 anchor, Vector2 offset, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = new Vector2(420f, 60f);
        rect.anchoredPosition = offset;

        return text;
    }
}
