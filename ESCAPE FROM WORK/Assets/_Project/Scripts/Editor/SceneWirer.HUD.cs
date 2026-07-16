using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// HUD Canvas builder for the one-click scene builder.
/// Creates health bar, ammo display, floor info, extraction warning,
/// interaction prompt, and the 3-column loot-container UI.
/// </summary>
public static partial class SceneWirer
{
    static void BuildHUD()
    {
        // Don't overwrite manually-adjusted layout once created
        if (GameObject.Find("HUDCanvas") != null) { Debug.Log("HUD exists — keeping manual layout"); return; }

        // Ensure EventSystem exists
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        var canvasGo = new GameObject("HUDCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var hud = canvasGo.AddComponent<EscapeFromWork.UI.HUDManager>();

        // ---- Panel helper ----
        RectTransform MakePanel(Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color bgColor)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var bg = go.AddComponent<Image>();
            bg.color = bgColor;
            bg.raycastTarget = false;
            return rt;
        }

        // ---- Text helper ----
        Text MakeText(string name, RectTransform parent, int fontSize, TextAnchor align, Color? c = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            t.fontSize = fontSize;
            t.color = c ?? Color.white;
            t.alignment = align;
            t.raycastTarget = false;
            var tr = go.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            tr.pivot = new Vector2(0.5f, 0.5f);
            return t;
        }

        // ---- Top-Left: Health (480x90) ----
        var hpRt = MakePanel(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(480, 90), new Color(0, 0, 0, 0.55f));
        var sliderGo = new GameObject("Slider", typeof(RectTransform)); sliderGo.transform.SetParent(hpRt, false);
        var sliderBg = sliderGo.AddComponent<Image>(); sliderBg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(sliderGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.9f, 0.2f, 0.2f);
        var fRt = fillGo.GetComponent<RectTransform>(); fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.sizeDelta = Vector2.zero;
        var slider = sliderGo.AddComponent<Slider>(); slider.fillRect = fRt; slider.targetGraphic = fillImg;
        var slRt = sliderGo.GetComponent<RectTransform>();
        slRt.anchorMin = new Vector2(0, 0.3f); slRt.anchorMax = new Vector2(1, 0.7f);
        slRt.offsetMin = new Vector2(12, 0); slRt.offsetMax = new Vector2(-12, 0);
        var healthTxt = MakeText("HealthText", hpRt, 24, TextAnchor.MiddleCenter);

        // ---- Top-Right: Floor (330x100) ----
        var fpRt = MakePanel(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(330, 100), new Color(0, 0, 0, 0.55f));
        var floorTxt = MakeText("FloorText", fpRt, 40, TextAnchor.MiddleCenter);
        var ftRt = floorTxt.GetComponent<RectTransform>();
        ftRt.anchorMin = new Vector2(0, 0.5f); ftRt.anchorMax = new Vector2(1, 1);
        ftRt.sizeDelta = Vector2.zero; ftRt.pivot = new Vector2(0.5f, 0.5f);
        var statusTxt = MakeText("StatusText", fpRt, 20, TextAnchor.MiddleCenter);
        var stRt = statusTxt.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0, 0); stRt.anchorMax = new Vector2(1, 0.5f);
        stRt.sizeDelta = Vector2.zero; stRt.pivot = new Vector2(0.5f, 0.5f);

        // ---- Top-Left below Health: Ammo (330x80) ----
        var apRt = MakePanel(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -140), new Vector2(330, 80), new Color(0, 0, 0, 0.55f));
        var ammoTxt = MakeText("AmmoText", apRt, 44, TextAnchor.MiddleCenter);
        ammoTxt.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.35f);
        ammoTxt.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
        ammoTxt.text = "15 / 15";
        var ammoTypeTxt = MakeText("AmmoTypeText", apRt, 22, TextAnchor.MiddleCenter);
        ammoTypeTxt.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
        ammoTypeTxt.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.35f);
        ammoTypeTxt.text = "Staple";

        // ---- Center: Extraction warning ----
        var extPanelRt = MakePanel(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 60), new Color(0, 0, 0, 0));
        extPanelRt.gameObject.SetActive(false);
        var extTxt = MakeText("ExtractionText", extPanelRt, 36, TextAnchor.MiddleCenter, Color.red);
        extTxt.text = "撤离!";

        // ---- Prompt ----
        var promptRt = MakePanel(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 85), new Vector2(400, 30), new Color(0, 0, 0, 0));
        var promptTxt = MakeText("PromptText", promptRt, 18, TextAnchor.MiddleCenter);
        promptTxt.text = "";

        // ---- Loot Container UI (3 equal columns: equip | backpack | container) ----
        float panelW = 1450, panelH = 820;
        var lcPanelRt = MakePanel(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelW, panelH), new Color(0.05f, 0.05f, 0.08f, 0.95f));
        lcPanelRt.gameObject.SetActive(false);

        var lcTitle = MakeText("LCTitle", lcPanelRt, 22, TextAnchor.UpperCenter);
        lcTitle.text = "搜刮中...";
        lcTitle.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.95f); lcTitle.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);

        var eqLabel = MakeText("EQLabel", lcPanelRt, 22, TextAnchor.UpperCenter); eqLabel.text = "装备";
        eqLabel.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.90f); eqLabel.GetComponent<RectTransform>().anchorMax = new Vector2(0.18f, 0.95f);
        var bpLabel = MakeText("BPLabel", lcPanelRt, 22, TextAnchor.UpperCenter); bpLabel.text = "背包";
        bpLabel.GetComponent<RectTransform>().anchorMin = new Vector2(0.19f, 0.90f); bpLabel.GetComponent<RectTransform>().anchorMax = new Vector2(0.60f, 0.95f);
        var cLabel = MakeText("CLabel", lcPanelRt, 22, TextAnchor.UpperCenter); cLabel.text = "容器";
        cLabel.GetComponent<RectTransform>().anchorMin = new Vector2(0.61f, 0.90f); cLabel.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.95f);

        var eqPanel = new GameObject("EquipPanel", typeof(RectTransform), typeof(Image));
        eqPanel.transform.SetParent(lcPanelRt, false);
        eqPanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.8f);
        eqPanel.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.06f);
        eqPanel.GetComponent<RectTransform>().anchorMax = new Vector2(0.18f, 0.89f);

        var bpGrid = new GameObject("BackpackGrid", typeof(RectTransform));
        bpGrid.transform.SetParent(lcPanelRt, false);
        bpGrid.GetComponent<RectTransform>().anchorMin = new Vector2(0.19f, 0.06f);
        bpGrid.GetComponent<RectTransform>().anchorMax = new Vector2(0.60f, 0.89f);

        var cGrid = new GameObject("ContainerGrid", typeof(RectTransform));
        cGrid.transform.SetParent(lcPanelRt, false);
        cGrid.GetComponent<RectTransform>().anchorMin = new Vector2(0.61f, 0.06f);
        cGrid.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.89f);

        // Drop zone
        var dropZone = new GameObject("DropZone", typeof(RectTransform), typeof(Image));
        dropZone.transform.SetParent(lcPanelRt, false);
        dropZone.GetComponent<Image>().color = new Color(0.8f, 0.1f, 0.1f, 0.7f);
        dropZone.GetComponent<RectTransform>().anchorMin = new Vector2(0.75f, 0.01f);
        dropZone.GetComponent<RectTransform>().anchorMax = new Vector2(0.98f, 0.05f);
        dropZone.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        var dzLabel = MakeText("DZLabel", dropZone.GetComponent<RectTransform>(), 14, TextAnchor.MiddleCenter, Color.white);
        dzLabel.text = "拖拽至此丢弃";
        dzLabel.raycastTarget = false;

        var lcUI = canvasGo.AddComponent<EscapeFromWork.UI.LootContainerUI>();
        var lcUISO = new SerializedObject(lcUI);
        lcUISO.FindProperty("panel").objectReferenceValue = lcPanelRt.gameObject;
        lcUISO.FindProperty("titleText").objectReferenceValue = lcTitle;
        lcUISO.FindProperty("equipParent").objectReferenceValue = eqPanel.transform;
        lcUISO.FindProperty("bpGridParent").objectReferenceValue = bpGrid.transform;
        lcUISO.FindProperty("bpLabel").objectReferenceValue = bpLabel;
        lcUISO.FindProperty("contGridParent").objectReferenceValue = cGrid.transform;
        lcUISO.FindProperty("contLabel").objectReferenceValue = cLabel;

        var infoTxt = MakeText("InfoText", lcPanelRt, 14, TextAnchor.UpperLeft);
        infoTxt.text = "";
        infoTxt.GetComponent<RectTransform>().anchorMin = new Vector2(0.02f, 0.01f);
        infoTxt.GetComponent<RectTransform>().anchorMax = new Vector2(0.25f, 0.05f);

        lcUISO.FindProperty("infoText").objectReferenceValue = infoTxt;
        lcUISO.FindProperty("cellSize").floatValue = 60f;
        lcUISO.ApplyModifiedProperties();

        // Wire HUDManager
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("healthBar").objectReferenceValue = slider;
        hudSO.FindProperty("healthText").objectReferenceValue = healthTxt;
        hudSO.FindProperty("ammoText").objectReferenceValue = ammoTxt;
        hudSO.FindProperty("ammoTypeText").objectReferenceValue = ammoTypeTxt;
        hudSO.FindProperty("floorNumberText").objectReferenceValue = floorTxt;
        hudSO.FindProperty("floorStatusText").objectReferenceValue = statusTxt;
        hudSO.FindProperty("extractionTimerText").objectReferenceValue = extTxt;
        hudSO.FindProperty("extractionWarning").objectReferenceValue = extPanelRt.gameObject;
        hudSO.FindProperty("interactionPrompt").objectReferenceValue = promptTxt;
        hudSO.ApplyModifiedProperties();

        Debug.Log("HUD Canvas created");
    }
}
