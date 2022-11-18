using System;
using System.Linq;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        // ------- Default configs --------

        string myName = "VT";
        List<double> Aggressivity = new List<double> { 0.1, 1, 4 };
        float ErrorMargin = 6f;
        double lowThrustCutOn = 0.5;
        double lowThrustCutOff = 0.01;
        double lowThrustCutCruiseOn = 1;
        double lowThrustCutCruiseOff = 0.15;

        double velprecisionmode = 1;

        List<double> Accelerations = new List<double> { 15, 50, 100 };
        int gear = 0;
        double gearaccel = 0;

        bool TurnOffThrustersOnPark = true;
        bool RechargeOnPark = true;
        string BackupSubstring = "Backup";
        bool PerformanceWhilePark = false;
        bool AutoAddGridConnectors = false;
        bool AutoAddGridLandingGears = false;
        bool forceparkifstatic = true;
        bool allowpark = false; //Dangerous setting it as config
        List<double> thrdirmultiplier = new List<double> { -1, -1, 0 };
        bool thrdiroverride = false;

        readonly double thrustermodifier = 0.0000000001;
        string[] tagSurround = new string[] { "|", "|" };
        bool cruisePlane = false; // make cruise mode act more like an airplane
        int FramesBetweenActions = 1;
        bool ShowMetrics = false;
        int SkipFrames = 0;
        int framesperprint = 10;
        bool stockvalues = true;
        bool onlyMainCockpit = false;

        // ------- End default configs ---------

        // START CONFIG STRINGS AND VARS
        readonly MyIni config = new MyIni();
        readonly MyIni configCD = new MyIni();

        const string inistr = "--------Vector Thrust 2 Settings--------";

        const string detectstr = "--Rotor Calibration Settings--";
        const string accelstr = "--Acceleration Settings--";
        const string parkstr = "--Parking Settings--";
        const string advancedstr = "--Advanced Settings--";
        const string whipahstr = "--Whip's Artificial Horizon Redux Settings--";
        const string configstr = "--Config Settings--";

        const string myNameStr = "Name Tag";
        const string TagSurroundStr = "Tag Surround Char(s)";

        const string AggressivityStr = "(Min/Thrust_Off/Max) Rotor Correction Aggressivity Level";
        const string ErrorMarginStr = "Margin No. For Error Correction";
        const string lowThrustCutStr = "Velocity To Turn On/Off VectorThrusters";
        const string lowThrustCutCruiseStr = "Velocity To Turn On/Off VectorThrusters In Cruise";
        const string velprecisionmodestr = "Velocity To Trigger Presision Mode";

        const string AccelerationsStr = "Accelerations (Total Thrust %)";

        const string TurnOffThrustersOnParkStr = "Turn Off Thrusters On Park";
        const string RechargeOnParkStr = "Set Batteries/Tanks to Recharge/Stockpile On Park";
        const string BackupSubstringStr = "Assign Backup Batteries With Surname";
        const string PerformanceWhileParkStr = "Run Script Each 100 Frames When Parked";
        const string AutoAddGridConnectorsStr = "Add Automatically Same Grid Connectors";
        const string AutoAddGridLandingGearsStr = "Add Automatically Same Grid Landing Gears";
        const string forceparkifstaticStr = "Activate Park Mode If Parked In Static Grid or Ground";
        const string thrdirmultiplierStr = "Direction Of Thrusters On Park (Vector X,Y,Z)";
        const string thrdiroverrideStr = "Override Adjustment of Direction Of Thrusters in Gravity";

        const string onlyMainCockpitStr = "Gather Data Only From Main Cockpit";
        const string cruisePlaneStr = "Cruise Mode Act Like Plane";
        const string FramesBetweenActionsStr = "Frames Per Operation: Task Splitter";
        const string ShowMetricsStr = "Show Metrics";
        const string SkipFramesStr = "Skip Frames";
        const string framesperprintStr = "Frames Where The Script Won't Print";
        const string stockvaluesStr = "Set Park Blocks/Normal Thrusters to Default (Recompile)";

        const string OrientationColorStr = "Lines In The Sides"; // Lines in the sides color
        const string RetrogradeColorStr = "Reverse Velocity and Backward Arrow Indicator";  //Reverse Reticule Color And Arrow
        const string SkyColorStr = "Sky Color (Gravity)"; // Color of sky over the horizon line
        const string HorizonLineColorStr = "Horizon Line (Default: None)"; // Horizon line, defaults to transparent
        const string ElevationLineColorStr = "Elevation Lines (Gravity)"; // Elevations line, can be more white, but I'll left it like it is
        const string OfflinecolorStr = "DAMP, CRUISE, PARK Off Indicator"; //Gray color of the text box of damp, cruise, etc
        const string OnlinecolorStr = "DAMP, CRUISE, PARK On Indicator"; //Red color of the text box of damp, cruise, etc
        const string ForwardArrowColorStr = "Forward Arrow Color"; // Green Color of the arrow if it is pointing to the front
        const string TextBoxBackgroundStr = "DAMP, CRUISE, PARK Background"; // Background of textbox of damp, cruise, etc 
        const string ReticuleSensStr = "Velocity Indicator Sensitivity"; // Reticule sensitivity, the more the value, the more sens will have the velocity reticule
        const string DampreticuleSensStr = "Dampeners Arrow Trigger Multiplier"; // How far it needs to be from the center to trigger arrow mode while in dampeners

        const string controlModuleStr = "Allow Control Module Mod";
        // END STRINGS AND VARS


        void Config()
        {
            if (Me.CustomData.Equals(savedconfig) && savedconfig.Length > 0) return;
            if (!justCompiled) log.AppendNR("\n  >Configuration Edit Detected\n");

            config.Clear();
            KeepConfig();

            if (config.TryParse(Me.CustomData))
            {
                myName = config.Get(inistr, myNameStr).ToString(myName);
                if (string.IsNullOrEmpty(myName))
                {
                    myName = "VT";
                }
                textSurfaceKeyword = $"{myName}:";
                LCDName = $"{myName}LCD";

                string sstr = config.Get(inistr, TagSurroundStr).ToString();
                int sstrl = sstr.Length;

                if (sstrl == 1)
                {
                    tagSurround = new string[] { sstr, sstr };
                }
                else if (sstrl > 1 && sstrl % 2 == 0)
                {
                    string first = sstr.Substring(0, (int)(sstr.Length / 2));
                    string last = sstr.Substring((int)(sstr.Length / 2), (int)(sstr.Length / 2));
                    tagSurround = new string[] { first, last };
                }
                else
                {
                    tagSurround = new string[] { "|", "|" };
                }

                ManageTag(); // Just after checking

                config.GetList<double>(ref Aggressivity, detectstr, AggressivityStr, () => Aggressivity.Count == 3);

                ErrorMargin = config.Get(detectstr, ErrorMarginStr).ToSingle(ErrorMargin);
                velprecisionmode = config.Get(detectstr, velprecisionmodestr).ToDouble(velprecisionmode);

                List<double> temp1 = new List<double>();
                config.GetList<double>(ref temp1, detectstr, lowThrustCutStr, () => temp1.Count == 2);
                lowThrustCutOn = temp1[0];
                lowThrustCutOff = temp1[1];

                temp1 = new List<double>();
                config.GetList<double>(ref temp1, detectstr, lowThrustCutCruiseStr, () => temp1.Count == 2);
                lowThrustCutCruiseOn = temp1[0];
                lowThrustCutCruiseOff = temp1[1];

                config.GetList<double>(ref Accelerations, accelstr, AccelerationsStr, () => Accelerations.Count > 1 && Accelerations.All(x => x > 0));

                TurnOffThrustersOnPark = config.Get(parkstr, TurnOffThrustersOnParkStr).ToBoolean(TurnOffThrustersOnPark);
                RechargeOnPark = config.Get(parkstr, RechargeOnParkStr).ToBoolean(RechargeOnPark);
                BackupSubstring = config.Get(parkstr, BackupSubstringStr).ToString(BackupSubstring);
                AutoAddGridConnectors = config.Get(parkstr, AutoAddGridConnectorsStr).ToBoolean(AutoAddGridConnectors);
                AutoAddGridLandingGears = config.Get(parkstr, AutoAddGridLandingGearsStr).ToBoolean(AutoAddGridLandingGears);
                forceparkifstatic = config.Get(parkstr, forceparkifstaticStr).ToBoolean(forceparkifstatic);
                PerformanceWhilePark = config.Get(parkstr, PerformanceWhileParkStr).ToBoolean(PerformanceWhilePark);

                config.GetList<double>(ref thrdirmultiplier, parkstr, thrdirmultiplierStr, () => thrdirmultiplier.Count == 3);

                thrdiroverride = config.Get(parkstr, thrdiroverrideStr).ToBoolean(thrdiroverride);

                WH.OrientationColor = GetColor(whipahstr, OrientationColorStr, config, WH.OrientationColor);
                WH.RetrogradeColor = GetColor(whipahstr, RetrogradeColorStr, config, WH.RetrogradeColor);
                WH.SkyColor = GetColor(whipahstr, SkyColorStr, config, WH.SkyColor);
                WH.HorizonLineColor = GetColor(whipahstr, HorizonLineColorStr, config, WH.HorizonLineColor);
                WH.ElevationLineColor = GetColor(whipahstr, ElevationLineColorStr, config, WH.ElevationLineColor);
                WH.ForwardArrowColor = GetColor(whipahstr, ForwardArrowColorStr, config, WH.ForwardArrowColor);
                WH.Onlinecolor = GetColor(whipahstr, OnlinecolorStr, config, WH.Onlinecolor);
                WH.Offlinecolor = GetColor(whipahstr, OfflinecolorStr, config, WH.Offlinecolor);
                WH.TextBoxBackground = GetColor(whipahstr, TextBoxBackgroundStr, config, WH.TextBoxBackground);
                WH.ReticuleSens = config.Get(whipahstr, ReticuleSensStr).ToSingle(WH.ReticuleSens);
                WH.DampreticuleSens = config.Get(whipahstr, DampreticuleSensStr).ToSingle(WH.DampreticuleSens);

                onlyMainCockpit = config.Get(advancedstr, onlyMainCockpitStr).ToBoolean(onlyMainCockpit);
                cruisePlane = config.Get(advancedstr, cruisePlaneStr).ToBoolean(cruisePlane);
                FramesBetweenActions = config.Get(advancedstr, FramesBetweenActionsStr).ToInt32(FramesBetweenActions);
                if (FramesBetweenActions <= 0)
                {
                    FramesBetweenActions = 1;
                }
                timepause = FramesBetweenActions * timeperframe;

                framesperprint = config.Get(advancedstr, framesperprintStr).ToInt32(framesperprint);

                SkipFrames = config.Get(advancedstr, SkipFramesStr).ToInt32(SkipFrames);
                stockvalues = config.Get(advancedstr, stockvaluesStr).ToBoolean(stockvalues);

                //framesperprint = config.Get(configstr, framesperprintStr).ToInt32(framesperprint);
                ShowMetrics = config.Get(configstr, ShowMetricsStr).ToBoolean(ShowMetrics);
                controlModule = config.Get(configstr, controlModuleStr).ToBoolean(controlModule);
            }

            SetConfig();
            RConfig(config.ToString());
        }

        double timepause = 0;
        void SetConfig()
        {
            config.Set(inistr, myNameStr, myName);
            config.SetSectionComment(inistr, " For more info, check Advanced I & II sections in the guide:\n https://steamcommunity.com/sharedfiles/filedetails/?id=2861711651\n ");
            string sstr = tagSurround[0].Equals(tagSurround[1]) ? tagSurround[0] : tagSurround[0] + tagSurround[1];
            config.Set(inistr, TagSurroundStr, sstr);

            string aggstr = String.Join(" ; ", Aggressivity);
            config.Set(detectstr, AggressivityStr, aggstr);
            config.Set(detectstr, ErrorMarginStr, ErrorMargin);
            config.Set(detectstr, velprecisionmodestr, velprecisionmode);
            string ltcstr = String.Join(" ; ", new double[] { lowThrustCutOn, lowThrustCutOff });
            config.Set(detectstr, lowThrustCutStr, ltcstr);
            string ltccstr = String.Join(" ; ", new double[] { lowThrustCutCruiseOn, lowThrustCutCruiseOff });
            config.Set(detectstr, lowThrustCutCruiseStr, ltccstr);

            string accstr = String.Join(" ; ", Accelerations);
            config.Set(accelstr, AccelerationsStr, accstr);

            config.Set(parkstr, TurnOffThrustersOnParkStr, TurnOffThrustersOnPark);
            config.Set(parkstr, RechargeOnParkStr, RechargeOnPark);
            config.Set(parkstr, BackupSubstringStr, BackupSubstring);

            config.Set(parkstr, AutoAddGridConnectorsStr, AutoAddGridConnectors);
            config.SetComment(parkstr, AutoAddGridConnectorsStr, "\n");
            config.Set(parkstr, AutoAddGridLandingGearsStr, AutoAddGridLandingGears);
            config.Set(parkstr, forceparkifstaticStr, forceparkifstatic);
            config.SetComment(parkstr, forceparkifstaticStr, "\n");
            config.Set(parkstr, PerformanceWhileParkStr, PerformanceWhilePark);

            string ttdmstr = String.Join(" ; ", thrdirmultiplier);
            config.Set(parkstr, thrdirmultiplierStr, ttdmstr);
            config.SetComment(parkstr, thrdirmultiplierStr, "\n");
            config.Set(parkstr, thrdiroverrideStr, thrdiroverride);

            SetColor(whipahstr, OrientationColorStr, config, WH.OrientationColor);
            SetColor(whipahstr, RetrogradeColorStr, config, WH.RetrogradeColor);
            SetColor(whipahstr, SkyColorStr, config, WH.SkyColor);
            SetColor(whipahstr, HorizonLineColorStr, config, WH.HorizonLineColor);
            SetColor(whipahstr, ElevationLineColorStr, config, WH.ElevationLineColor);
            SetColor(whipahstr, ForwardArrowColorStr, config, WH.ForwardArrowColor);
            SetColor(whipahstr, OnlinecolorStr, config, WH.Onlinecolor);
            SetColor(whipahstr, OfflinecolorStr, config, WH.Offlinecolor);
            SetColor(whipahstr, TextBoxBackgroundStr, config, WH.TextBoxBackground);
            config.Set(whipahstr, ReticuleSensStr, WH.ReticuleSens);
            config.Set(whipahstr, DampreticuleSensStr, WH.DampreticuleSens);

            config.Set(advancedstr, cruisePlaneStr, cruisePlane);
            config.Set(advancedstr, FramesBetweenActionsStr, FramesBetweenActions);

            config.Set(advancedstr, framesperprintStr, framesperprint);

            config.Set(advancedstr, SkipFramesStr, SkipFrames);
            config.Set(advancedstr, stockvaluesStr, stockvalues);

            //config.Set(configstr, framesperprintStr, framesperprint);
            config.Set(configstr, ShowMetricsStr, ShowMetrics);
            config.Set(configstr, controlModuleStr, controlModule);
        }

        string savedconfig = "";

        void RConfig(string output)
        {
            if (output != Me.CustomData) Me.CustomData = output;
            try { if (!Me.CustomData.Contains($"\n---\n{textSurfaceKeyword}0")) Me.CustomData = Me.CustomData.Replace(Me.CustomData.Between("\n---\n", "0")[0], textSurfaceKeyword); }
            catch { if (!justCompiled) log.AppendNR("No tag found textSufaceKeyword\n"); }
            if (!Me.CustomData.Contains($"\n---\n{textSurfaceKeyword}0")) Me.CustomData += $"\n---\n{textSurfaceKeyword}0";

            savedconfig = Me.CustomData;
        }

        void KeepConfig()
        {
            if (justCompiled && configCD.TryParse(Me.CustomData))
            {
                SetConfig();
                List<MyIniKey> ccd = new List<MyIniKey>();
                List<MyIniKey> ccfg = new List<MyIniKey>();

                configCD.GetKeys(ccd);
                config.GetKeys(ccfg);

                foreach (MyIniKey cd in ccd)
                {
                    foreach (MyIniKey cfg in ccfg)
                    {
                        if (cd.Equals(cfg)) config.Set(cfg, configCD.Get(cd).ToString());
                    }
                }
                RConfig(config.ToString());
            }
            configCD.Clear();
        }

        /// <summary>
        /// Adds a Color to a MyIni object
        /// </summary>
        public static void SetColor(string sectionName, string itemName, MyIni ini, Color color)
        {
            string colorString = string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A);
            ini.Set(sectionName, itemName, colorString);
        }

        /// <summary>
        /// Parses a MyIni for a Color
        /// </summary>
        public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
        {
            string rgbString = ini.Get(sectionName, itemName).ToString("null");
            string[] rgbSplit = rgbString.Split(',');

            int r, g, b, a;
            if (rgbSplit.Length != 4)
            {
                if (defaultChar.HasValue)
                    return defaultChar.Value;
                else
                    return Color.Transparent;
            }

            int.TryParse(rgbSplit[0].Trim(), out r);
            int.TryParse(rgbSplit[1].Trim(), out g);
            int.TryParse(rgbSplit[2].Trim(), out b);
            bool hasAlpha = int.TryParse(rgbSplit[3].Trim(), out a);
            if (!hasAlpha)
                a = 255;

            r = MathHelper.Clamp(r, 0, 255);
            g = MathHelper.Clamp(g, 0, 255);
            b = MathHelper.Clamp(b, 0, 255);
            a = MathHelper.Clamp(a, 0, 255);

            return new Color(r, g, b, a);
        }

        // END ENTIRE CONFIG HANDLER

    }
}
