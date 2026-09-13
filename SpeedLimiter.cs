using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;

public class SpeedLimiter : Script
{
    // GUI
    private bool guiOpen = false;
    private int selectedOption = 0;
    private const int TOTAL_OPTIONS = 2;

    // Drift mode
    private bool driftEnabled = false;
    private float limitKmh = 60f;
    private const float STEP = 5f;
    private const float MIN_LIMIT = 10f;
    private const float MAX_LIMIT = 300f;
    private const float MS_TO_KMH = 3.6f;

    private float origTractionCurveMin;
    private float origTractionCurveMax;
    private float origTractionLossMult;
    private float origBrakeForce;
    private float origSteeringLock;
    private bool handlingSaved = false;

    // Population
    private bool populationEnabled = false;
    private string lastDensityPeriod = "";

    private bool startupShown = false;

    public SpeedLimiter()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;
    }

    private void ShowNotification(string text)
    {
        Function.Call((Hash)0x202709F4C58A0424, "STRING");
        Function.Call((Hash)0x6C188BE134E074AA, text);
        Function.Call((Hash)0x2ED7843F8F801023, false, false);
    }

    private void ShowSubtitle(string text, int durationMs)
    {
        Function.Call((Hash)0xB87A37EEB7FAA67D, "STRING");
        Function.Call((Hash)0x6C188BE134E074AA, text);
        Function.Call((Hash)0x9D77056A530643F6, durationMs, true);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // F11 = toggle GUI
        if (e.KeyCode == Keys.F11 && e.Modifiers == Keys.None)
        {
            guiOpen = !guiOpen;
            return;
        }

        if (guiOpen)
        {
            // Navigate menu
            if (e.KeyCode == Keys.Up)
            {
                selectedOption--;
                if (selectedOption < 0) selectedOption = TOTAL_OPTIONS - 1;
            }
            else if (e.KeyCode == Keys.Down)
            {
                selectedOption++;
                if (selectedOption >= TOTAL_OPTIONS) selectedOption = 0;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ToggleOption(selectedOption);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                guiOpen = false;
            }
        }

        // Speed limit controls (work outside GUI)
        if (driftEnabled)
        {
            if (e.KeyCode == Keys.PageUp && limitKmh < MAX_LIMIT)
                limitKmh += STEP;
            if (e.KeyCode == Keys.PageDown && limitKmh > MIN_LIMIT)
                limitKmh -= STEP;
        }
    }

    private void ToggleOption(int option)
    {
        if (option == 0)
        {
            // Toggle Drift + Speed Limiter
            driftEnabled = !driftEnabled;
            if (driftEnabled)
            {
                Vehicle veh = Game.Player.Character.CurrentVehicle;
                if (veh != null)
                {
                    float currentSpeed = veh.Speed * MS_TO_KMH;
                    if (currentSpeed > MIN_LIMIT)
                        limitKmh = (float)Math.Round(currentSpeed / STEP) * STEP;
                    ApplyDriftHandling(veh);
                }
                ShowNotification("~g~Drift + Speed Limiter ON\n~y~" + limitKmh + " km/h\n~w~PgUp/PgDn: adjust | Shift: bypass");
            }
            else
            {
                Vehicle veh = Game.Player.Character.CurrentVehicle;
                if (veh != null)
                    RestoreHandling(veh);
                handlingSaved = false;
                ShowNotification("~r~Drift + Speed Limiter OFF");
            }
        }
        else if (option == 1)
        {
            // Toggle Population
            populationEnabled = !populationEnabled;
            lastDensityPeriod = "";
            if (populationEnabled)
                ShowNotification("~g~Population Density ON\n~w~Rush 200% | Night 50% | Normal 100%");
            else
                ShowNotification("~r~Population Density OFF");
        }
    }

    private void ApplyDriftHandling(Vehicle veh)
    {
        if (!handlingSaved)
        {
            var h = veh.HandlingData;
            origTractionCurveMin = h.TractionCurveMin;
            origTractionCurveMax = h.TractionCurveMax;
            origTractionLossMult = h.TractionLossMultiplier;
            origBrakeForce = h.BrakeForce;
            origSteeringLock = h.SteeringLock;
            handlingSaved = true;
        }

        var hd = veh.HandlingData;
        hd.TractionCurveMin = origTractionCurveMin * 0.65f;
        hd.TractionCurveMax = origTractionCurveMax * 0.65f;
        hd.TractionLossMultiplier = origTractionLossMult * 0.25f;
        hd.BrakeForce = origBrakeForce * 0.25f;
        hd.SteeringLock = origSteeringLock * 0.5f;
    }

    private void RestoreHandling(Vehicle veh)
    {
        if (handlingSaved)
        {
            var hd = veh.HandlingData;
            hd.TractionCurveMin = origTractionCurveMin;
            hd.TractionCurveMax = origTractionCurveMax;
            hd.TractionLossMultiplier = origTractionLossMult;
            hd.BrakeForce = origBrakeForce;
            hd.SteeringLock = origSteeringLock;
            handlingSaved = false;
        }
    }

    private void UpdateTrafficDensity()
    {
        int hour = Function.Call<int>((Hash)0x25223CA6B4D20B7F);

        string period;
        float multiplier;

        if ((hour >= 7 && hour < 10) || (hour >= 17 && hour < 20))
        {
            period = "rush";
            multiplier = 2.0f;
        }
        else if (hour >= 0 && hour < 6)
        {
            period = "night";
            multiplier = 0.5f;
        }
        else
        {
            period = "normal";
            multiplier = 1.0f;
        }

        if (period != lastDensityPeriod)
        {
            lastDensityPeriod = period;
            string label = period == "rush" ? "~o~RUSH HOUR 200%" :
                           period == "night" ? "~b~MADRUGADA 50%" :
                           "~w~NORMAL 100%";
            ShowSubtitle("~y~Traffic: " + label, 4000);
        }

        Function.Call((Hash)0x95E3D6257B166CF2, multiplier);
        Function.Call((Hash)0x245A6883D966D537, multiplier);
        Function.Call((Hash)0xB3B3359379FE77D3, multiplier);
        Function.Call((Hash)0xEAE6DCC7EEE3DB1D, multiplier);
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (Game.Player == null || Game.Player.Character == null)
            return;

        if (!startupShown && Game.Player.CanControlCharacter)
        {
            startupShown = true;
            ShowSubtitle("~y~KolaiasMode~w~ loaded! Press ~b~F11~w~ to open menu", 8000);
        }

        // Population density (always runs if enabled)
        if (populationEnabled && Game.Player.CanControlCharacter)
            UpdateTrafficDensity();

        // Draw GUI
        if (guiOpen)
            DrawGUI();

        // Drift logic
        Vehicle veh = Game.Player.Character.CurrentVehicle;
        if (veh == null)
            return;

        if (driftEnabled && !handlingSaved)
            ApplyDriftHandling(veh);

        if (driftEnabled)
        {
            float currentSpeedKmh = veh.Speed * MS_TO_KMH;
            bool shiftHeld = Game.IsKeyPressed(Keys.ShiftKey);

            if (shiftHeld)
            {
                if (handlingSaved)
                    veh.HandlingData.BrakeForce = origBrakeForce;
            }
            else
            {
                if (handlingSaved)
                    veh.HandlingData.BrakeForce = origBrakeForce * 0.25f;

                if (currentSpeedKmh > limitKmh)
                    Function.Call((Hash)0xFE99B66D079CF6BC, 0, 71, true);
            }

            DrawHUD(veh.Speed * MS_TO_KMH);
        }
    }

    private void DrawText(string text, float x, float y, float scale, int r, int g, int b, int a, bool center)
    {
        Function.Call((Hash)0x66E0276CC5F6B9DA, 4); // font
        Function.Call((Hash)0x07C837F9A01C34C9, 0.0f, scale); // scale
        Function.Call((Hash)0xBE6B23FFA53FB442, r, g, b, a); // colour
        Function.Call((Hash)0x2513DFB0FB8400FE); // outline
        if (center)
            Function.Call((Hash)0xC02F4DBFB51D988B, true); // centre
        Function.Call((Hash)0x25FBB336DF1804CB, "STRING"); // begin
        Function.Call((Hash)0x6C188BE134E074AA, text); // add text
        Function.Call((Hash)0xCD015E5BB0D96A57, x, y); // end
    }

    private void DrawRect(float x, float y, float w, float h, int r, int g, int b, int a)
    {
        // DRAW_RECT
        Function.Call((Hash)0x3A618A217E5154F0, x, y, w, h, r, g, b, a);
    }

    private void DrawGUI()
    {
        float boxX = 0.5f;
        float boxY = 0.38f;
        float boxW = 0.28f;
        float boxH = 0.28f;

        // Background
        DrawRect(boxX, boxY, boxW, boxH, 0, 0, 0, 200);

        // Title bar
        DrawRect(boxX, boxY - boxH / 2f + 0.025f, boxW, 0.05f, 200, 150, 0, 220);
        DrawText("~w~KOLAIAS MODE", boxX, boxY - boxH / 2f + 0.005f, 0.5f, 255, 255, 255, 255, true);

        // Option 0: Drift + Speed Limiter
        float opt0Y = boxY - 0.04f;
        bool sel0 = selectedOption == 0;
        if (sel0)
            DrawRect(boxX, opt0Y + 0.015f, boxW - 0.01f, 0.055f, 255, 200, 0, 80);

        string driftStatus = driftEnabled ? "~g~ON" : "~r~OFF";
        string driftInfo = driftEnabled ? "  ~y~Limit: " + limitKmh + " km/h" : "";
        DrawText((sel0 ? ">> " : "   ") + "Drift + Speed Limiter  " + driftStatus + driftInfo,
                 boxX - boxW / 2f + 0.015f, opt0Y, 0.35f, 255, 255, 255, 255, false);

        // Drift details
        float detY = opt0Y + 0.03f;
        DrawText("   ~c~Tracao 65% | Aderencia 25% | Freio 25% | Curva 50%",
                 boxX - boxW / 2f + 0.015f, detY, 0.25f, 180, 180, 180, 200, false);

        // Option 1: Population
        float opt1Y = boxY + 0.04f;
        bool sel1 = selectedOption == 1;
        if (sel1)
            DrawRect(boxX, opt1Y + 0.015f, boxW - 0.01f, 0.055f, 255, 200, 0, 80);

        string popStatus = populationEnabled ? "~g~ON" : "~r~OFF";
        DrawText((sel1 ? ">> " : "   ") + "Population Density  " + popStatus,
                 boxX - boxW / 2f + 0.015f, opt1Y, 0.35f, 255, 255, 255, 255, false);

        // Population details
        float detY2 = opt1Y + 0.03f;
        DrawText("   ~c~Rush 7-10h/17-20h: 200% | Night 0-6h: 50% | Normal: 100%",
                 boxX - boxW / 2f + 0.015f, detY2, 0.25f, 180, 180, 180, 200, false);

        // Footer
        float footY = boxY + boxH / 2f - 0.035f;
        DrawText("~y~Up/Down~w~ navigate  ~y~Enter~w~ toggle  ~y~F11/Esc~w~ close",
                 boxX, footY, 0.28f, 200, 200, 200, 200, true);
    }

    private void DrawHUD(float currentSpeedKmh)
    {
        string trafficLabel = "";
        if (populationEnabled)
        {
            trafficLabel = lastDensityPeriod == "rush" ? " ~o~RUSH" :
                           lastDensityPeriod == "night" ? " ~b~NIGHT" :
                           " ~w~NORMAL";
        }

        string text = string.Format("~y~LIMIT: {0:0} km/h ~w~| NOW: {1:0} km/h ~b~| DRIFT{2}",
                                    limitKmh, currentSpeedKmh, trafficLabel);

        Function.Call((Hash)0x66E0276CC5F6B9DA, 4);
        Function.Call((Hash)0x07C837F9A01C34C9, 0.0f, 0.4f);
        Function.Call((Hash)0xBE6B23FFA53FB442, 255, 255, 100, 230);
        Function.Call((Hash)0x2513DFB0FB8400FE);
        Function.Call((Hash)0xC02F4DBFB51D988B, true);
        Function.Call((Hash)0x25FBB336DF1804CB, "STRING");
        Function.Call((Hash)0x6C188BE134E074AA, text);
        Function.Call((Hash)0xCD015E5BB0D96A57, 0.5f, 0.02f);
    }
}
