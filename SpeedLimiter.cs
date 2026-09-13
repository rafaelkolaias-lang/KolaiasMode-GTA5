using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;

public class SpeedLimiter : Script
{
    private bool enabled = false;
    private float limitKmh = 60f;
    private const float STEP = 5f;
    private const float MIN_LIMIT = 10f;
    private const float MAX_LIMIT = 300f;
    private const float MS_TO_KMH = 3.6f;
    private bool startupShown = false;

    private float origTractionCurveMin;
    private float origTractionCurveMax;
    private float origTractionLossMult;
    private float origBrakeForce;
    private float origSteeringLock;
    private bool handlingSaved = false;

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
        if (e.KeyCode == Keys.F11 && e.Modifiers == Keys.Control)
        {
            enabled = !enabled;
            if (enabled)
            {
                Vehicle veh = Game.Player.Character.CurrentVehicle;
                if (veh != null)
                {
                    float currentSpeed = veh.Speed * MS_TO_KMH;
                    if (currentSpeed > MIN_LIMIT)
                        limitKmh = (float)Math.Round(currentSpeed / STEP) * STEP;
                    ApplyDriftHandling(veh);
                }
                ShowNotification("~g~KolaiasMode ON\n~y~Speed: " + limitKmh + " km/h\n~b~Drift ON\n~w~PgUp/PgDn: adjust");
            }
            else
            {
                Vehicle veh = Game.Player.Character.CurrentVehicle;
                if (veh != null)
                    RestoreHandling(veh);
                handlingSaved = false;
                ShowNotification("~r~KolaiasMode OFF");
            }
        }

        // Page Up = increase limit
        if (enabled && e.KeyCode == Keys.PageUp)
        {
            if (limitKmh < MAX_LIMIT)
                limitKmh += STEP;
        }
        // Page Down = decrease limit
        if (enabled && e.KeyCode == Keys.PageDown)
        {
            if (limitKmh > MIN_LIMIT)
                limitKmh -= STEP;
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

    private void OnTick(object sender, EventArgs e)
    {
        if (Game.Player == null || Game.Player.Character == null)
            return;

        if (!startupShown && Game.Player.CanControlCharacter)
        {
            startupShown = true;
            ShowSubtitle("~y~KolaiasMode~w~ loaded! ~b~Ctrl+F11~w~ to toggle | ~g~PgUp/PgDn~w~ adjust speed", 8000);
        }

        Vehicle veh = Game.Player.Character.CurrentVehicle;
        if (veh == null)
            return;

        // Apply drift to each new vehicle entered while mode is on
        if (enabled && !handlingSaved)
            ApplyDriftHandling(veh);

        if (enabled)
        {
            float currentSpeedKmh = veh.Speed * MS_TO_KMH;

            // Shift = bypass speed limit + restore brake temporarily
            bool shiftHeld = Game.IsKeyPressed(Keys.ShiftKey);

            if (shiftHeld)
            {
                // Restore original brake while holding Shift
                if (handlingSaved)
                    veh.HandlingData.BrakeForce = origBrakeForce;
            }
            else
            {
                // Re-apply reduced brake
                if (handlingSaved)
                    veh.HandlingData.BrakeForce = origBrakeForce * 0.25f;

                // Disable throttle when over limit
                if (currentSpeedKmh > limitKmh)
                {
                    Function.Call((Hash)0xFE99B66D079CF6BC, 0, 71, true);
                }
            }

            DrawHUD(veh.Speed * MS_TO_KMH);
        }
    }

    private void DrawHUD(float currentSpeedKmh)
    {
        string text = string.Format("~y~LIMIT: {0:0} km/h ~w~| NOW: {1:0} km/h ~b~| DRIFT ON", limitKmh, currentSpeedKmh);

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
