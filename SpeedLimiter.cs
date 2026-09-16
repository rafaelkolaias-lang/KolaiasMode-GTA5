using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;

public class SpeedLimiter : Script
{
    // GUI
    private bool guiOpen = false;
    private int selectedOption = 0;
    private const int TOTAL_OPTIONS = 3;

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
    private int savedVehicleHandle = -1;

    // Population
    private bool populationEnabled = false;
    private string lastDensityPeriod = "";

    // Dynamic weather
    private bool weatherEnabled = false;
    private DateTime lastWeatherChange = DateTime.MinValue;
    private int weatherIndex = 0;
    private string[] weathers = new string[] {
        "EXTRASUNNY", "CLEAR", "CLOUDS", "OVERCAST",
        "RAIN", "THUNDER", "CLEARING", "CLEAR",
        "FOGGY", "CLOUDS", "RAIN", "SMOG",
        "EXTRASUNNY", "CLEAR", "OVERCAST", "THUNDER"
    };

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
                ShowNotification("~g~Drift + Limitador de Velocidade LIGADO\n~y~" + limitKmh + " km/h\n~w~PgUp/PgDn: ajustar | Shift: ignorar limite");
            }
            else
            {
                Vehicle veh = Game.Player.Character.CurrentVehicle;
                if (veh != null)
                    RestoreHandling(veh);
                handlingSaved = false;
                savedVehicleHandle = -1;
                ShowNotification("~r~Drift + Limitador de Velocidade DESLIGADO");
            }
        }
        else if (option == 1)
        {
            // Toggle Population
            populationEnabled = !populationEnabled;
            lastDensityPeriod = "";
            if (populationEnabled)
                ShowNotification("~g~Densidade Populacional LIGADO\n~w~Hora Pico 200% | Madrugada 50% | Normal 100%");
            else
                ShowNotification("~r~Densidade Populacional DESLIGADO");
        }
        else if (option == 2)
        {
            // Toggle Dynamic Weather
            weatherEnabled = !weatherEnabled;
            lastWeatherChange = DateTime.MinValue;
            if (weatherEnabled)
                ShowNotification("~g~Clima Dinamico LIGADO\n~w~Clima muda a cada 5-10 minutos");
            else
                ShowNotification("~r~Clima Dinamico DESLIGADO");
        }
    }

    private float GetWeightFactor(Vehicle veh)
    {
        // Veiculos mais pesados recebem menos reducao de tracao
        // Leve (<1200kg): fator 0.0  |  Medio (~1600kg): ~0.5  |  Pesado (>2000kg): 1.0
        float mass = veh.HandlingData.Mass;
        float factor = (mass - 1200f) / 800f;
        if (factor < 0f) factor = 0f;
        if (factor > 1f) factor = 1f;
        return factor;
    }

    private void ApplyDriftHandling(Vehicle veh)
    {
        int handle = veh.Handle;
        if (!handlingSaved || savedVehicleHandle != handle)
        {
            // First restore old vehicle if switching
            if (handlingSaved && savedVehicleHandle != handle)
                RestoreHandlingDirect(veh);

            // Save fresh originals from this vehicle
            var h = veh.HandlingData;
            origTractionCurveMin = h.TractionCurveMin;
            origTractionCurveMax = h.TractionCurveMax;
            origTractionLossMult = h.TractionLossMultiplier;
            origBrakeForce = h.BrakeForce;
            origSteeringLock = h.SteeringLock;

            handlingSaved = true;
            savedVehicleHandle = handle;
        }

        // Escala por peso: pesados mantem mais tracao pra nao derrapar em morro
        // Freio e curva sao fixos pra todos os veiculos
        float w = GetWeightFactor(veh);
        float tractionMult = 0.65f + w * 0.25f;
        float lossMult = 0.25f + w * 0.35f;

        var hd = veh.HandlingData;
        hd.TractionCurveMin = origTractionCurveMin * tractionMult;
        hd.TractionCurveMax = origTractionCurveMax * tractionMult;
        hd.TractionLossMultiplier = origTractionLossMult * lossMult;
        hd.BrakeForce = origBrakeForce * 0.25f;
        hd.SteeringLock = origSteeringLock * 0.5f;
    }

    private void RestoreHandlingDirect(Vehicle veh)
    {
        var hd = veh.HandlingData;
        hd.TractionCurveMin = origTractionCurveMin;
        hd.TractionCurveMax = origTractionCurveMax;
        hd.TractionLossMultiplier = origTractionLossMult;
        hd.BrakeForce = origBrakeForce;
        hd.SteeringLock = origSteeringLock;
    }

    private void RestoreHandling(Vehicle veh)
    {
        if (handlingSaved)
        {
            RestoreHandlingDirect(veh);
            handlingSaved = false;
            savedVehicleHandle = -1;
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
            string label = period == "rush" ? "~o~HORA PICO 200%" :
                           period == "night" ? "~b~MADRUGADA 50%" :
                           "~w~NORMAL 100%";
            ShowSubtitle("~y~Transito: " + label, 4000);
        }

        Function.Call((Hash)0x95E3D6257B166CF2, multiplier);
        Function.Call((Hash)0x245A6883D966D537, multiplier);
        Function.Call((Hash)0xB3B3359379FE77D3, multiplier);
        Function.Call((Hash)0xEAE6DCC7EEE3DB1D, multiplier);
    }

    private void UpdateWeather()
    {
        // Transition 2-5 min + stay 10 min = total 12-15 min per weather
        int transitionSec = 120 + (weatherIndex * 43) % 180; // 120-300 sec (2-5 min)
        int totalInterval = transitionSec + 600; // + 10 min stay
        if ((DateTime.Now - lastWeatherChange).TotalSeconds > totalInterval)
        {
            lastWeatherChange = DateTime.Now;
            string weather = weathers[weatherIndex % weathers.Length];
            weatherIndex++;

            // SET_WEATHER_TYPE_OVERTIME_PERSIST - gradual transition
            Function.Call((Hash)0xFB5045B7C42B75BF, weather, (float)transitionSec);

            ShowSubtitle("~y~Clima: ~w~" + weather, 3000);
        }
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (Game.Player == null || Game.Player.Character == null)
            return;

        if (!startupShown && Game.Player.CanControlCharacter)
        {
            startupShown = true;
            ShowSubtitle("~y~KolaiasMode~w~ carregado! Aperte ~b~F11~w~ para abrir o menu", 8000);
        }

        // Population density (always runs if enabled)
        if (populationEnabled && Game.Player.CanControlCharacter)
            UpdateTrafficDensity();

        // Dynamic weather
        if (weatherEnabled && Game.Player.CanControlCharacter)
            UpdateWeather();

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
                // Re-apply every frame to prevent game from resetting values
                if (handlingSaved)
                {
                    float w = GetWeightFactor(veh);
                    float tractionMult = 0.65f + w * 0.25f;
                    float lossMult = 0.25f + w * 0.35f;

                    var hd = veh.HandlingData;
                    hd.TractionCurveMin = origTractionCurveMin * tractionMult;
                    hd.TractionCurveMax = origTractionCurveMax * tractionMult;
                    hd.TractionLossMultiplier = origTractionLossMult * lossMult;
                    hd.BrakeForce = origBrakeForce * 0.25f;
                    hd.SteeringLock = origSteeringLock * 0.5f;
                }

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
        float boxH = 0.36f;

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

        string driftStatus = driftEnabled ? "~g~LIGADO" : "~r~DESLIGADO";
        string driftInfo = driftEnabled ? "  ~y~Limite: " + limitKmh + " km/h" : "";
        DrawText((sel0 ? ">> " : "   ") + "Drift + Limitador  " + driftStatus + driftInfo,
                 boxX - boxW / 2f + 0.015f, opt0Y, 0.35f, 255, 255, 255, 255, false);

        // Drift details
        float detY = opt0Y + 0.03f;
        DrawText("   ~c~Drift adaptativo por peso (leves derrapam mais, pesados sobem morro)",
                 boxX - boxW / 2f + 0.015f, detY, 0.25f, 180, 180, 180, 200, false);

        // Option 1: Population
        float opt1Y = boxY + 0.04f;
        bool sel1 = selectedOption == 1;
        if (sel1)
            DrawRect(boxX, opt1Y + 0.015f, boxW - 0.01f, 0.055f, 255, 200, 0, 80);

        string popStatus = populationEnabled ? "~g~LIGADO" : "~r~DESLIGADO";
        DrawText((sel1 ? ">> " : "   ") + "Densidade Populacional  " + popStatus,
                 boxX - boxW / 2f + 0.015f, opt1Y, 0.35f, 255, 255, 255, 255, false);

        // Population details
        float detY2 = opt1Y + 0.03f;
        DrawText("   ~c~Pico 7-10h/17-20h: 200% | Madrugada 0-6h: 50% | Normal: 100%",
                 boxX - boxW / 2f + 0.015f, detY2, 0.25f, 180, 180, 180, 200, false);

        // Option 2: Dynamic Weather
        float opt2Y = boxY + 0.1f;
        bool sel2 = selectedOption == 2;
        if (sel2)
            DrawRect(boxX, opt2Y + 0.015f, boxW - 0.01f, 0.055f, 255, 200, 0, 80);

        string weatherStatus = weatherEnabled ? "~g~LIGADO" : "~r~DESLIGADO";
        DrawText((sel2 ? ">> " : "   ") + "Clima Dinamico  " + weatherStatus,
                 boxX - boxW / 2f + 0.015f, opt2Y, 0.35f, 255, 255, 255, 255, false);

        float detY3 = opt2Y + 0.03f;
        DrawText("   ~c~Clima muda a cada 5-10 min (sol, chuva, neblina, tempestade...)",
                 boxX - boxW / 2f + 0.015f, detY3, 0.25f, 180, 180, 180, 200, false);

        // Footer
        float footY = boxY + boxH / 2f - 0.035f;
        DrawText("~y~Cima/Baixo~w~ navegar  ~y~Enter~w~ alternar  ~y~F11/Esc~w~ fechar",
                 boxX, footY, 0.28f, 200, 200, 200, 200, true);
    }

    private void DrawHUD(float currentSpeedKmh)
    {
        string trafficLabel = "";
        if (populationEnabled)
        {
            trafficLabel = lastDensityPeriod == "rush" ? " ~o~PICO" :
                           lastDensityPeriod == "night" ? " ~b~MADRUGADA" :
                           " ~w~NORMAL";
        }

        string text = string.Format("~y~LIMITE: {0:0} km/h ~w~| ATUAL: {1:0} km/h ~b~| DRIFT{2}",
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
